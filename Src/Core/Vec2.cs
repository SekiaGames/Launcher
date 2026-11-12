namespace OpenWC3;

// 二维坐标 (地图单位)
public readonly struct Vec2 : IEquatable<Vec2>
{
    public readonly float X, Y;
    public Vec2(float x, float y) { X = x; Y = y; }
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);
    public float LengthSquared => X * X + Y * Y;
    public float Length => MathF.Sqrt(LengthSquared);
    public Vec2 Normalized { get { var l = Length; return l < 1e-5f ? new(0, 0) : new(X / l, Y / l); } }
    public static float Dist(Vec2 a, Vec2 b) => (a - b).Length;
    // Match ValueType/float equality, including equal NaNs and signed zeros.
    // Coordinate comparisons must not box and reflect over these two fields.
    public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X,Y);
    public override string ToString() => $"({X:0},{Y:0})";
}
