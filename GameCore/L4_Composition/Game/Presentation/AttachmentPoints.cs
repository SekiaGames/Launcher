using Godot;
using System;
using System.Text;

namespace Launcher;

public static class AttachmentPoints
{
    private const string Prefix = "attach_";

    public static Node3D Find(Node3D modelRoot, string point)
    {
        if (modelRoot == null || string.IsNullOrWhiteSpace(point)) return null;

        string requested = Normalize(point);
        bool prefixed = requested.StartsWith("attach", StringComparison.Ordinal);
        if (prefixed) requested = requested[6..];

        Node3D partial = null;
        foreach (Node node in modelRoot.FindChildren(Prefix + "*", "", true, false))
        {
            if (node is not Node3D attach) continue;
            if (attach.Name.ToString().StartsWith("attach_node", StringComparison.Ordinal)) continue;
            string candidate = Normalize(attach.Name.ToString());
            if (candidate.StartsWith("attach", StringComparison.Ordinal)) candidate = candidate[6..];

            if (string.Equals(candidate, requested, StringComparison.Ordinal))
            {
                return PointOf(attach);
            }
            if (partial == null && candidate.Contains(requested, StringComparison.Ordinal))
            {
                partial = attach;
            }
        }
        return PointOf(partial);
    }

    public static string Describe(Node3D modelRoot, string point)
    {
        Node3D found = Find(modelRoot, point);
        if (found == null) return "";
        return string.Equals(found.Name.ToString(), "Point", StringComparison.Ordinal)
            ? found.GetParent().Name.ToString()
            : found.Name.ToString();
    }

    private static Node3D PointOf(Node3D attach)
    {
        if (attach == null) return null;
        foreach (Node child in attach.GetChildren())
        {
            if (child is Node3D point && string.Equals(point.Name.ToString(), "Point", StringComparison.Ordinal))
            {
                return point;
            }
        }
        return attach;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }
}
