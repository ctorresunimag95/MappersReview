using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CustomMapper.SourceGenerator.Generator
{
    internal static class IgnoreMappingHelpers
    {
        private const string MapperConfigTypeName = "CustomMapper.SourceGenerator.Runtime.MapperConfig";
        
        public static Dictionary<ITypeSymbol, HashSet<string>> BuildIgnoreMap(
            ClassDeclarationSyntax classSyntax,
            SemanticModel semanticModel)
        {
            var result = new Dictionary<ITypeSymbol, HashSet<string>>(SymbolEqualityComparer.Default);

            foreach (var attributeList in classSyntax.AttributeLists)
            {
                foreach (var attributeSyntax in attributeList.Attributes)
                {
                    var attributeName = GetRightmostName(attributeSyntax.Name);
                    if (attributeName != "MapperIgnore" && attributeName != "MapperIgnoreAttribute")
                        continue;

                    if (attributeSyntax.ArgumentList is null || attributeSyntax.ArgumentList.Arguments.Count < 2)
                        continue;

                    var destinationExpression = attributeSyntax.ArgumentList.Arguments[0].Expression;
                    if (destinationExpression is not TypeOfExpressionSyntax typeOfExpression)
                        continue;

                    var destinationType = semanticModel.GetTypeInfo(typeOfExpression.Type).Type;
                    if (destinationType is null)
                        continue;

                    for (int i = 1; i < attributeSyntax.ArgumentList.Arguments.Count; i++)
                    {
                        if (TryExtractNameOfProperty(attributeSyntax.ArgumentList.Arguments[i].Expression, out var propertyName))
                            AddIgnoredProperty(result, destinationType, propertyName);
                    }
                }
            }

            foreach (var methodSyntax in classSyntax.Members.OfType<MethodDeclarationSyntax>())
            {
                if (methodSyntax.Identifier.ValueText != "ConfigureMapper")
                    continue;

                if (methodSyntax.ParameterList.Parameters.Count != 1)
                    continue;

                if (semanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
                    continue;

                if (methodSymbol.Parameters.Length != 1)
                    continue;

                if (methodSymbol.Parameters[0].Type.ToDisplayString() != MapperConfigTypeName)
                    continue;

                if (methodSyntax.Body is null)
                    continue;

                foreach (var invocation in methodSyntax.Body.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                        continue;

                    if (memberAccess.Name is not GenericNameSyntax genericName)
                        continue;

                    if (genericName.Identifier.ValueText != "Ignore")
                        continue;

                    if (genericName.TypeArgumentList.Arguments.Count != 1)
                        continue;

                    var destinationType = semanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type;
                    if (destinationType is null)
                        continue;

                    foreach (var argument in invocation.ArgumentList.Arguments)
                    {
                        if (TryExtractNameOfProperty(argument.Expression, out var propertyName))
                            AddIgnoredProperty(result, destinationType, propertyName);
                    }
                }
            }

            return result;
        }

        public static HashSet<string> ResolveIgnoredProperties(
            Dictionary<ITypeSymbol, HashSet<string>> ignoreMap,
            ITypeSymbol destinationType)
        {
            if (ignoreMap.TryGetValue(destinationType, out var ignored))
                return ignored;

            return new HashSet<string>(System.StringComparer.Ordinal);
        }

        private static void AddIgnoredProperty(
            Dictionary<ITypeSymbol, HashSet<string>> ignoreMap,
            ITypeSymbol destinationType,
            string propertyName)
        {
            if (!ignoreMap.TryGetValue(destinationType, out var properties))
            {
                properties = new HashSet<string>(System.StringComparer.Ordinal);
                ignoreMap[destinationType] = properties;
            }

            properties.Add(propertyName);
        }

        private static bool TryExtractNameOfProperty(ExpressionSyntax expression, out string propertyName)
        {
            propertyName = string.Empty;

            if (expression is not InvocationExpressionSyntax invocation)
                return false;

            if (invocation.Expression is not IdentifierNameSyntax identifier)
                return false;

            if (!identifier.IsKind(SyntaxKind.IdentifierName) || identifier.Identifier.ValueText != "nameof")
                return false;

            if (invocation.ArgumentList.Arguments.Count != 1)
                return false;

            return TryExtractPropertyName(invocation.ArgumentList.Arguments[0].Expression, out propertyName);
        }

        private static bool TryExtractPropertyName(ExpressionSyntax expression, out string propertyName)
        {
            propertyName = string.Empty;

            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                propertyName = memberAccess.Name.Identifier.ValueText;
                return propertyName.Length > 0;
            }

            if (expression is IdentifierNameSyntax identifier)
            {
                propertyName = identifier.Identifier.ValueText;
                return propertyName.Length > 0;
            }

            return false;
        }

        private static string GetRightmostName(NameSyntax nameSyntax)
        {
            return nameSyntax switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                QualifiedNameSyntax qualified => GetRightmostName(qualified.Right),
                AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
                GenericNameSyntax generic => generic.Identifier.ValueText,
                _ => nameSyntax.ToString()
            };
        }
    }
}