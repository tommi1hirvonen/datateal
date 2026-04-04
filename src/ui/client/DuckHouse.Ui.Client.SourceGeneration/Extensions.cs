using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace DuckHouse.Ui.Client.SourceGeneration;

internal static class Extensions
{
    public static string GetPropertyNameFromIconName(this string iconName)
    {
        var segments = iconName.Split('-', '_');
        var segmentsPascalCase = segments.Select(segment => segment.Substring(0, 1).ToUpper() + segment.Substring(1));
        var propertyName = string.Join("", segmentsPascalCase);
        if (!SyntaxFacts.IsValidIdentifier(propertyName))
        {
            // e.g. "123" => "_123"
            propertyName = "_" + propertyName;
        }
        return propertyName;
    }

    public static string GetNamespace(this BaseTypeDeclarationSyntax syntax)
    {
        var @namespace = string.Empty;
        var potentialNamespaceParent = syntax.Parent;
        while (potentialNamespaceParent != null
               && potentialNamespaceParent is not NamespaceDeclarationSyntax
               && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        if (potentialNamespaceParent is not BaseNamespaceDeclarationSyntax namespaceParent)
        {
            return @namespace;
        }
        
        @namespace = namespaceParent.Name.ToString();
        while (true)
        {
            if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent)
            {
                break;
            }

            @namespace = $"{namespaceParent.Name}.{@namespace}";
            namespaceParent = parent;
        }

        return @namespace;
    }
}