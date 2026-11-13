namespace OpenWC3;

public static class RandomExtensions
{
    /// <summary>客户端 Random.NextUnit：返回 [0,1) 单精度随机数。</summary>
    public static float NextUnit(this System.Random rng) => (float)rng.NextDouble();

    public static int NextRange(this System.Random rng, int minInclusive, int maxExclusive)
        => minInclusive >= maxExclusive ? minInclusive : rng.Next(minInclusive, maxExclusive);
}

/// <summary>客户端脚本随机流的等价物：每个 stream 号一条独立序列，Launcher 侧共用同一随机源。</summary>
public readonly struct RandomStream(Random source, int stream)
{
    public float NextUnit() => (float)source.NextDouble();
    public int Number => stream;
}

public static class RandomStreamExtensions
{
    public static RandomStream Stream(this Random random, int stream) => new(random, stream);
}
