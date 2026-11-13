using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenWC3.Game;

public sealed class War3Camera
{
    public const float ClassicProjectionAspect = 4f / 3f;

    sealed class FieldTransition
    {
        public float Start, Target, Duration, Elapsed;
        public bool RemoveWhenDone;
    }

    public sealed record Preset(
        float Aoa,
        float Fov,
        float Rotation,
        float RotationInsert,
        float RotationDelete,
        float Distance,
        float FarZ,
        float NearZ,
        float Height);

    public sealed record Rates(
        float Aoa,
        float Fov,
        float Rotation,
        float Distance,
        float Forward,
        float Strafe);

    static readonly Preset[] FallbackPresets =
    {
        new(304f, 70f, 90f, 20f, 160f, 1650f, 5000f, 100f, 0f),
        new(311f, 70f, 90f, 20f, 160f, 1600f, 5000f, 100f, 0f),
        new(318f, 70f, 90f, 20f, 160f, 1500f, 5000f, 100f, 0f),
        new(325f, 70f, 90f, 20f, 160f, 1400f, 5000f, 100f, 0f),
        new(332f, 70f, 90f, 20f, 160f, 1275f, 5000f, 100f, 0f),
        new(339f, 70f, 90f, 20f, 160f, 1100f, 5000f, 100f, 0f),
    };

    const float PlatformGodotFov = 54.43f;

    readonly Func<float, float, float> _terrainHeight;

