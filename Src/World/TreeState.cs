using Godot;

namespace OpenWC3;

public sealed class TreeState
{
    public Node3D Node;
    public Vector3 Position;
    public string Id;
    public float Hp = 50f;
    public bool Alive = true;

    public void Chop(float amount)
    {
        if (!Alive) return;
        Hp -= amount;
        if (Hp <= 0f)
        {
            Alive = false;
            if (Node != null) Node.Visible = false;
        }
    }
}