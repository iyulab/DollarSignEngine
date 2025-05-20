using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DollarSignEngine.Internals;

/// <summary>
/// Syntax rewriter for transforming dictionary and collection access expressions for proper type casting.
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

                // Handle array type based on element type
                string elementTypeName = GetParsableTypeName(elementType);

                // If array contains anonymous types or objects, use dynamic[]
                if (IsAnonymousType(elementType) || elementType == typeof(object))
                {
                    return "dynamic[]";
                }

                // For arrays that might be used with LINQ (numeric types)
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

        // Check for dictionary types
        if (IsDictionaryType(type))
        {
            // Always use dynamic for dictionaries to allow proper nested property access
            return "dynamic";
        }

        // Check if type is a generic collection
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments();

            // Handle collection types with special cases for anonymous types and objects
            if ((genericTypeDef == typeof(List<>) ||
                 genericTypeDef == typeof(IEnumerable<>) ||
                 genericTypeDef == typeof(ICollection<>) ||
                 genericTypeDef == typeof(IList<>)) &&
                genericArgs.Length == 1)
            {
                var elementType = genericArgs[0];

                // Use dynamic for collections with anonymous types or objects
                if (IsAnonymousType(elementType) || elementType == typeof(object))
                {
                    return "dynamic";
                }

                // Otherwise preserve the original collection type
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
        if (type == typeof(object)) return "dynamic"; // Use dynamic for object type to allow property access
        if (type == typeof(void)) return "void";

        // Handle ExpandoObject and Dynamic types as dynamic
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

    // Helper method to check if a type is a dictionary
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

    // Get the element type from a collection
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

        // This handles array or collection access: Items[0]
        if (newExpression is IdentifierNameSyntax identifierName &&
            _globalVariableTypes.TryGetValue(identifierName.Identifier.Text, out var collectionType) &&
            (collectionType.IsArray || IsCollectionType(collectionType)))
        {
            Type? elementType = GetElementType(collectionType);

            // If the element type is anonymous, uses ExpandoObject, or is dictionary-like
            if (elementType != null && (IsAnonymousType(elementType) ||
                                       typeof(System.Dynamic.ExpandoObject).IsAssignableFrom(elementType) ||
                                       IsDictionaryType(elementType) ||
                                       elementType == typeof(object)))
            {
                // Create a standard element access expression but cast to dynamic
                var elementAccess = node.Update(newExpression, newArgumentList);

                var castExpression = SyntaxFactory.CastExpression(
                    SyntaxFactory.ParseTypeName("dynamic"),
                    elementAccess
                );

                return SyntaxFactory.ParenthesizedExpression(castExpression).WithTriviaFrom(node);
            }
        }

        // For other cases, just update normally
        return node.Update(newExpression, newArgumentList);
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Transform the expression part first
        var newExpression = (ExpressionSyntax)Visit(node.Expression);
        var newName = (SimpleNameSyntax)Visit(node.Name);

        // Case 1: Direct property access on a dictionary (obj.Property)
        if (node.Expression is IdentifierNameSyntax identifierName &&
            _globalVariableTypes.TryGetValue(identifierName.Identifier.Text, out var type) &&
            IsDictionaryType(type))
        {
            // Transform to dictionary indexer: obj.Property => ((dynamic)Globals["obj"])["Property"]
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
        // Case 2: Access on an element from collection/array (arrayOrCollection[index].Property)
        else if (node.Expression is ElementAccessExpressionSyntax elementAccess)
        {
            // Try to determine if the collection might contain anonymous types or dictionaries
            bool requiresSpecialHandling = false;

            if (elementAccess.Expression is IdentifierNameSyntax collectionIdentifier &&
                _globalVariableTypes.TryGetValue(collectionIdentifier.Identifier.Text, out var collectionType))
            {
                Type? elementType = GetElementType(collectionType);
                if (elementType != null && (IsAnonymousType(elementType) ||
                                          IsDictionaryType(elementType) ||
                                          elementType == typeof(object)))
                {
                    requiresSpecialHandling = true;
                }
            }

            if (requiresSpecialHandling)
            {
                // Instead of using indexer access, use direct property name
                // This transforms: array[index].Property => ((dynamic)array[index]).Property

                // First visit the element access
                var updatedElementAccess = (ElementAccessExpressionSyntax)Visit(elementAccess);

                // Cast to dynamic
                var castExpression = SyntaxFactory.CastExpression(
                    SyntaxFactory.ParseTypeName("dynamic"),
                    updatedElementAccess
                );

                // Wrap in parentheses
                var parenthesizedCast = SyntaxFactory.ParenthesizedExpression(castExpression);

                // Create a standard property access on the cast
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    parenthesizedCast,
                    newName
                );

                return memberAccess.WithTriviaFrom(node);
            }
        }
        // Case 3: Nested property access on dictionaries (Address.City.District)
        else if (IsNestedDictionaryAccess(node))
        {
            return TransformNestedPropertyPath(node);
        }

        // Default case: update normally
        return node.Update(newExpression, node.OperatorToken, newName);
    }

    // Check if this is a nested property access on dictionaries (e.g., Address.City.District)
    private bool IsNestedDictionaryAccess(MemberAccessExpressionSyntax node)
    {
        // Recursively check if this is a nested property path that starts with a dictionary
        ExpressionSyntax current = node.Expression;
        string? rootIdentifier = null;

        // Walk up the property chain to find the root
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

    // Transform nested property path (Address.City.District) to dictionary indexers
    private SyntaxNode TransformNestedPropertyPath(MemberAccessExpressionSyntax node)
    {
        // Get the property name
        string propertyName = node.Name.Identifier.Text;

        // Recursively transform the expression part
        ExpressionSyntax transformedExpression;

        if (node.Expression is MemberAccessExpressionSyntax nestedAccess)
        {
            // Recursively handle the nested access
            transformedExpression = (ExpressionSyntax)TransformNestedPropertyPath(nestedAccess);
        }
        else if (node.Expression is IdentifierNameSyntax ins)
        {
            // Base case - this is the root identifier
            transformedExpression = (ExpressionSyntax)Visit(ins);
        }
        else
        {
            // For any other expression type, just visit it normally
            transformedExpression = (ExpressionSyntax)Visit(node.Expression);
        }

        // Create the indexer for the current property
        var propertyNameArg = SyntaxFactory.Argument(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(propertyName)
            )
        );

        var bracketedArgList = SyntaxFactory.BracketedArgumentList(
            SyntaxFactory.SingletonSeparatedList(propertyNameArg)
        );

        // Create the element access
        var elementAccessExpr = SyntaxFactory.ElementAccessExpression(
            transformedExpression,
            bracketedArgList
        );

        return elementAccessExpr.WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        string identifierName = node.Identifier.Text;

        // First check for case-sensitive match
        bool identifierExists = _globalVariableTypes.ContainsKey(identifierName);

        // If not found, try case-insensitive lookup
        if (!identifierExists)
        {
            foreach (var key in _globalVariableTypes.Keys)
            {
                if (string.Equals(key, identifierName, StringComparison.OrdinalIgnoreCase))
                {
                    identifierName = key; // Use the actual key with correct casing
                    identifierExists = true;
                    break;
                }
            }
        }

        if (identifierExists && _globalVariableTypes.TryGetValue(identifierName, out Type? originalType))
        {
            var parent = node.Parent;
            bool skipRewrite = false;

            // Check various parent syntax to determine if we should skip rewriting
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
            else if (parent is UsingDirectiveSyntax uds && (uds.Name.ToString() == identifierName || uds.Name.ToString().EndsWith("." + identifierName))) skipRewrite = true;
            else if (parent is QualifiedNameSyntax qnsParent && qnsParent.Left.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(id => id == node)) skipRewrite = true;

            // Special handling for element access
            if (parent is ElementAccessExpressionSyntax eas && eas.Expression == node)
            {
                if (originalType.IsArray || IsCollectionType(originalType))
                {
                    // The parent is already handling the array/collection access
                    skipRewrite = false;
                }
            }

            if (!skipRewrite)
            {
                // Create a Globals dictionary access: Identifier => ((TYPE)Globals["Identifier"])
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

                // Determine what type to cast to
                TypeSyntax typeSyntaxToCastTo;

                // Special handling for different types
                if (IsDictionaryType(originalType))
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic");
                }
                else if (originalType.IsArray || IsCollectionType(originalType))
                {
                    Type? elementType = GetElementType(originalType);

                    // For collections with anonymous types or dictionaries, use dynamic
                    if (elementType != null && (IsAnonymousType(elementType) ||
                                               IsDictionaryType(elementType) ||
                                               elementType == typeof(object)))
                    {
                        typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic");
                    }
                    else
                    {
                        // For regular collections, use the actual type
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

                // Create the cast expression
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

    // Helper method to check if a type is a collection
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