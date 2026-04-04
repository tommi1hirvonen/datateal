using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;

namespace DuckHouse.Ui.Client.SourceGeneration;

internal class IconsClassData(
    string @namespace,
    string className,
    string? cssClass,
    string?[] pathSegments,
    bool incorrectModifiers,
    Location? location) : IEquatable<IconsClassData?>
{
    public string Namespace { get; } = @namespace;

    public string ClassName { get; } = className;

    public string? CssClass { get; } = cssClass;

    public string IconsPath { get; } = string.Join(Path.DirectorySeparatorChar.ToString(), pathSegments);

    public bool IncorrectModifiers { get; } = incorrectModifiers;

    public Location? Location { get; } = location;

    public override bool Equals(object? obj)
    {
        return obj is IconsClassData other &&
               Namespace == other.Namespace &&
               ClassName == other.ClassName &&
               IconsPath == other.IconsPath &&
               IncorrectModifiers == other.IncorrectModifiers;
    }

    public bool Equals(IconsClassData? other)
    {
        return other is not null &&
               Namespace == other.Namespace &&
               ClassName == other.ClassName &&
               IconsPath == other.IconsPath &&
               IncorrectModifiers == other.IncorrectModifiers;
    }

    public override int GetHashCode()
    {
        var hashCode = -1494284360;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Namespace);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ClassName);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(IconsPath);
        hashCode = hashCode * -1521134295 + IncorrectModifiers.GetHashCode();
        return hashCode;
    }
}