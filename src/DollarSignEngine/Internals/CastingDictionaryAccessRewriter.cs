using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DollarSignEngine.Internals;

internal class CastingDictionaryAccessRewriter : CSharpSyntaxRewriter
{
    private readonly IDictionary<string, Type> _globalVariableTypes;
    private const string GlobalsPropertyName = "Globals";

    public CastingDictionaryAccessRewriter(IDictionary<string, Type> globalVariableTypes)
    {
        _globalVariableTypes = globalVariableTypes ?? throw new ArgumentNullException(nameof(globalVariableTypes));
    }

    private bool IsAnonymousType(Type type) => DollarSign.IsAnonymousType(type);

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

    public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        // Check if this is accessing an array or collection that might contain anonymous types
        if (node.Expression is IdentifierNameSyntax identifierName &&
            _globalVariableTypes.TryGetValue(identifierName.Identifier.Text, out var collectionType) &&
            (collectionType.IsArray || IsCollectionType(collectionType)))
        {
            // Get the element type
            Type? elementType = null;
            if (collectionType.IsArray)
            {
                elementType = collectionType.GetElementType();
            }
            else if (collectionType.IsGenericType)
            {
                var genericArgs = collectionType.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    elementType = genericArgs[0];
                }
            }

            // For arrays or collections of objects or anonymous types, we need special handling
            if (elementType != null && (IsAnonymousType(elementType) || elementType == typeof(object)))
            {
                // Visit the expression and arguments normally
                var newExpression = (ExpressionSyntax)Visit(node.Expression);
                var newArgumentList = (BracketedArgumentListSyntax)Visit(node.ArgumentList);

                // Create the element access
                var elementAccess = node.Update(newExpression, newArgumentList);

                // Cast the result to dynamic to allow property access
                var castExpression = SyntaxFactory.CastExpression(
                    SyntaxFactory.ParseTypeName("dynamic"),
                    elementAccess
                );

                return SyntaxFactory.ParenthesizedExpression(castExpression).WithTriviaFrom(node);
            }
        }

        // Default processing for other element access expressions
        return base.VisitElementAccessExpression(node);
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // First check if this is a nested property access on a dictionary
        if (node.Expression is IdentifierNameSyntax identifierName &&
            _globalVariableTypes.TryGetValue(identifierName.Identifier.Text, out var type) &&
            IsDictionaryType(type))
        {
            // This is a property access on a dictionary, we need to transform it to dictionary lookup
            // e.g., Address.City => ((dynamic)Globals["Address"])["City"]

            // First, get the rewritten identifier name
            var rewrittenExpr = (ExpressionSyntax)Visit(identifierName);

            // Create the element access for the property
            var propertyNameArg = SyntaxFactory.Argument(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(node.Name.Identifier.Text)
                )
            );

            var bracketedArgList = SyntaxFactory.BracketedArgumentList(
                SyntaxFactory.SingletonSeparatedList(propertyNameArg)
            );

            // Create the element access
            var elementAccessExpr = SyntaxFactory.ElementAccessExpression(
                rewrittenExpr,
                bracketedArgList
            );

            return elementAccessExpr.WithTriviaFrom(node);
        }
        // Check if this is accessing a property of an array/collection element
        else if (node.Expression is ElementAccessExpressionSyntax elementAccess &&
            elementAccess.Expression is IdentifierNameSyntax collectionIdentifier &&
            _globalVariableTypes.TryGetValue(collectionIdentifier.Identifier.Text, out var collectionType) &&
            (collectionType.IsArray || IsCollectionType(collectionType)))
        {
            // Get the element type
            Type? elementType = null;
            if (collectionType.IsArray)
            {
                elementType = collectionType.GetElementType();
            }
            else if (collectionType.IsGenericType)
            {
                var genericArgs = collectionType.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    elementType = genericArgs[0];
                }
            }

            // For arrays or collections of objects or anonymous types, we need special handling
            if (elementType != null && (IsAnonymousType(elementType) || elementType == typeof(object)))
            {
                // Special handling for accessing properties on array elements with anonymous types
                if (node.Expression is ElementAccessExpressionSyntax elementAccessInner)
                {
                    // Cast the element to dynamic first
                    var newExpression1 = (ExpressionSyntax)Visit(elementAccessInner.Expression);
                    var newArgList = (BracketedArgumentListSyntax)Visit(elementAccessInner.ArgumentList);
                    var rewrittenElementAccess = elementAccessInner.Update(newExpression1, newArgList);

                    // Cast the element access to dynamic
                    var castExpression = SyntaxFactory.CastExpression(
                        SyntaxFactory.ParseTypeName("dynamic"),
                        rewrittenElementAccess
                    );
                    var parenthesizedCast = SyntaxFactory.ParenthesizedExpression(castExpression);

                    // Create the member access with the new expression
                    var newMemberAccess = node.Update(parenthesizedCast, node.OperatorToken, (SimpleNameSyntax)Visit(node.Name));
                    return newMemberAccess;
                }
            }

            // Default member access processing
            var newStandardExpression = (ExpressionSyntax)Visit(node.Expression);
            var newStandardName = (SimpleNameSyntax)Visit(node.Name);
            return node.Update(newStandardExpression, node.OperatorToken, newStandardName);
        }
        else if (IsNestedDictionaryAccess(node))
        {
            // Handle nested dictionary property access like "Address.City" where Address is a dictionary
            return TransformNestedPropertyPath(node);
        }

        // If not a special case, process normally
        var newExpression = (ExpressionSyntax)Visit(node.Expression);
        var newName = (SimpleNameSyntax)Visit(node.Name);
        return node.Update(newExpression, node.OperatorToken, newName);
    }

    // Check if this is a nested property access on dictionaries (e.g., Address.City.District)
    private bool IsNestedDictionaryAccess(MemberAccessExpressionSyntax node)
    {
        // Recursively check if this is a nested property path that starts with a dictionary
        ExpressionSyntax current = node.Expression;
        string rootIdentifier = null;

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

            // Check context for type determination
            bool isPartOfNestedProperty = false;
            bool isLikelyToBeUsedInLinq = IsLikelyToBeUsedInLinq(parent, node);
            bool containsAnonymousTypeOrObject = ContainsAnonymousTypeOrObject(originalType);
            bool isDictionary = IsDictionaryType(originalType);

            if (parent is MemberAccessExpressionSyntax maesParent && maesParent.Expression == node)
            {
                isPartOfNestedProperty = true;
            }

            // If the identifier is the expression part of an element access, don't rewrite it
            // We'll handle it in VisitElementAccessExpression
            if (parent is ElementAccessExpressionSyntax eas && eas.Expression == node)
            {
                if (originalType.IsArray || IsCollectionType(originalType))
                {
                    // For collections or arrays, let the type system handle it directly
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

                // Determine appropriate type cast based on context
                TypeSyntax typeSyntaxToCastTo;

                // For dictionaries, always use dynamic to allow property access
                if (isDictionary)
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic");
                }
                // For arrays of anonymous types, use dynamic[]
                else if (originalType.IsArray && originalType.GetElementType() != null &&
                    (IsAnonymousType(originalType.GetElementType()!) || originalType.GetElementType() == typeof(object)))
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic[]");
                }
                // For collections that might contain anonymous types, use dynamic
                else if (IsCollectionType(originalType) && containsAnonymousTypeOrObject)
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic");
                }
                // For collections likely to be used with LINQ, preserve the original type
                else if (isLikelyToBeUsedInLinq && (originalType.IsArray || IsCollectionType(originalType)))
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName(GetParsableTypeName(originalType));
                }
                // For regular collections, preserve their type
                else if (originalType.IsArray || IsCollectionType(originalType))
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName(GetParsableTypeName(originalType));
                }
                // Anonymous types always use dynamic
                else if (IsAnonymousType(originalType))
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic");
                }
                // For nested property access with non-collection types, use dynamic
                else if (isPartOfNestedProperty)
                {
                    typeSyntaxToCastTo = SyntaxFactory.ParseTypeName("dynamic");
                }
                // For all other cases, use the original type
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

    // Helper method to check if a collection type contains anonymous types or object elements
    private bool ContainsAnonymousTypeOrObject(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            return elementType != null && (IsAnonymousType(elementType) || elementType == typeof(object));
        }

        if (type.IsGenericType)
        {
            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                return IsAnonymousType(genericArgs[0]) || genericArgs[0] == typeof(object);
            }
        }

        return false;
    }

    // Helper method to detect if a node is likely to be used in a LINQ operation
    private bool IsLikelyToBeUsedInLinq(SyntaxNode? parent, SyntaxNode node)
    {
        if (parent == null) return false;

        // Check if this node is part of a member access that might be a LINQ method
        if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
        {
            string memberName = memberAccess.Name.ToString();
            // Common LINQ method names
            string[] linqMethods = { "Where", "Select", "OrderBy", "OrderByDescending", "GroupBy",
                                    "Join", "Skip", "Take", "First", "FirstOrDefault", "Last",
                                    "LastOrDefault", "Single", "SingleOrDefault", "Any", "All",
                                    "Count", "Sum", "Min", "Max", "Average", "Aggregate", "ToList",
                                    "ToArray", "ToDictionary", "ToLookup" };

            return linqMethods.Contains(memberName);
        }

        // Check if parent is an invocation and this node is the expression
        if (parent is InvocationExpressionSyntax invocation && invocation.Expression is MemberAccessExpressionSyntax invocationMember)
        {
            if (invocationMember.Expression == node)
            {
                string methodName = invocationMember.Name.ToString();
                // Check if the method is likely a LINQ method
                string[] linqMethods = { "Where", "Select", "OrderBy", "OrderByDescending", "GroupBy",
                                        "Join", "Skip", "Take", "First", "FirstOrDefault", "Last",
                                        "LastOrDefault", "Single", "SingleOrDefault", "Any", "All",
                                        "Count", "Sum", "Min", "Max", "Average", "Aggregate", "ToList",
                                        "ToArray", "ToDictionary", "ToLookup" };

                return linqMethods.Contains(methodName);
            }
        }

        return false;
    }
}