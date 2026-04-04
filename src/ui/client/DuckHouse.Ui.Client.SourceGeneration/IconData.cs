using System;
using System.Collections.Generic;
using System.IO;

namespace DuckHouse.Ui.Client.SourceGeneration;

internal class IconData(string iconPath, string? iconText) : IEquatable<IconData?>
{
    public string IconPath { get; } = iconPath;

    public string IconName { get; } = Path.GetFileNameWithoutExtension(iconPath);

    public string? IconText { get; } = iconText;

    public override bool Equals(object? obj)
    {
        return obj is IconData other &&
               IconPath == other.IconPath &&
               IconName == other.IconName &&
               IconText == other.IconText;
    }

    public bool Equals(IconData? other)
    {
        return other is not null &&
               IconPath == other.IconPath &&
               IconName == other.IconName &&
               IconText == other.IconText;
    }

    public override int GetHashCode()
    {
        var hashCode = -445792199;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(IconPath);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(IconName);
        hashCode = hashCode * -1521134295 + EqualityComparer<string?>.Default.GetHashCode(IconText);
        return hashCode;
    }
}