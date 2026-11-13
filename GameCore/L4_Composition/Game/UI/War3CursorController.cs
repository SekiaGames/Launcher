using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    internal sealed class War3CursorController : IDisposable
    {
        private const float CursorScale = 1280f;
        private const float ViewportScale = 2f;
        private const float ConsoleBandRatio = 225.28f / 768f;

        private sealed class CursorEffect
        {
            public readonly SubViewport Viewport;
            public readonly CanvasLayer Layer;
            public readonly Node3D Model;
            public readonly AnimationPlayer Player;
            public readonly List<ShaderMaterial> Materials;

            public CursorEffect(SubViewport viewport, CanvasLayer layer, Node3D model,
                AnimationPlayer player, List<ShaderMaterial> materials)
            {
                Viewport = viewport;
                Layer = layer;
                Model = model;
                Player = player;
                Materials = materials;
            }
        }

        private enum CursorState { Normal, Target, Select, Move }

        private readonly War3GameScene _scene;
        private CursorEffect _effect;
        private CursorState _state = CursorState.Normal;
        private Color _tint = Colors.White;
        private string _animation;
        private float _moveFlash;
        private Vector2 _lastMouse;

        private War3CursorController(War3GameScene scene, CursorEffect effect)
        {
            _scene = scene;
            _effect = effect;
            ApplyTint(Colors.White);
            PlaySequence(CursorState.Normal);
            Input.MouseMode = Input.MouseModeEnum.Hidden;
        }

        public bool Active => _effect != null;

        public static War3CursorController TryCreate(War3GameScene scene, string race)
        {
            try
            {
                string path = PathResolve.ModelScene("UI/Cursor/" + race + "Cursor");
                PackedScene packed = path == null ? null : Assets.LoadScene(path);
                if (packed == null && race != "Human")
                {
                    path = PathResolve.ModelScene("UI/Cursor/HumanCursor");
                    packed = path == null ? null : Assets.LoadScene(path);
                }
                if (packed == null)
                {
                    GD.PushWarning("[CURSOR] 未找到种族光标模型");
                    return null;
                }

                var viewport = new SubViewport
                {
                    Name = "CursorViewport",
                    Size = new Vector2I(2048, 1536),
                    TransparentBg = true,
                    OwnWorld3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                };
                scene.AddChild(viewport);

                var camera = new Camera3D
                {
                    Name = "CursorCamera",
                    Projection = Camera3D.ProjectionType.Orthogonal,
                    Size = 1536f,
                    Near = 0.1f,
                    Far = 200f,
                    Current = true,
                };
                viewport.AddChild(camera);
                camera.LookAtFromPosition(new Vector3(1024f, 768f, 50f),
                    new Vector3(1024f, 768f, 0f), Vector3.Up);

                Node3D model = packed.Instantiate<Node3D>();
                model.Name = "CursorModel";
                model.Scale = Vector3.One * (CursorScale * ViewportScale);
                model.Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f);
                viewport.AddChild(model);

                if (model.FindChild("AnimationTree", true, false) is AnimationTree tree)
                    tree.Active = false;
                var player = model.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
                if (player == null)
                {
                    GD.PushWarning("[CURSOR] 光标模型缺少 AnimationPlayer");
                    viewport.QueueFree();
                    return null;
                }

                var materials = new List<ShaderMaterial>();
                CacheTintMaterials(model, materials);

                var layer = new CanvasLayer { Name = "CursorLayer", Layer = 120 };
                scene.AddChild(layer);
                var display = new TextureRect
                {
                    Name = "CursorDisplay",
                    Texture = viewport.GetTexture(),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                };
                layer.AddChild(display);
                display.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

                var effect = new CursorEffect(viewport, layer, model, player, materials);
                GD.Print($"[CURSOR] race={race} model={path}");
                return new War3CursorController(scene, effect);
            }
            catch (Exception error)
            {
                GD.PushWarning($"[CURSOR] 初始化失败: {error.Message}");
                return null;
            }
        }

        public void FlashMove()
        {
            if (_effect == null) return;
            _moveFlash = 0.4f;
        }

        public void Tick(float delta)
        {
            if (_effect == null) return;
            if (Input.MouseMode == Input.MouseModeEnum.Visible)
                Input.MouseMode = Input.MouseModeEnum.Hidden;

            Vector2 viewportSize = _scene.GetViewport().GetVisibleRect().Size;
            if (viewportSize.X <= 0f || viewportSize.Y <= 0f) return;

            _lastMouse = _scene.GetViewport().GetMousePosition();
            Vector2 design = new(
                _lastMouse.X * BaseWidth / viewportSize.X,
                _lastMouse.Y * BaseHeight / viewportSize.Y);
            _effect.Model.Position = new Vector3(design.X * ViewportScale,
                (BaseHeight - design.Y) * ViewportScale, 0f);

            UpdateState(design, delta);
            ApplyTint(_tint);
            PlaySequence(_state);
        }

        private const float BaseWidth = 1024f;
        private const float BaseHeight = 768f;

        private void UpdateState(Vector2 design, float delta)
        {
            if (_moveFlash > 0f) _moveFlash = Mathf.Max(0f, _moveFlash - delta);

            if (_scene.CommandPending || _scene.CastPending || _scene._placingId != null)
            {
                SetState(CursorState.Target, Colors.White);
                return;
            }

            if (design.Y >= BaseHeight * (1f - ConsoleBandRatio))
            {
                SetState(CursorState.Normal, Colors.White);
                return;
            }

            Unit hover = _scene.PickUnit(_scene.GetViewport().GetMousePosition(),
                _scene.ViewPlayer, anyPlayer: true);
            if (hover != null && hover.Alive && !hover.Untargetable && IsCursorVisible(hover))
            {
                SetState(CursorState.Select, TeamTint(hover));
                return;
            }

            bool rightMove = _scene._rightDown && HasMovableSelection();
            if (rightMove || _moveFlash > 0f)
            {
                SetState(CursorState.Move, Colors.White);
                return;
            }

            SetState(CursorState.Normal, Colors.White);
        }

        private bool IsCursorVisible(Unit unit)
        {
            if (unit.Player == _scene.ViewPlayer || _scene.AreAllied(unit.Player, _scene.ViewPlayer))
                return true;
            return _scene._fog == null || !_scene._fog.Enabled
                || _scene._fog.IsVisible(unit.Position);
        }

        private bool HasMovableSelection()
        {
            foreach (Unit unit in _scene._selected)
            {
                if (unit == null || !unit.Alive || unit.IsBuilding) continue;
                if (unit.Player == _scene.ViewPlayer) return true;
            }
            return false;
        }

        private Color TeamTint(Unit unit)
        {
            if (unit.Player >= 12) return new Color(1f, 1f, 0.2f);
            if (unit.Player == _scene.ViewPlayer || _scene.AreAllied(unit.Player, _scene.ViewPlayer))
                return new Color(0.2f, 1f, 0.2f);
            return new Color(1f, 0.2f, 0.2f);
        }

        private void SetState(CursorState state, Color tint)
        {
            _state = state;
            _tint = tint;
        }

        private void PlaySequence(CursorState state)
        {
            string name = state switch
            {
                CursorState.Normal => "Normal",
                CursorState.Target => "Target",
                CursorState.Select => "Select",
                _ => "TargetSelect",
            };
            if (!_effect.Player.HasAnimation(name))
                name = _effect.Player.HasAnimation("Normal") ? "Normal" : null;
            if (string.IsNullOrEmpty(name) || name == _animation) return;

            Animation animation = _effect.Player.GetAnimation(name);
            if (animation != null) animation.LoopMode = Animation.LoopModeEnum.Linear;
            _effect.Player.Play(name);
            _animation = name;
        }

        private void ApplyTint(Color tint)
        {
            foreach (ShaderMaterial material in _effect.Materials)
                material.SetShaderParameter("geoset_tint", tint);
        }

        private static void CacheTintMaterials(Node root, List<ShaderMaterial> materials)
        {
            foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
            {
                if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;
                if (mesh.MaterialOverride is ShaderMaterial overrideMaterial)
                {
                    ShaderMaterial copy = (ShaderMaterial)overrideMaterial.Duplicate();
                    mesh.MaterialOverride = copy;
                    materials.Add(copy);
                }
                int surfaces = mesh.Mesh.GetSurfaceCount();
                for (int surface = 0; surface < surfaces; surface++)
                {
                    Material material = mesh.GetSurfaceOverrideMaterial(surface)
                        ?? mesh.Mesh.SurfaceGetMaterial(surface);
                    if (material is not ShaderMaterial shader) continue;
                    ShaderMaterial copy = (ShaderMaterial)shader.Duplicate();
                    mesh.SetSurfaceOverrideMaterial(surface, copy);
                    materials.Add(copy);
                }
            }
        }

        public void Dispose()
        {
            if (_effect == null) return;
            if (GodotObject.IsInstanceValid(_effect.Layer)) _effect.Layer.QueueFree();
            if (GodotObject.IsInstanceValid(_effect.Viewport)) _effect.Viewport.QueueFree();
            _effect = null;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}