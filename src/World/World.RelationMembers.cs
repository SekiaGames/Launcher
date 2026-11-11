using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class World
{
    private bool IsAllied(int a, int b)
    {
        if (a == b) return true;
        return Allied != null && Allied(a, b);
    }
}
