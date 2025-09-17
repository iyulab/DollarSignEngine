using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DollarSignEngine.Internals;

/// <summary>
/// Syntax rewriter for transforming dictionary and collection access expressions for proper type casting.
/// Enhanced to handle LINQ iterator types and anonymous type collections.
/// </summary>
internal class CastingDictionaryAccessRewriter : CSharpSyntaxRewriter
{
    private readonly IDictionary<string, Type> _globalVariableTypes;
    private const string GlobalsPropertyName = "Globals";

    public CastingDictionaryAccessRewriter(IDictionary<string, Type> globalVariableTypes)
    {
        _globalVariableTypes = globalVariableTypes ?? throw new ArgumentNullException(nameof(globalVariableTypes));
    }

    private bool IsAnonymousType(Type type) => DataPreparationHelper.IsAnonymousType(type);

    private bool IsLinqIteratorType(Type type)
    {
        if (!type.IsGenericType) return false;

        var typeName = type.Name;
        return typeName.Contains("Iterator") ||
               typeName.Contains("Enumerable") ||
               type.FullName?.Contains("System.Linq") == true;
    }

    private string GetParsableTypeName(Type type)
    {
        if (IsAnonymousType(type)) return "dynamic";

        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            return GetParsableTypeName(underlyingType) + "?";
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            if (elementType != null)
            {
                int rank = type.GetArrayRank();
                string commas = rank > 1 ? new string(',', rank - 1) : "";

                string elementTypeName = GetParsableTypeName(elementType);

                if (IsAnonymousType(elementType) || elementType == typeof(object))
                {
                    return "dynamic[]";
                }

                if (elementType == typeof(int) ||
                    elementType == typeof(long) ||
                    elementType == typeof(float) ||
                    elementType == typeof(double) ||
                    elementType == typeof(decimal))
                {
                    return $"{elementTypeName}[{commas}]";
                }

                return $"{elementTypeName}[{commas}]";
            }
        }

        if (IsDictionaryType(type))
        {
            return "dynamic";
        }

        // Handle LINQ iterator types by returning dynamic
        if (IsLinqIteratorType(type))
        {
            return "dynamic";
        }

        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments();

