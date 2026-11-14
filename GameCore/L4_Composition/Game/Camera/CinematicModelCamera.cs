using Godot;
using System;

namespace OpenWC3.Game;

internal sealed class CinematicModelCamera
{
    private readonly Node3D _eye;
    private readonly Node3D _target;
    private readonly AnimationPlayer _player;
    private string _sequence;
    private double _sequenceLength;
    private double _elapsed;
    private bool _prepared;

    public CinematicModelCamera(Node3D modelRoot)
    {
        _eye = modelRoot.FindChild("camera0", true, false) as Node3D;
        _target = modelRoot.FindChild("camera0_target", true, false) as Node3D;
        _player = modelRoot.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        if (modelRoot.FindChild("AnimationTree", true, false) is AnimationTree tree) tree.Active = false;
        if (modelRoot.GetNodeOrNull("Model") is Node3D model && !model.IsAncestorOf(_eye)) model.Free();
    }

    public bool Valid => _eye != null && _target != null;

    public (Vector3 Eye, Vector3 Target, float Fov, float Near, float Far) Step(float seconds)
    {
        if (!Prepare()) return Pose();
        _elapsed += Math.Max(0f, seconds);
        if (_sequenceLength > 0d) _player.Seek(Math.Min(_elapsed, _sequenceLength), true);
        return Pose();
    }

    private (Vector3 Eye, Vector3 Target, float Fov, float Near, float Far) Pose()
    {
        var camera = _eye as Camera3D;
        return (_eye.GlobalPosition, _target.GlobalPosition,
            camera?.Fov ?? 70f, camera?.Near ?? 100f, camera?.Far ?? 5000f);
    }

    private bool Prepare()
    {
        if (_prepared) return true;
        if (_player == null || !_player.IsInsideTree()) return false;
        _prepared = true;

        string[] names = _player.GetAnimationList();
        if (names.Length == 0) return false;
        _sequence = Array.Find(names, name => name.Equals("Stand", StringComparison.OrdinalIgnoreCase))
            ?? names[0];
        _sequenceLength = _player.GetAnimation(_sequence)?.Length ?? 0d;
        _player.Play(_sequence);
        _player.Pause();
        return true;
    }
}
