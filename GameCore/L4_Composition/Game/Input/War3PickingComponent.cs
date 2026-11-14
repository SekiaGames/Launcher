using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    internal sealed class War3PickingComponent
    {
        private readonly War3GameScene _scene;

        internal War3PickingComponent(War3GameScene scene) => _scene = scene;

        internal Unit PickUnit(Vector2 screenPosition, int player, bool anyPlayer = false,
            bool includeCorpses = false)
            => PickUnit(screenPosition, unit => MatchesOwner(unit, player, anyPlayer, includeCorpses));

        /// <summary>射线打模型：包围盒排队，最近的几个再逐三角面确认（客户端 IntersectRayPicking）。</summary>
        internal Unit PickUnit(Vector2 screenPosition, Func<Unit, bool> predicate)
        {
            Camera3D camera = _scene._camera.Camera;
            Vector3 origin = camera.ProjectRayOrigin(screenPosition);
            Vector3 direction = camera.ProjectRayNormal(screenPosition);

            _candidates.Clear();
            foreach (Unit unit in Units())
            {
                if (unit.IsCargoed || unit.Hidden) continue;
                if (predicate != null && !predicate(unit)) continue;
                if (!IsVisible(unit)) continue;
                if (!PickGeometry.TryRayBounds(unit.View?.Root, origin, direction, out float entry)) continue;
                _candidates.Add(new PickCandidate(unit, entry));
            }
            return Resolve(origin, direction);
        }

        private Unit Resolve(Vector3 origin, Vector3 direction)
        {
            if (_candidates.Count == 0) return null;
            _candidates.Sort((left, right) => left.Entry.CompareTo(right.Entry));
            int probe = Mathf.Min(_candidates.Count, MeshPickDepth);
            for (int index = 0; index < probe; index++)
            {
                PickCandidate candidate = _candidates[index];
                if (!PickGeometry.TryRayHit(candidate.Unit?.View?.Root, origin, direction, out _)) continue;
                return candidate.Unit;
            }
            // 网格数据缺失（材质球、粒子壳这类）时退回包围盒命中，别让点选凭空失灵。
            return _candidates[0].Unit;
        }

        private readonly struct PickCandidate
        {
            public readonly Unit Unit;
            public readonly float Entry;

            public PickCandidate(Unit unit, float entry)
            {
                Unit = unit;
                Entry = entry;
            }
        }

        private const int MeshPickDepth = 3;
        private readonly List<PickCandidate> _candidates = new();
        private readonly List<(TreeState Tree, float Entry)> _treeCandidates = new();

        internal TreeState PickTree(Vector2 screenPosition, Func<TreeState, bool> predicate = null)
        {
            Camera3D camera = _scene._camera.Camera;
            Vector3 origin = camera.ProjectRayOrigin(screenPosition);
            Vector3 direction = camera.ProjectRayNormal(screenPosition);

            _treeCandidates.Clear();
            foreach (TreeState tree in _scene._trees ?? new List<TreeState>())
            {
                if (tree == null || !tree.Alive) continue;
                if (predicate != null && !predicate(tree)) continue;
                if (!IsVisible(tree.Position)) continue;
                if (!PickGeometry.TryRayBounds(tree.Node, origin, direction, out float entry)) continue;
                _treeCandidates.Add((tree, entry));
            }
            if (_treeCandidates.Count == 0) return null;
            _treeCandidates.Sort((left, right) => left.Entry.CompareTo(right.Entry));

            int probe = Mathf.Min(_treeCandidates.Count, MeshPickDepth);
            for (int index = 0; index < probe; index++)
            {
                TreeState tree = _treeCandidates[index].Tree;
                if (!PickGeometry.TryRayHit(tree.Node, origin, direction, out _)) continue;
                return tree;
            }
            return _treeCandidates[0].Tree;
        }

        internal bool PickGroundItem(Vector2 screenPosition, Func<Item, bool> predicate, out Item item)
        {
            item = null;
            Camera3D camera = _scene._camera.Camera;
            Vector3 origin = camera.ProjectRayOrigin(screenPosition);
            Vector3 direction = camera.ProjectRayNormal(screenPosition);

            float bestDistance = float.MaxValue;
            foreach (Item candidate in _scene._world?.GroundItems ?? (IReadOnlyList<Item>)Array.Empty<Item>())
            {
                if (candidate == null || !candidate.OnGround) continue;
                if (predicate != null && !predicate(candidate)) continue;
                if (!IsVisible(candidate.Position)) continue;
                if (!PickGeometry.TryRayBounds(candidate.Node, origin, direction, out _)) continue;
                if (!PickGeometry.TryRayHit(candidate.Node, origin, direction, out float hit)) continue;
                if (hit >= bestDistance) continue;
                bestDistance = hit;
                item = candidate;
            }
            return item != null;
        }

        internal List<Unit> UnitsInScreenRect(Vector2 min, Vector2 max, Func<Unit, bool> predicate)
        {
            var result = new List<Unit>();
            Camera3D camera = _scene._camera.Camera;
            Rect2 rect = new(min, max - min);
            foreach (Unit unit in Units())
            {
                if (!unit.Alive || unit.IsCargoed || unit.Hidden) continue;
                if (predicate != null && !predicate(unit)) continue;
                if (!IsVisible(unit)) continue;

                Vector3 point = unit.Position + new Vector3(0f,
                    unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
                if (camera.IsPositionBehind(point)) continue;
                if (!rect.HasPoint(camera.UnprojectPosition(point))) continue;
                result.Add(unit);
            }
            return result;
        }

        private IEnumerable<Unit> Units()
            => _scene._world?.Units ?? (IEnumerable<Unit>)_scene._allUnits;

        private bool IsVisible(Unit unit)
        {
            if (unit.Player == _scene.ViewPlayer
                || _scene.AreAllied(unit.Player, _scene.ViewPlayer)) return true;
            return IsVisible(unit.Position);
        }

        private bool IsVisible(Vector3 position)
            => _scene._fog == null || !_scene._fog.Enabled || _scene._fog.IsVisible(position);

        private bool MatchesOwner(Unit unit, int player, bool anyPlayer, bool includeCorpses)
        {
            if (!unit.Alive && !(includeCorpses && unit.IsCorpse)) return false;
            if (unit.Alive && unit.State.Invisible && unit.Player != _scene.ViewPlayer
                && (_scene._world == null || !_scene._world.IsDetectedBy(_scene.ViewPlayer, unit)))
                return false;
            if (anyPlayer) return true;
            if (unit.Player == player) return true;
            return War3GameScene.IsNeutralShop(unit);
        }
    }
}