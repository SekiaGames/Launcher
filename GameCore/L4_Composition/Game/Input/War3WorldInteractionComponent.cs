using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    internal sealed class War3WorldInteractionComponent
    {
        private readonly War3GameScene _scene;
        private bool _leftDown;
        private Vector2 _dragStart;
        private float _clickTime = -1f;
        private Unit _clickUnit;
        private float _groupPressedAt = -1f;
        private int _groupPressed = -1;

        internal War3WorldInteractionComponent(War3GameScene scene) => _scene = scene;

        internal void PointerDown(Vector2 position, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                if (Input.IsKeyPressed(Key.Alt) && _scene._placingId == null)
                {
                    if (_scene.TryGroundPoint(position, out Vector3 pingPoint))
                    {
                        _scene.PingMinimap(new System.Numerics.Vector2(-pingPoint.X, pingPoint.Z));
                    }
                    return;
                }

                _leftDown = true;
                _dragStart = position;
                _scene._hud?.SetSelectionBox(new Rect2(), false);
                return;
            }

            if (button == MouseButton.Right)
            {
                if (_scene._placingId != null) _scene.CancelPlacement();
                else if (_scene.CastPending) _scene.CancelCast();
                else OrderAt(position, Input.IsKeyPressed(Key.Shift));
            }
        }

        internal void PointerUp(Vector2 position, MouseButton button)
        {
            if (button != MouseButton.Left || !_leftDown) return;
            _leftDown = false;

            if (_scene._placingId != null)
            {
                if (position.DistanceTo(_dragStart) < 8f)
                    _scene.TryPlace(position, Input.IsKeyPressed(Key.Shift));
                return;
            }

            if (position.DistanceTo(_dragStart) < 8f)
            {
                _scene._hud?.SetSelectionBox(new Rect2(), false);
                HandleClick(position, Input.IsKeyPressed(Key.Shift));
                return;
            }

            _scene._hud?.SetSelectionBox(new Rect2(), false);
            BoxSelect(_dragStart, position, Input.IsKeyPressed(Key.Shift));
        }

        internal void PointerMove(Vector2 position)
        {
            if (_leftDown && _scene._placingId == null
                && position.DistanceTo(_dragStart) >= 8f)
                _scene._hud?.SetSelectionBox(BoxRect(_dragStart, position), true);
        }

        private void HandleClick(Vector2 position, bool additive)
        {
            if (_scene.TryCommandAt(position)) return;
            if (_scene.TryCastAt(position)) return;

            Unit picked = _scene.PickUnit(position, _scene.ViewPlayer);
            if (picked == null)
            {
                // 点到地面物品：主位英雄装得下就派它去捡，否则只把详情摊出来。
                if (!additive && _scene._picking.PickGroundItem(position, null, out Item item))
                {
                    Unit primary = _scene.PrimarySelection;
                    if (primary != null && primary.Alive && primary.CanCarryItems
                        && primary.HasFreeSlot && primary.CanMove)
                    {
                        _scene._commandProcessor.Move(
                            new Vec2(item.Position.X, item.Position.Z), false,
                            silent: true, explicitMovers: new[] { primary });
                        _scene._hud?.SetHint($"拾取 {item.Type.Name}");
                    }
                    else _scene.SelectGroundItem(item);
                    _scene.CancelPending();
                    return;
                }
                if (!additive) _scene._selection?.Clear();
                _scene.CancelPending();
                return;
            }

            // 原版 "Ctrl + 选定单位 或者 双击单位 = 选中屏幕附近所有的此种单位"。
            if (IsSelectAllSameType(picked) && SelectSameType(picked, additive)) return;

            _scene._selection?.SelectClicked(picked, additive, mouseChannel: true);
            _scene.CancelPending();
        }

        /// <summary>双击（同一单位、0.4 秒内）或按住 Ctrl 点单位。</summary>
        private bool IsSelectAllSameType(Unit clicked)
        {
            float now = Time.GetTicksMsec() / 1000f;
            bool doubleClick = ReferenceEquals(_clickUnit, clicked) && now - _clickTime <= 0.4f;
            _clickUnit = clicked;
            _clickTime = now;
            return doubleClick || Input.IsKeyPressed(Key.Ctrl);
        }

        internal void HandleGroupKey(int group, bool ctrl, bool shift)
        {
            if (_scene._selection == null) return;
            if (ctrl)
            {
                _scene._selection.SetControlGroup(group);
                _scene._hud?.SetHint($"编队 {group} 已保存（{_scene._selected.Count} 个单位）");
                return;
            }
            if (shift)
            {
                _scene._selection.AddToControlGroup(group);
                _scene._hud?.SetHint($"已并入编队 {group}");
                return;
            }

            _scene._selection.RecallControlGroup(group);
            if (_scene._selected.Count == 0) return;

            Vector3 sum = Vector3.Zero;
            foreach (Unit unit in _scene._selected) sum += unit.Position;

            float now = Time.GetTicksMsec() / 1000f;
            bool center = _groupPressed == group && now - _groupPressedAt < 0.5f;
            _groupPressed = group;
            _groupPressedAt = now;
            if (center) _scene._camera.Focus(sum / _scene._selected.Count);
        }

        private bool SelectSameType(Unit reference, bool additive)
        {
            Vector2 size = _scene.GetViewport().GetVisibleRect().Size;
            List<Unit> units = _scene._picking?.UnitsInScreenRect(Vector2.Zero, size,
                unit => _scene.CanControl(unit)
                    && string.Equals(unit.Type.Id, reference.Type.Id, StringComparison.OrdinalIgnoreCase))
                ?? new List<Unit>();
            // 一颗都选不到时退回单选，不让一次 Ctrl+点击变成"什么都没发生"。
            if (units.Count == 0) return false;
            _scene._selection?.SelectRect(units, additive);
            _scene.CancelPending();
            return true;
        }

        private void BoxSelect(Vector2 from, Vector2 to, bool additive)
        {
            if (!ClipToViewport(from, to, out Vector2 lo, out Vector2 hi)) return;
            List<Unit> units = _scene._picking?.UnitsInScreenRect(lo, hi,
                unit => _scene.CanControl(unit)) ?? new List<Unit>();
            _scene._selection?.SelectRect(units, additive);
            _scene.CancelPending();
        }

        /// <summary>框到屏幕外的部分裁掉而不是整框作废（客户端 ScreenViewport.TryClipRect）。</summary>
        private bool ClipToViewport(Vector2 first, Vector2 second, out Vector2 lo, out Vector2 hi)
        {
            Vector2 size = _scene.GetViewport().GetVisibleRect().Size;
            lo = new Vector2(Mathf.Max(Mathf.Min(first.X, second.X), 0f),
                Mathf.Max(Mathf.Min(first.Y, second.Y), 0f));
            hi = new Vector2(Mathf.Min(Mathf.Max(first.X, second.X), size.X),
                Mathf.Min(Mathf.Max(first.Y, second.Y), size.Y));
            return lo.X < hi.X && lo.Y < hi.Y;
        }

        internal void MinimapOrderAt(Vector3 worldPoint, bool queue)
        {
            if (_scene._selected.Count == 0) return;

            Unit enemy = null;
            float bestDistance = 90f;
            foreach (Unit unit in _scene._allUnits)
            {
                if (!unit.Alive || _scene.AreAllied(unit.Player, _scene.ViewPlayer)) continue;
                if (_scene._fog != null && _scene._fog.Enabled
                    && !_scene._fog.IsVisible(unit.Position)) continue;
                float distance = new Vector2(unit.Position.X - worldPoint.X,
                    unit.Position.Z - worldPoint.Z).Length();
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                enemy = unit;
            }

            if (enemy != null) _scene.IssueAttackOrders(enemy, queue);
            else _scene.IssueMoveOrders(worldPoint, queue);
        }

        /// <summary>
        /// 右键语境指令：每一支只吃掉干得了这活的单位，剩下的继续执行右键本义（客户端 IssueContextCommandAt）。
        /// </summary>
        internal void OrderAt(Vector2 screenPosition, bool queue)
        {
            if (_scene._selected.Count == 0) return;
            if (_scene._selection.LiveControlled().Length == 0) return;

            var handled = new HashSet<Unit>();

            // 1. 集结点：逐单位按 CanSetRally 过滤，混选里的其余单位继续往下走。
            if (_scene.TryIssueRallyAt(screenPosition, out List<Unit> rallyTaken))
            {
                foreach (Unit producer in rallyTaken) handled.Add(producer);
            }

            // 目标只拾取一次：换个判据重新拾取会打到单位身后那座建筑上。
            Unit target = _scene.PickUnit(screenPosition, unit => unit.Alive);

            // 2. 交资源 / 继续建造 / 修理：目标得是自己能控制的建筑。
            Unit receiver = target != null && target.IsBuilding && _scene.CanControl(target)
                ? target : null;
            if (receiver != null)
            {
                Unit[] returned = IssueReturnResources(receiver);
                if (returned.Length > 0)
                {
                    _scene._commandProcessor.NotifyExternalOrder(
                        new Vec2(receiver.Position.X, receiver.Position.Z), false);
                    _scene._hud?.SetHint($"交付资源：{returned.Length} 个工人 → {receiver.Type.Name}");
                    AddHandled(handled, returned);
                    MoveUnhandled(handled, receiver, queue);
                    return;
                }

                bool constructing = receiver.IsUnderConstruction;
                Unit[] workers = constructing
                    ? IssueResumeConstruction(receiver)
                    : IssueRepairBuilding(receiver);
                if (workers.Length > 0)
                {
                    _scene._hud?.SetHint(constructing
                        ? $"继续建造：{workers.Length} 个工人 → {receiver.Type.Name}"
                        : $"修理：{workers.Length} 个工人 → {receiver.Type.Name}（{receiver.Hp:0}/{receiver.MaxHp:0}）");
                    AddHandled(handled, workers);
                    MoveUnhandled(handled, receiver, queue);
                    return;
                }
            }

            // 3. 采金：只有能吃金矿的工人接单。
            if (target != null && target.Gold > 0)
            {
                Unit[] sent = IssueGatherGold(target);
                if (sent.Length > 0)
                {
                    _scene._commandProcessor.NotifyExternalOrder(
                        new Vec2(target.Position.X, target.Position.Z), false);
                    _scene._hud?.SetHint($"采金：{sent.Length} 个工人 → {target.Type.Name}（剩余 {target.Gold}）");
                    AddHandled(handled, sent);
                    MoveUnhandled(handled, target, queue);
                    return;
                }
            }

            // 4. 载具装卸 / 攻击 / 跟随：目标是一个单位。
            if (target != null)
            {
                int loaded = _scene._commandProcessor.TransportSmart(target, queue);
                if (loaded > 0)
                {
                    _scene._hud?.SetHint($"装卸：{loaded} 个单位 ⇄ {target.Type.Name}");
                    return;
                }

                Unit[] eligible = _scene._commandProcessor.EligibleAttackers(target);
                if (eligible.Length > 0)
                {
                    int attackers = _scene._commandProcessor.Attack(target, queue);
                    _scene._hud?.SetHint(queue
                        ? $"追加攻击 {target.Type.Name}"
                        : $"攻击 {target.Type.Name}（{attackers} 个单位）");
                    AddHandled(handled, eligible);
                    MoveUnhandled(handled, target, queue);
                    return;
                }

                int followers = _scene._commandProcessor.Follow(target, queue);
                if (followers > 0)
                {
                    _scene._hud?.SetHint(queue
                        ? $"追加跟随 {target.Type.Name}"
                        : $"跟随 {target.Type.Name}（{followers} 个单位）");
                    AddHandled(handled, _scene._selection.LiveControlled());
                }
                return;
            }

            // 5. 伐木。
            TreeState tree = _scene._picking.PickTree(screenPosition);
            if (tree != null)
            {
                Unit[] choppers = IssueChopLumber(tree);
                if (choppers.Length > 0)
                {
                    _scene._commandProcessor.NotifyExternalOrder(
                        new Vec2(tree.Position.X, tree.Position.Z), false);
                    _scene._hud?.SetHint($"伐木：{choppers.Length} 个工人 → {tree.Id}（{tree.Hp:0} HP）");
                    AddHandled(handled, choppers);
                    MoveUnhandled(handled, null, tree.Position, queue);
                    return;
                }
            }

            // 6. 地面物品：主位单位接拾取订单，走到 PickupItemRange 内再结算。
            if (_scene._picking.PickGroundItem(screenPosition, null, out Item item))
            {
                Unit picker = _scene.PrimarySelection;
                if (picker != null && picker.Alive)
                {
                    PickupItemResult pickup = _scene._world.IssuePickupItem(picker, item);
                    if (pickup == PickupItemResult.InventoryFull)
                    {
                        _scene._hud?.SetHint("物品栏已满。");
                        return;
                    }
                    if (pickup == PickupItemResult.Issued)
                    {
                        _scene._hud?.SetHint($"拾取 {item.Type.Name}");
                        MoveUnhandled(new HashSet<Unit> { picker }, null, item.Position, queue);
                        return;
                    }
                }
            }

            // 7. 地面：没有任何能走的单位时保持静默，不打空指令。
            if (!_scene.TryGroundPoint(screenPosition, out Vector3 ground)) return;
            List<Unit> movers = UnhandledMovers(handled, null);
            if (movers.Count == 0) return;
            _scene._commandProcessor.Move(new Vec2(ground.X, ground.Z), queue,
                explicitMovers: movers.ToArray());
            _scene._hud?.SetHint(queue
                ? $"追加移动 {movers.Count} 个单位"
                : $"移动到 ({ground.X:0}, {ground.Z:0})，{movers.Count} 个单位按阵型展开");
        }

        private static void AddHandled(HashSet<Unit> handled, IEnumerable<Unit> units)
        {
            foreach (Unit unit in units)
            {
                if (unit != null) handled.Add(unit);
            }
        }

        /// <summary>剩下的单位继续走向目标（客户端 MoveUnhandled）：不发确认音，反馈归触发它的那条指令。</summary>
        private void MoveUnhandled(ICollection<Unit> handled, Unit targetObject, bool queue)
            => MoveUnhandled(handled, targetObject,
                targetObject != null ? targetObject.Position : Vector3.Zero, queue);

        private void MoveUnhandled(ICollection<Unit> handled, Unit targetObject, Vector3 point, bool queue)
        {
            List<Unit> rest = UnhandledMovers(handled, targetObject);
            if (rest.Count == 0) return;
            _scene._commandProcessor.Move(new Vec2(point.X, point.Z), queue,
                silent: true, explicitMovers: rest.ToArray());
        }

        private List<Unit> UnhandledMovers(ICollection<Unit> handled, Unit targetObject)
        {
            var rest = new List<Unit>();
            foreach (Unit unit in _scene._selection.LiveControlled())
            {
                if (handled != null && handled.Contains(unit)) continue;
                if (targetObject != null && ReferenceEquals(unit, targetObject)) continue;
                if (!unit.CanMove) continue;
                rest.Add(unit);
            }
            return rest;
        }

        /// <summary>派出的工人逐个下发"交付资源"，返回真正接活的那些。</summary>
        private Unit[] IssueReturnResources(Unit receiver)
        {
            var sent = new List<Unit>();
            foreach (Unit worker in _scene._selection.LiveControlled())
            {
                if (_scene._world.SetReturnResources(worker, receiver)) sent.Add(worker);
            }
            return sent.ToArray();
        }

        /// <summary>派出的工人逐个下发"继续建造"，返回真正接活的那些。</summary>
        private Unit[] IssueResumeConstruction(Unit site)
        {
            var sent = new List<Unit>();
            foreach (Unit worker in _scene._selection.LiveControlled())
            {
                if (ReferenceEquals(worker, site)) continue;
                if (!worker.Alive || !worker.CanMove) continue;
                if (worker.Player != site.Player) continue;
                if (!CanResumeConstruction(worker, site)) continue;
                _scene._world.SetBuild(worker, site);
                sent.Add(worker);
            }
            return sent.ToArray();
        }

        private static bool CanResumeConstruction(Unit worker, Unit site)
        {
            UnitType type = worker.Type;
            if (type == null || site.Type == null) return false;
            if (Array.IndexOf(type.Builds, site.Type.Id) >= 0) return true;
            return type.IsWorkerType && World.FindRepairAbility(worker) != null;
        }

        /// <summary>派出的工人逐个下发"修理建筑"，返回真正接活的那些。</summary>
        private Unit[] IssueRepairBuilding(Unit structure)
        {
            if (structure.Hp >= structure.MaxHp) return Array.Empty<Unit>();
            var sent = new List<Unit>();
            foreach (Unit worker in _scene._selection.LiveControlled())
            {
                if (ReferenceEquals(worker, structure)) continue;
                if (!worker.Alive || !worker.CanMove) continue;
                if (worker.Type == null || !worker.Type.IsWorkerType) continue;
                if (World.FindRepairAbility(worker) == null) continue;
                _scene._world.SetRepair(worker, structure);
                sent.Add(worker);
            }
            return sent.ToArray();
        }

        /// <summary>派出的工人逐个下发"采金"，返回真正接活的那些。</summary>
        private Unit[] IssueGatherGold(Unit mine)
        {
            var sent = new List<Unit>();
            foreach (Unit worker in _scene._selection.LiveControlled())
            {
                if (!worker.Alive || worker.Type == null || !worker.Type.CanGatherGold) continue;
                _scene._world.SetGather(worker, mine);
                sent.Add(worker);
            }
            return sent.ToArray();
        }

        /// <summary>派出的工人逐个下发"伐木"，返回真正接活的那些。</summary>
        private Unit[] IssueChopLumber(TreeState tree)
        {
            var sent = new List<Unit>();
            foreach (Unit worker in _scene._selection.LiveControlled())
            {
                if (!worker.Alive || worker.Type == null || !worker.Type.CanGatherLumber) continue;
                _scene._world.SetChop(worker, tree);
                sent.Add(worker);
            }
            return sent.ToArray();
        }

        private static Rect2 BoxRect(Vector2 a, Vector2 b)
        {
            float x = Mathf.Min(a.X, b.X);
            float y = Mathf.Min(a.Y, b.Y);
            return new Rect2(x, y, Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        }
    }
}