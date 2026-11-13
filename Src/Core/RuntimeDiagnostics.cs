using System;
using Godot;

namespace OpenWC3;

public static class RuntimeDiagnostics
{
    public static void WriteLine(string message) => GD.Print(message);

    public static void WriteWarning(string message) => GD.PushWarning(message);

    public static void WriteError(string message) => GD.PushError(message);

    public static void WriteException(string context, Exception error)
        => GD.PushError($"{context}: {error?.GetType().Name} {error?.Message}");
}
