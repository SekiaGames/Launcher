using Godot;

namespace Launcher;

public sealed class TreeState
{
    public Node3D Node;
    public Node3D Shadow;
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
            if (Shadow != null) Shadow.Visible = false;
        }
    }
}