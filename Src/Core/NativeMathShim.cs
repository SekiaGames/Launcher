namespace StandalonePathing.Sim;

/// <summary>
/// 客户端原生浮点乘法（StandalonePathing 里的镜像实现）的最小替身。
/// A 侧只需要它的数值语义，等真正搬 StandalonePathing 时再删掉。
/// </summary>
public static class CantPathSearchPoint
{
    public static float NativeMultiply(float a, float b) => a * b;

    public static float NativeAdd(float a, float b) => a + b;

    public static float NativeSubtract(float a, float b) => a - b;

    public static float NativeDivide(float a, float b) => MathF.Abs(b) < 1e-9f ? 0f : a / b;
}
