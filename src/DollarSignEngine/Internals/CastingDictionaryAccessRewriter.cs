namespace DollarSignEngine.Internals;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq; // Required for Enumerable.Repeat and .Any()

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
                return GetParsableTypeName(elementType) + $"[{commas}]";
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
        if (type == typeof(object)) return "object";
        if (type == typeof(void)) return "void";

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

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        string identifierName = node.Identifier.Text;

        if (_globalVariableTypes.TryGetValue(identifierName, out Type? originalType))
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

                TypeSyntax typeSyntaxToCastTo = SyntaxFactory.ParseTypeName(GetParsableTypeName(originalType));

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
}
