namespace StandalonePathing.Core;

public static class NativeSoftFloat
{
    public static float Truncate(float value) => MathF.Truncate(value);

    public static float Add(float left, float right) => left + right;

    public static float Multiply(float left, float right) => left * right;
}
