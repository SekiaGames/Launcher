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

        internal Unit PickUnit(Vector2 screenPosition, Func<Unit, bool> predicate)
        {
            Camera3D camera = _scene._camera.Camera;
            Unit best = null;
            float bestDistance = 55f;

            foreach (Unit unit in Units())
            {
                if (unit.IsCargoed || unit.Hidden) continue;
                if (predicate != null && !predicate(unit)) continue;
                if (!IsVisible(unit)) continue;

                Vector3 point = unit.Position + new Vector3(0f,
                    unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
                if (camera.IsPositionBehind(point)) continue;

                Vector2 screen = camera.UnprojectPosition(point);
                float distance = screen.DistanceTo(screenPosition);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = unit;
            }
            return best;
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
            return _scene._fog == null || !_scene._fog.Enabled
                || _scene._fog.IsVisible(unit.Position);
        }

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