    public Preset[] Presets { get; }
    public Rates CameraRates { get; }
    public int CurrentPreset { get; private set; }
    public Vector3 Target { get; private set; }
    public Vector3 Position { get; private set; }
    public Vector3 ViewTarget => _smoothedTarget + ToWorldOffset(_targetNoise.Offset);
    public float FieldOfView { get; private set; }
    public float NearZ { get; private set; }
    public float FarZ { get; private set; }
    public float ProjectionNearZ => HasScriptedField(7) ? NearZ : NearZ * GameplayProjectionScale;
    public float ProjectionFarZ => MathF.Max(ProjectionNearZ + 1f,
        FarZ * (HasScriptedField(1) ? 1f : GameplayProjectionScale));
    public float HorizontalAngle => _horizontalAngle;
    public Vector4 Bounds { get; set; } = new(-1e9f, -1e9f, 1e9f, 1e9f);
    public Vector4 Margins { get; }
    public Vector3 ShakeOffset { get; private set; }
    public float SmoothingFactor
    {
        get => _smoothingFactor;
        set => _smoothingFactor = float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 10f);
    }

    public Camera3D Camera { get; }

    readonly Dictionary<int, FieldTransition> _fieldTransitions = new();
    readonly CameraNoise _sourceNoise = new();
    readonly CameraNoise _targetNoise = new();
    float _horizontalAngle;
    float _verticalAngle;
    float _rollAngle;
    float _distance;
    float _zOffset;
    float _smoothingFactor;
    float _shakeMagnitude, _shakeRemaining, _shakeDuration, _shakeElapsed;
    Vector3 _smoothedTarget;
    Vector2 _panStart, _panTarget;
    float _panDuration, _panElapsed;
    bool _panning;
    bool _insertDown;
    bool _deleteDown;
    Unit _targetController;
    Vector3? _presentationControllerPosition;
    Vector2 _targetControllerOffset;
    bool _inheritOrientation;
    Unit _orientController;
    Vector2 _orientControllerOffset;
    Vector3 _orientEye;
    Unit _softFollowController;
    Vector3? _softFollowPresentation;
    Vector3 _softFollowGlideFrom;
    float _softFollowGlideElapsed;
    float _softFollowGlideDuration;
    bool _softFollowGliding;

    public War3Camera(Node parent, Func<float, float, float> terrainHeight)
    {
        _terrainHeight = terrainHeight ?? ((_, _) => 0f);
        LoadSettings(out Preset[] presets, out Rates rates, out Vector4 margins);
        Presets = presets;
        CameraRates = rates;
        Margins = margins;
        Camera = new Camera3D
        {
            Name = "War3Camera",
            Current = true,
        };
        parent.AddChild(Camera);
        SnapToPreset();
        GD.Print($"[CAMERA] 预设距离={string.Join('/', Array.ConvertAll(Presets,
            preset => preset.Distance.ToString("0", CultureInfo.InvariantCulture)))}, "
            + $"AOA={string.Join('/', Array.ConvertAll(Presets,
                preset => preset.Aoa.ToString("0", CultureInfo.InvariantCulture)))}, "
            + $"速率={CameraRates.Forward:0}/{CameraRates.Strafe:0}, "
            + $"边距={Margins.X:0}/{Margins.Y:0}/{Margins.Z:0}/{Margins.W:0}");
    }

    bool HasScriptedField(int field) => _fieldTransitions.TryGetValue(field, out var transition)
        && !transition.RemoveWhenDone;

    float GameplayProjectionScale => HasScriptedField(0)
        ? 1f : MathF.Max(1f, _distance / MathF.Max(1f, Presets[CurrentPreset].Distance));

    public static void LoadSettings(out Preset[] presets, out Rates rates, out Vector4 margins)
    {
        float[] aoa = ReadArray("Camera.AOA");
        float[] fov = ReadArray("Camera.FOV");
        float[] rotation = ReadArray("Camera.Rotation");
        float[] distance = ReadArray("Camera.Distance");
        float[] farZ = ReadArray("Camera.FarZ");
        float[] nearZ = ReadArray("Camera.NearZ");
        float[] height = ReadArray("Camera.Height");
        presets = new Preset[6];
        for (int i = 0; i < presets.Length; i++)
        {
            Preset fallback = FallbackPresets[i];
            presets[i] = new Preset(
                ValueAt(aoa, i, fallback.Aoa),
                ValueAt(fov, i, fallback.Fov),
                ValueAt(rotation, i, fallback.Rotation),
                ValueAt(rotation, i + 6, fallback.RotationInsert),
                ValueAt(rotation, i + 12, fallback.RotationDelete),
                MathF.Max(1f, ValueAt(distance, i, fallback.Distance)),
                ValueAt(farZ, i, fallback.FarZ),
                ValueAt(nearZ, i, fallback.NearZ),
                ValueAt(height, i, fallback.Height));
        }

        rates = new Rates(
            ReadFloat("CameraRates.AOA", 20f),
            ReadFloat("CameraRates.FOV", 20f),
            ReadFloat("CameraRates.Rotation", 30f) * 3f,
            ReadFloat("CameraRates.Distance", 1200f),
            ReadFloat("CameraRates.Forward", 3000f),
            ReadFloat("CameraRates.Strafe", 3000f));
        margins = new Vector4(
            MathF.Max(0f, ReadFloat("CameraMargins.left", 4f)) * 128f,
            MathF.Max(0f, ReadFloat("CameraMargins.right", 4f)) * 128f,
            MathF.Max(0f, ReadFloat("CameraMargins.top", 2f)) * 128f,
            MathF.Max(0f, ReadFloat("CameraMargins.bottom", 2f)) * 128f);
    }

    public void SetBounds(float minX, float minZ, float maxX, float maxZ)
    {
        Bounds = new Vector4(MathF.Min(minX, maxX), MathF.Min(minZ, maxZ),
            MathF.Max(minX, maxX), MathF.Max(minZ, maxZ));
        ClampTarget();
    }

    public void Focus(Vector3 point)
    {
        SetTarget(point.X, point.Z);
    }

    public void SetTarget(float x, float z)
    {
        _panning = false;
        _targetController = null;
        _orientController = null;
        _softFollowController = null;
        _inheritOrientation = false;
        Target = new Vector3(x, Target.Y, z);
        ClampTarget();
        UpdateTargetHeight();
        _smoothedTarget = Target;
        UpdatePose();
    }

    public void PanTo(float x, float z, float duration)
    {
        _panning = false;
        _targetController = null;
        _orientController = null;
        _inheritOrientation = false;
        float seconds = ScriptDuration(duration);
        if (seconds <= 0f)
        {
            SetTarget(x, z);
            return;
        }
        _panStart = new Vector2(Target.X, Target.Z);
        _panTarget = new Vector2(x, z);
        _panDuration = seconds;
        _panElapsed = 0f;
        _panning = true;
    }

    public void Stop()
    {
        _panning = false;
        foreach ((int field, FieldTransition transition) in _fieldTransitions)
        {
            transition.Start = transition.Target = CurrentFieldValue(field, transition);
            transition.Duration = transition.Elapsed = 0f;
            transition.RemoveWhenDone = false;
        }
    }

    public void SetTargetController(Unit unit, float xOffset, float zOffset,
        bool inheritOrientation = false)
    {
        if (unit == null || !unit.Alive) return;
        _panning = false;
        _softFollowController = null;
        _orientController = null;
        _targetController = unit;
        _presentationControllerPosition = null;
        _targetControllerOffset = new Vector2(xOffset, zOffset);
        _inheritOrientation = inheritOrientation;
    }

    public void ClearTargetController()
    {
        _targetController = null;
        _presentationControllerPosition = null;
        _inheritOrientation = false;
    }

    public void SetOrientController(Unit unit, float xOffset, float zOffset)
    {
        if (unit == null || !unit.Alive) return;
        _panning = false;
        _targetController = null;
        _inheritOrientation = false;
        _orientController = unit;
        _orientControllerOffset = new Vector2(xOffset, zOffset);
        _orientEye = Position - ToWorldOffset(ShakeOffset)
            - ToWorldOffset(_sourceNoise.Offset);
    }

    public void ClearOrientController() => _orientController = null;

    public bool HasSoftFollowController => _softFollowController != null;

    public void SetSoftFollowController(Unit unit, Vector3? presentationPosition = null)
    {
        if (unit == null || !unit.Alive)
        {
            ClearSoftFollowController();
            return;
        }
        bool retarget = !ReferenceEquals(_softFollowController, unit);
        _softFollowController = unit;
        _softFollowPresentation = presentationPosition;
        if (!retarget) return;
        _softFollowGlideFrom = _smoothedTarget;
        _softFollowGlideElapsed = 0f;
        Vector3 goal = presentationPosition ?? unit.Position;
        float glideDistance = new Vector2(_softFollowGlideFrom.X - goal.X,
            _softFollowGlideFrom.Z - goal.Z).Length();
        _softFollowGlideDuration = Math.Clamp(glideDistance / 3000f, 0.3f, 0.75f);
        _softFollowGliding = true;
    }

    public void ClearSoftFollowController()
    {
        _softFollowController = null;
        _softFollowPresentation = null;
        _softFollowGliding = false;
    }

    public void SetRotationKeys(bool insertDown, bool deleteDown)
    {
        _insertDown = insertDown;
        _deleteDown = deleteDown;
    }

    public void SetPreset(int preset)
    {
        int next = Math.Clamp(preset, 0, Presets.Length - 1);
        if (next == CurrentPreset) return;
        CurrentPreset = next;
        SnapToPreset();
    }

    public void Scroll(float amount)
    {
        if (amount == 0f || !float.IsFinite(amount)) return;
        int next = Math.Clamp(CurrentPreset + Math.Sign(amount), 0, Presets.Length - 1);
        if (next == CurrentPreset) return;
        CurrentPreset = next;
        RestorePresetFields(1f);
    }

    public void Pan(float deltaX, float deltaZ)
    {
        if (_targetController != null || _orientController != null
            || _softFollowController != null) return;
        if (deltaX != 0f || deltaZ != 0f) _panning = false;
        Target += new Vector3(deltaX, 0f, deltaZ);
        ClampTarget();
    }

    public void PanScreen(float deltaX, float deltaY, float viewportHeight)
    {
        if (viewportHeight <= 0f || (deltaX == 0f && deltaY == 0f)) return;
        float worldPerPixel = Presets[CurrentPreset].Distance / viewportHeight;
        var screenRight = new Vector2(MathF.Cos(_horizontalAngle), MathF.Sin(_horizontalAngle));
        var screenUp = new Vector2(-screenRight.Y, screenRight.X);
        Vector2 worldDelta = screenRight * (-deltaX * worldPerPixel)
            + screenUp * (deltaY * worldPerPixel);
        Pan(worldDelta.X, worldDelta.Y);
    }

    public void SetField(int field, float value, float duration)
    {
        if (field is < 0 or > MaxScriptCameraField || !float.IsFinite(value)) return;
        float current = GetField(field);
        _fieldTransitions[field] = new FieldTransition
        {
            Start = current,
            Target = value,
            Duration = ScriptDuration(duration),
            Elapsed = 0f,
        };
    }

    public float GetField(int field)
    {
        if (_fieldTransitions.TryGetValue(field, out FieldTransition transition))
            return CurrentFieldValue(field, transition);
        Preset preset = Presets[CurrentPreset];
        return field switch
        {
            0 => _distance,
            1 => FarZ,
            2 => RadiansToDegrees(_verticalAngle) + 270f,
            3 => RadiansToDegrees(FieldOfView) * 2f,
            4 => RadiansToDegrees(_rollAngle),
            5 => RadiansToDegrees(_horizontalAngle) + 90f,
            6 => _zOffset,
            7 => NearZ,
            8 => _localPitch,
            9 => RadiansToDegrees(_localYaw),
            _ => 0f,
        };
    }

    public void ResetToGameCamera(float duration)
    {
        SetNoise(false, 0f, 0f, false);
        SetNoise(true, 0f, 0f, false);
        ClearTargetController();
        ClearOrientController();
        ClearSoftFollowController();
        RestorePresetFields(duration);
    }

    public void SetNoise(bool target, float magnitude, float velocity, bool verticalOnly)
    {
        CameraNoise noise = target ? _targetNoise : _sourceNoise;
        float usable = float.IsFinite(magnitude) ? MathF.Max(0f, magnitude) : 0f;
        noise.Magnitude = usable < NoiseMagnitudeFloor ? 0f : usable;
        noise.Velocity = float.IsFinite(velocity) ? MathF.Max(0f, velocity) : 0f;
        noise.VerticalOnly = verticalOnly;
        noise.Phase = 0f;
        noise.Offset = new Vector3();
    }

    public void SetShake(float magnitude, float duration)
    {
        _shakeMagnitude = MathF.Max(0f, magnitude);
        _shakeDuration = MathF.Max(0.0001f, duration);
        _shakeRemaining = MathF.Max(0f, duration);
        _shakeElapsed = 0f;
    }

    public void Update(float deltaTime, bool up, bool down, bool left, bool right)
    {
        if (_targetController != null && !_targetController.Alive) ClearTargetController();
        if (_orientController != null && !_orientController.Alive) ClearOrientController();
        if (_softFollowController != null && !_softFollowController.Alive)
            ClearSoftFollowController();

        float velocityZ = up == down ? 0f : (up ? CameraRates.Forward : -CameraRates.Forward);
        float velocityX = left == right ? 0f : (right ? CameraRates.Strafe : -CameraRates.Strafe);
        bool scriptedFollow = _targetController != null || _orientController != null;
        if ((velocityX != 0f || velocityZ != 0f) && !scriptedFollow
            && _softFollowController == null)
        {
            Vector2 screenRight = new(MathF.Cos(_horizontalAngle), -MathF.Sin(_horizontalAngle));
            Vector2 screenUp = new(-MathF.Sin(_horizontalAngle), -MathF.Cos(_horizontalAngle));
            Vector2 world = screenRight * velocityX + screenUp * velocityZ;
            Pan(world.X * deltaTime, world.Y * deltaTime);
        }
        else if (scriptedFollow)
        {
            Unit controlled = _targetController ?? _orientController;
            Vector2 offset = _targetController != null
                ? _targetControllerOffset : _orientControllerOffset;
            Vector3? presentation = _targetController != null
                ? _presentationControllerPosition : null;
            Vector3 sample = presentation ?? controlled.Position;
            Target = new Vector3(sample.X, Target.Y, sample.Z);
            ClampTarget();
            Target += new Vector3(offset.X, 0f, offset.Y);
        }
        else if (_panning)
        {
            _panElapsed = Math.Min(_panDuration, _panElapsed + Math.Max(0f, deltaTime));
            float ratio = _panDuration <= 0f ? 1f : _panElapsed / _panDuration;
            Vector2 value = _panStart.Lerp(_panTarget, ratio);
            Target = new Vector3(value.X, Target.Y, value.Y);
            ClampTarget();
            if (ratio >= 1f) _panning = false;
        }
        else if (_softFollowController != null)
        {
            Vector3 desired = _softFollowPresentation ?? _softFollowController.Position;
            if (_softFollowGliding)
            {
                _softFollowGlideElapsed = Math.Min(_softFollowGlideDuration,
                    _softFollowGlideElapsed + deltaTime);
                float ratio = _softFollowGlideDuration <= 0f
                    ? 1f : _softFollowGlideElapsed / _softFollowGlideDuration;
                float ease = ratio * ratio * (3f - 2f * ratio);
                Target = new Vector3(
                    _softFollowGlideFrom.X + (desired.X - _softFollowGlideFrom.X) * ease,
                    Target.Y,
                    _softFollowGlideFrom.Z + (desired.Z - _softFollowGlideFrom.Z) * ease);
                if (ratio >= 1f) _softFollowGliding = false;
            }
            else
            {
                Target = new Vector3(desired.X, Target.Y, desired.Z);
            }
            ClampTarget();
        }

        Preset preset = Presets[CurrentPreset];
        float presetRotation = _insertDown == _deleteDown
            ? preset.Rotation
            : (_insertDown ? preset.RotationInsert : preset.RotationDelete);
        bool rotationTransition = _fieldTransitions.ContainsKey(5);
        bool aoaTransition = _fieldTransitions.ContainsKey(2);
        bool distanceTransition = _fieldTransitions.ContainsKey(0);
        bool fovTransition = _fieldTransitions.ContainsKey(3);
        float rotation = ResolveField(5, presetRotation, deltaTime);
        _rollAngle = DegreesToRadians(ResolveField(4, 0f, deltaTime));
        float aoa = ResolveField(2, preset.Aoa, deltaTime);
        float distance = MathF.Max(1f, ResolveField(0, preset.Distance, deltaTime));
        float fov = ResolveField(3, preset.Fov, deltaTime);
        float nearZ = ResolveField(7, preset.NearZ, deltaTime);
        float farZ = ResolveField(1, preset.FarZ, deltaTime);
        _zOffset = ResolveField(6, 0f, deltaTime);
        _localPitch = ResolveField(8, _localPitch, deltaTime);
        _localYaw = DegreesToRadians(ResolveField(9,
            RadiansToDegrees(_localYaw), deltaTime));
        float targetHorizontal = DegreesToRadians(rotation - 90f);
        float targetVertical = DegreesToRadians(aoa - 270f);
        _horizontalAngle = rotationTransition
            ? targetHorizontal
            : ApplyAtRateAngle(_horizontalAngle, targetHorizontal,
                DegreesToRadians(CameraRates.Rotation), deltaTime);
        _verticalAngle = aoaTransition
            ? targetVertical
            : ApplyAtRateAngle(_verticalAngle, targetVertical,
                DegreesToRadians(CameraRates.Aoa), deltaTime);
        _distance = distanceTransition
            ? distance
            : ApplyAtRate(_distance, distance, CameraRates.Distance, deltaTime);
        float targetFov = DegreesToRadians(fov * 0.5f);
        FieldOfView = fovTransition
            ? targetFov
            : ApplyAtRate(FieldOfView, targetFov, DegreesToRadians(CameraRates.Fov), deltaTime);
        NearZ = MathF.Max(0.1f, nearZ);
        FarZ = MathF.Max(NearZ + 1f, farZ);

        if (!scriptedFollow) ClampTarget();
        if (scriptedFollow)
        {
            Unit controlled = _targetController ?? _orientController;
            Vector3 sample = (_targetController != null ? _presentationControllerPosition : null)
                ?? controlled.Position;
            Target = new Vector3(Target.X, sample.Y + _zOffset, Target.Z);
        }
        else UpdateTargetHeight();
        bool presentationReady = (_targetController != null
                && _presentationControllerPosition.HasValue)
            || (_softFollowController != null && _softFollowPresentation.HasValue);
        _smoothedTarget = presentationReady && SmoothingFactor <= 0f
            ? Target
            : SmoothTarget(_smoothedTarget, Target, SmoothingFactor, deltaTime);
        UpdateShake(deltaTime);
        _sourceNoise.Step(deltaTime);
        _targetNoise.Step(deltaTime);
        UpdatePose();
    }

    void SnapToPreset()
    {
        Preset preset = Presets[CurrentPreset];
        _horizontalAngle = DegreesToRadians(preset.Rotation - 90f);
        _verticalAngle = DegreesToRadians(preset.Aoa - 270f);
        _rollAngle = 0f;
        _distance = preset.Distance;
        _zOffset = 0f;
        FieldOfView = DegreesToRadians(preset.Fov * 0.5f);
        NearZ = MathF.Max(0.1f, preset.NearZ);
        FarZ = MathF.Max(NearZ + 1f, preset.FarZ);
        _smoothedTarget = Target;
        UpdateTargetHeight();
        UpdatePose();
    }

    void RestorePresetFields(float duration)
    {
        Preset preset = Presets[CurrentPreset];
        ResetField(0, preset.Distance, duration);
        ResetField(1, preset.FarZ, duration);
        ResetField(2, preset.Aoa, duration);
        ResetField(3, preset.Fov, duration);
        ResetField(4, 0f, duration);
        ResetField(5, preset.Rotation, duration);
        ResetField(6, 0f, duration);
        ResetField(7, preset.NearZ, duration);
    }

    void ResetField(int field, float target, float duration)
    {
        if (ScriptDuration(duration) <= 0f)
        {
            _fieldTransitions.Remove(field);
            return;
        }
        SetField(field, target, duration);
        _fieldTransitions[field].RemoveWhenDone = true;
    }

    float ResolveField(int field, float fallback, float deltaTime)
    {
        if (!_fieldTransitions.TryGetValue(field, out FieldTransition transition))
            return fallback;
        transition.Elapsed = Math.Min(transition.Duration,
            transition.Elapsed + Math.Max(0f, deltaTime));
        float value = CurrentFieldValue(field, transition);
        if (transition.Elapsed >= transition.Duration && transition.RemoveWhenDone)
            _fieldTransitions.Remove(field);
        return value;
    }

    static float CurrentFieldValue(int field, FieldTransition transition)
    {
        if (transition.Duration <= 0f) return transition.Target;
        float ratio = Math.Clamp(transition.Elapsed / transition.Duration, 0f, 1f);
        return InterpolateField(field, transition.Start, transition.Target, ratio);
    }

    static float InterpolateField(int field, float start, float target, float ratio)
    {
        ratio = Math.Clamp(ratio, 0f, 1f);
        float delta = field is 2 or 4 or 5 or 9
            ? MathF.IEEERemainder(target - start, 360f)
            : target - start;
        return start + delta * ratio;
    }

    void ClampTarget()
    {
        if (Bounds.Z <= Bounds.X || Bounds.W <= Bounds.Y) return;
        Target = new Vector3(
            ClampTargetAxis(Target.X, Bounds.X, Bounds.Z, Margins.X, Margins.Y),
            Target.Y,
            ClampTargetAxis(Target.Z, Bounds.Y, Bounds.W, Margins.W, Margins.Z));
    }

    static float ClampTargetAxis(float value, float min, float max,
        float negativeMargin, float positiveMargin)
        => Math.Clamp(value, min - MathF.Max(0f, negativeMargin),
            max + MathF.Max(0f, positiveMargin));

    void UpdateTargetHeight()
    {
        float ground = _terrainHeight(Target.X, Target.Z);
        if (!float.IsFinite(ground)) return;
        Target = new Vector3(Target.X, ground + Presets[CurrentPreset].Height + _zOffset,
            Target.Z);
    }

    void UpdateShake(float deltaTime)
    {
        if (_shakeRemaining <= 0f)
        {
            ShakeOffset = new Vector3();
            return;
        }
        _shakeRemaining -= Math.Max(0f, deltaTime);
        _shakeElapsed += Math.Max(0f, deltaTime);
        float decay = Math.Clamp(_shakeRemaining / _shakeDuration, 0f, 1f);
        float t = _shakeElapsed;
        ShakeOffset = new Vector3(MathF.Sin(t * 37f), MathF.Sin(t * 41f),
            MathF.Sin(t * 29f)) * (_shakeMagnitude * decay);
        if (_shakeRemaining <= 0f) ShakeOffset = new Vector3();
    }

    void UpdatePose()
    {
        float sinVertical = MathF.Sin(_verticalAngle);
        float horizontal = _horizontalAngle
            + (_inheritOrientation && _targetController != null ? _targetController.Facing : 0f);
        var clientOffset = new Vector3(
            _distance * sinVertical * MathF.Sin(horizontal),
            -_distance * sinVertical * MathF.Cos(horizontal),
            _distance * MathF.Cos(_verticalAngle));
        Vector3 offset = ToWorldOffset(clientOffset);
        Vector3 eye = (_orientController != null ? _orientEye : _smoothedTarget + offset)
            + ToWorldOffset(ShakeOffset) + ToWorldOffset(_sourceNoise.Offset);
        float ground = _terrainHeight(eye.X, eye.Z);
        if (_orientController == null && float.IsFinite(ground)
            && eye.Y < ground + TerrainCollisionClearance)
            eye.Y = ground + TerrainCollisionClearance;
        Position = eye;

        Vector3 look = ViewTarget;
        Vector3 direction = look - Position;
        Vector3 up = direction.X * direction.X + direction.Z * direction.Z < 1e-6f
            ? Vector3.Back : Vector3.Up;
        if (direction.LengthSquared() < 1e-8f) direction = Vector3.Forward;
        Camera.LookAtFromPosition(Position, Position + direction, up);
        if (MathF.Abs(_rollAngle) > 1e-6f)
            Camera.RotateObjectLocal(Vector3.Back, _rollAngle);
        Camera.Fov = RadiansToDegrees(FieldOfView)
            * (PlatformGodotFov / (FallbackPresets[0].Fov * 0.5f));
        Camera.Near = MathF.Max(0.1f, ProjectionNearZ);
        Camera.Far = MathF.Max(Camera.Near + 1f, ProjectionFarZ);
    }

    static Vector3 ToWorldOffset(Vector3 client)
        => new(client.X, client.Z, -client.Y);

    internal static Vector3 SmoothTarget(Vector3 previous, Vector3 target, float factor,
        float deltaTime)
    {
        if (deltaTime <= 0f) return factor <= 0f ? target : previous;
        float distance = previous.DistanceTo(target);
        if (distance > 1000f) return target;
        float frameAdjustment = MathF.Pow(Math.Min(1f, 10f * deltaTime), 1.5f);
        float blend = Math.Min(1f, MathF.Pow(distance / 1000f, factor) * 0.8f
            + 0.15f + frameAdjustment);
        return target * blend + previous * (1f - blend);
    }

    static float ApplyAtRate(float oldValue, float newValue, float rate, float deltaTime)
    {
        float maxDelta = rate * deltaTime;
        float delta = newValue - oldValue;
        return MathF.Abs(delta) <= maxDelta ? newValue
            : oldValue + MathF.CopySign(maxDelta, delta);
    }

    static float ApplyAtRateAngle(float oldValue, float newValue, float rate, float deltaTime)
    {
        const float twoPi = MathF.PI * 2f;
        float delta = MathF.IEEERemainder(newValue - oldValue, twoPi);
        float maxDelta = rate * deltaTime;
        if (MathF.Abs(delta) <= maxDelta) return newValue;
        return MathF.IEEERemainder(oldValue + MathF.CopySign(maxDelta, delta), twoPi);
    }

    const int MaxScriptCameraField = 9;
    const float ImmediateDurationEpsilon = 0.001f;
    const float NoiseMagnitudeFloor = 2.0000000233721948e-07f;
    const float TerrainCollisionClearance = 32f;
    float _localPitch;
    float _localYaw;

    static float ScriptDuration(float duration)
    {
        float magnitude = MathF.Abs(duration);
        if (!float.IsFinite(magnitude)) return 0f;
        return magnitude < ImmediateDurationEpsilon ? 0f : magnitude;
    }

    static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);
    static float RadiansToDegrees(float radians) => radians * (180f / MathF.PI);

    static float ReadFloat(string key, float fallback)
    {
        if (!GeneratedTexts.TryGet("UIMiscData", key, out string value)) return fallback;
        int comma = value.IndexOf(',');
        if (comma >= 0) value = value[..comma];
        return float.TryParse(value.Trim(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out float parsed)
            && float.IsFinite(parsed) ? parsed : fallback;
    }

    static float[] ReadArray(string key)
    {
        if (!GeneratedTexts.TryGet("UIMiscData", key, out string value))
            return Array.Empty<float>();
        string[] parts = value.Split(',');
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = float.TryParse(parts[i].Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out float parsed) ? parsed : float.NaN;
        return result;
    }

    static float ValueAt(float[] values, int index, float fallback)
        => index >= 0 && index < values.Length && float.IsFinite(values[index])
            ? values[index] : fallback;

    sealed class CameraNoise
    {
        public float Magnitude, Velocity, Phase;
        public bool VerticalOnly;
        public Vector3 Offset;

        public void Step(float deltaTime)
        {
            if (Magnitude <= 0f)
            {
                Offset = new Vector3();
                return;
            }
            Phase = (Phase + Math.Max(0f, deltaTime) * Velocity / Math.Max(1f, Magnitude))
                % (2f * MathF.PI);
            Offset = new Vector3(
                VerticalOnly ? 0f : MathF.Sin(Phase),
                VerticalOnly ? 0f : MathF.Sin(Phase * 2f),
                MathF.Sin(Phase * 3f)) * Magnitude;
        }
    }
}