namespace OpenWC3;

public static class GameLogger
{
    public static bool Enabled { get; set; }

    public static bool DetailedWorldEnabled { get; set; }

    public static void Write(string message)
    {
        if (!Enabled || string.IsNullOrEmpty(message)) return;
        Godot.GD.Print(message);
    }
}