            if ((genericTypeDef == typeof(List<>) ||
                 genericTypeDef == typeof(IEnumerable<>) ||
                 genericTypeDef == typeof(ICollection<>) ||
                 genericTypeDef == typeof(IList<>)) &&
                genericArgs.Length == 1)
            {
                var elementType = genericArgs[0];

                if (IsAnonymousType(elementType) || elementType == typeof(object))
                {
                    return "dynamic";
                }

                string baseName = genericTypeDef.FullName?.Split('`')[0] ?? genericTypeDef.Name.Split('`')[0];
                baseName = baseName.Replace('+', '.');
                var elementTypeName = GetParsableTypeName(elementType);
                return $"{baseName}<{elementTypeName}>";
            }
        }

        if (type == typeof(bool)) return "bool";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(sbyte)) return "sbyte";
        if (type == typeof(char)) return "char";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(int)) return "int";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(long)) return "long";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(short)) return "short";
        if (type == typeof(ushort)) return "ushort";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "dynamic";
        if (type == typeof(void)) return "void";

        if (type == typeof(System.Dynamic.ExpandoObject) ||
            type == typeof(System.Dynamic.DynamicObject) ||
            typeof(System.Dynamic.IDynamicMetaObjectProvider).IsAssignableFrom(type))
        {
            return "dynamic";
        }

        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments().Select(GetParsableTypeName);

            string baseName = genericTypeDef.FullName?.Split('`')[0] ?? genericTypeDef.Name.Split('`')[0];
            baseName = baseName.Replace('+', '.');
            return $"{baseName}<{string.Join(", ", genericArgs)}>";
        }

        var fullName = type.FullName ?? type.Name;
        return fullName.Replace('+', '.');
    }

    private bool IsDictionaryType(Type type)
    {
        if (type.IsGenericType)
        {
            Type genericDef = type.GetGenericTypeDefinition();
            return genericDef == typeof(Dictionary<,>) ||
                   genericDef == typeof(IDictionary<,>) ||
                   genericDef == typeof(IReadOnlyDictionary<,>);
        }

        return typeof(IDictionary).IsAssignableFrom(type);
    }

    private Type? GetElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        if (collectionType.IsGenericType)
        {
            var genericArgs = collectionType.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                return genericArgs[0];
            }
        }

        return null;
    }

    public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        var newExpression = (ExpressionSyntax)Visit(node.Expression);
        var newArgumentList = (BracketedArgumentListSyntax)Visit(node.ArgumentList);

        Logger.Debug($"[CastingRewriter] VisitElementAccessExpression: {node}");

        if (newExpression is IdentifierNameSyntax identifierName &&
            _globalVariableTypes.TryGetValue(identifierName.Identifier.Text, out var collectionType))
        {
            Logger.Debug($"[CastingRewriter] Found variable: {identifierName.Identifier.Text}, Type: {collectionType.FullName}");

            // Handle LINQ iterator types or collections that need dynamic casting
            if (IsLinqIteratorType(collectionType) ||
                (collectionType.IsArray || IsCollectionType(collectionType)))
            {
                Type? elementType = GetElementType(collectionType);
                Logger.Debug($"[CastingRewriter] Element type: {elementType?.FullName ?? "null"}");

                // List<object>의 경우 특별 처리
                if (elementType == typeof(object) &&
                    collectionType.IsGenericType &&
                    collectionType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    Logger.Debug("[CastingRewriter] Detected List<object>, applying object cast");

                    var elementAccess = node.Update(newExpression, newArgumentList);
                    var castExpression = SyntaxFactory.CastExpression(
                        SyntaxFactory.ParseTypeName("object"),
                        elementAccess
                    );
                    var result = SyntaxFactory.ParenthesizedExpression(castExpression).WithTriviaFrom(node);

                    Logger.Debug($"[CastingRewriter] Transformed to: {result}");
                    return result;
                }

                // 다른 경우에만 dynamic 캐스팅
                if ((elementType != null && (IsAnonymousType(elementType) ||
                                           typeof(System.Dynamic.ExpandoObject).IsAssignableFrom(elementType) ||
                                           IsDictionaryType(elementType))) ||
                    IsLinqIteratorType(collectionType) ||
                    (collectionType.IsArray && collectionType.GetElementType() == typeof(object)))
                {
                    Logger.Debug("[CastingRewriter] Applying dynamic cast");

                    var elementAccess = node.Update(newExpression, newArgumentList);
                    var castExpression = SyntaxFactory.CastExpression(
                        SyntaxFactory.ParseTypeName("dynamic"),
                        elementAccess
                    );
                    var result = SyntaxFactory.ParenthesizedExpression(castExpression).WithTriviaFrom(node);

                    Logger.Debug($"[CastingRewriter] Transformed to: {result}");
                    return result;
                }
            }
        }

        Logger.Debug($"[CastingRewriter] No transformation applied");
        return node.Update(newExpression, newArgumentList);
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var newExpression = (ExpressionSyntax)Visit(node.Expression);
        var newName = (SimpleNameSyntax)Visit(node.Name);

        Logger.Debug($"[CastingRewriter] VisitMemberAccessExpression: {node}");

        // 딕셔너리 타입 처리
        if (node.Expression is IdentifierNameSyntax identifierName &&
            _globalVariableTypes.TryGetValue(identifierName.Identifier.Text, out var type) &&
            IsDictionaryType(type))
        {
            var propertyNameArg = SyntaxFactory.Argument(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(node.Name.Identifier.Text)
                )
            );

            var bracketedArgList = SyntaxFactory.BracketedArgumentList(
                SyntaxFactory.SingletonSeparatedList(propertyNameArg)
            );

            var elementAccessExpr = SyntaxFactory.ElementAccessExpression(
                newExpression,
                bracketedArgList
            );

            return elementAccessExpr.WithTriviaFrom(node);
        }
        // 배열/컬렉션 요소에 대한 멤버 접근 처리
        else if (node.Expression is ElementAccessExpressionSyntax elementAccess)
        {
            Logger.Debug($"[CastingRewriter] Processing member access on element access: {elementAccess}");

            if (elementAccess.Expression is IdentifierNameSyntax collectionIdentifier &&
                _globalVariableTypes.TryGetValue(collectionIdentifier.Identifier.Text, out var collectionType))
            {
                Type? elementType = GetElementType(collectionType);
                Logger.Debug($"[CastingRewriter] Collection: {collectionIdentifier.Identifier.Text}, Type: {collectionType.FullName}, ElementType: {elementType?.FullName}");

                // List<object>의 경우 리플렉션 기반 속성 접근으로 변경
                if (elementType == typeof(object) &&
                    collectionType.IsGenericType &&
                    collectionType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    Logger.Debug("[CastingRewriter] Converting List<object> member access to SafeGetProperty");

                    var updatedElementAccess = (ElementAccessExpressionSyntax)Visit(elementAccess);

                    // ScriptHost.SafeGetProperty 호출로 변환
                    var safeGetCall = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("ScriptHost"),
                            SyntaxFactory.IdentifierName("SafeGetProperty")))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList(new[]
                                {
                                SyntaxFactory.Argument(updatedElementAccess),
                                SyntaxFactory.Argument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(node.Name.Identifier.Text)))
                                })));

                    Logger.Debug($"[CastingRewriter] Transformed to: {safeGetCall}");
                    return safeGetCall.WithTriviaFrom(node);
                }

                // 다른 복잡한 타입들 (익명 타입, 딕셔너리, ExpandoObject 등)
                if (elementType != null && (IsAnonymousType(elementType) ||
                                          IsDictionaryType(elementType) ||
                                          typeof(System.Dynamic.ExpandoObject).IsAssignableFrom(elementType)) ||
                    IsLinqIteratorType(collectionType) ||
                    (collectionType.IsArray && collectionType.GetElementType() == typeof(object)))
                {
                    Logger.Debug("[CastingRewriter] Applying dynamic cast for member access");

                    var updatedElementAccess = (ElementAccessExpressionSyntax)Visit(elementAccess);

                    var castExpression = SyntaxFactory.CastExpression(
                        SyntaxFactory.ParseTypeName("dynamic"),
                        updatedElementAccess
                    );

                    var parenthesizedCast = SyntaxFactory.ParenthesizedExpression(castExpression);

                    var memberAccess = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        parenthesizedCast,
                        newName
                    );

                    Logger.Debug($"[CastingRewriter] Transformed to: {memberAccess}");
                    return memberAccess.WithTriviaFrom(node);
                }
            }
        }
        // 중첩된 딕셔너리 접근 처리 (예: dict.prop1.prop2)
        else if (IsNestedDictionaryAccess(node))
        {
            return TransformNestedPropertyPath(node);
        }

        Logger.Debug($"[CastingRewriter] No transformation applied for member access");
        return node.Update(newExpression, node.OperatorToken, newName);
    }

    private bool IsNestedDictionaryAccess(MemberAccessExpressionSyntax node)
    {
        ExpressionSyntax current = node.Expression;
        string? rootIdentifier = null;

        while (current is MemberAccessExpressionSyntax maes)
        {
            current = maes.Expression;
        }

        if (current is IdentifierNameSyntax ins)
        {
            rootIdentifier = ins.Identifier.Text;
            if (_globalVariableTypes.TryGetValue(rootIdentifier, out var rootType))
            {
                return IsDictionaryType(rootType);
            }
        }

        return false;
    }

    private SyntaxNode TransformNestedPropertyPath(MemberAccessExpressionSyntax node)
    {
        string propertyName = node.Name.Identifier.Text;

        ExpressionSyntax transformedExpression;

        if (node.Expression is MemberAccessExpressionSyntax nestedAccess)
        {
            transformedExpression = (ExpressionSyntax)TransformNestedPropertyPath(nestedAccess);
        }
        else if (node.Expression is IdentifierNameSyntax ins)
        {
            transformedExpression = (ExpressionSyntax)Visit(ins);
        }
        else
        {
            transformedExpression = (ExpressionSyntax)Visit(node.Expression);
        }

        var propertyNameArg = SyntaxFactory.Argument(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(propertyName)
            )
        );

        var bracketedArgList = SyntaxFactory.BracketedArgumentList(
            SyntaxFactory.SingletonSeparatedList(propertyNameArg)
        );

        var elementAccessExpr = SyntaxFactory.ElementAccessExpression(
            transformedExpression,
            bracketedArgList
        );

        return elementAccessExpr.WithTriviaFrom(node);
    }
    
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        string identifierName = node.Identifier.Text;

        bool identifierExists = _globalVariableTypes.ContainsKey(identifierName);

        if (!identifierExists)
        {
            foreach (var key in _globalVariableTypes.Keys)
            {
                if (string.Equals(key, identifierName, StringComparison.OrdinalIgnoreCase))
                {
                    identifierName = key;
                    identifierExists = true;
                    break;
                }
            }
        }

        if (identifierExists && _globalVariableTypes.TryGetValue(identifierName, out Type? originalType))
        {
            var parent = node.Parent;
            bool skipRewrite = false;

            if (parent is MemberAccessExpressionSyntax maes && maes.Name == node) skipRewrite = true;
            else if (parent is QualifiedNameSyntax qns && qns.Right == node) skipRewrite = true;
            else if (parent is InvocationExpressionSyntax inv && inv.Expression == node) skipRewrite = true;
            else if (parent is TypeArgumentListSyntax tal && tal.Arguments.Any(a => a == node)) skipRewrite = true;
            else if (parent is ObjectCreationExpressionSyntax oces && oces.Type == node) skipRewrite = true;
            else if (parent is CastExpressionSyntax ces && ces.Type == node) skipRewrite = true;
            else if (parent is VariableDeclarationSyntax vds && vds.Type == node) skipRewrite = true;
            else if (parent is ParameterSyntax ps && ps.Type == node) skipRewrite = true;
            else if (parent is TypeConstraintSyntax tcs && tcs.Type == node) skipRewrite = true;
            else if (parent is PredefinedTypeSyntax) skipRewrite = true;
            else if (parent is CrefSyntax) skipRewrite = true;
            else if (parent is UsingDirectiveSyntax uds && (uds.Name?.ToString() == identifierName || uds.Name?.ToString().EndsWith("." + identifierName) == true)) skipRewrite = true;
            else if (parent is QualifiedNameSyntax qnsParent && qnsParent.Left.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(id => id == node)) skipRewrite = true;

            if (parent is ElementAccessExpressionSyntax eas && eas.Expression == node)
            {
                if (originalType.IsArray || IsCollectionType(originalType) || IsLinqIteratorType(originalType))
                {
                    skipRewrite = false;
                }
            }

            if (!skipRewrite)
            {
                var globalsIdentifier = SyntaxFactory.IdentifierName(GlobalsPropertyName);
                var argument = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(identifierName)
                ));
                var bracketedArgumentList = SyntaxFactory.BracketedArgumentList(
                    SyntaxFactory.SingletonSeparatedList(argument)
                );
                var elementAccessExpression = SyntaxFactory.ElementAccessExpression(
                    globalsIdentifier,
                    bracketedArgumentList
                );

                TypeSyntax typeSyntaxToCastTo;

                if (IsDictionaryType(originalType))
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic");
                }
                else if (originalType.IsArray || IsCollectionType(originalType) || IsLinqIteratorType(originalType))
                {
                    Type? elementType = GetElementType(originalType);

                    if (elementType != null && (IsAnonymousType(elementType) ||
                                               IsDictionaryType(elementType) ||
                                               elementType == typeof(object)) ||
                        IsLinqIteratorType(originalType))
                    {
                        typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic");
                    }
                    else
                    {
                        typeSyntaxToCastTo = SyntaxFactory.ParseTypeName(GetParsableTypeName(originalType));
                    }
                }
                else if (IsAnonymousType(originalType))
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic");
                }
                else
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName(GetParsableTypeName(originalType));
                }

                var castExpression = SyntaxFactory.CastExpression(
                    typeSyntaxToCastTo,
                    elementAccessExpression
                );
                var parenthesizedCastExpression = SyntaxFactory.ParenthesizedExpression(castExpression);
                return parenthesizedCastExpression.WithTriviaFrom(node);
            }
        }
        return base.VisitIdentifierName(node);
    }

    private bool IsCollectionType(Type type)
    {
        if (type.IsArray) return true;

        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            return genericTypeDef == typeof(List<>) ||
                   genericTypeDef == typeof(IEnumerable<>) ||
                   genericTypeDef == typeof(ICollection<>) ||
                   genericTypeDef == typeof(IList<>) ||
                   typeof(IEnumerable).IsAssignableFrom(type);
        }

        return typeof(IEnumerable).IsAssignableFrom(type);
    }
}