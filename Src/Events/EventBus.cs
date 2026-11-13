namespace OpenWC3;

public enum EventSubscriptionScope { Any,PlayerUnit,Unit }

// 事件总线 (对标 getEventList(eventId)+fire 派发 + unit/player/game 三级 scope)
public sealed class EventBus
{
    sealed class Sub { public Action<EventContext> H; public Unit Unit; public int Team = -1; public EventSubscriptionScope Scope; }
    readonly Dictionary<GameEvent, EventReceivers<Sub>> _subs = new();
    readonly Dictionary<GameEvent, EventReceivers<Trigger>> _triggers = new();
    long _registrationSequence;

    // A context need not be allocated if no subscriber can observe this scope.
    public bool HasListeners(GameEvent e,Unit triggerUnit)
    {
        if(_triggers.TryGetValue(e,out var triggers))
        {
            var selected=triggers.Select(triggerUnit);
            while(selected.MoveNext(out var t))
                if((t._unit==null||t._unit==triggerUnit)
                    &&(t._team<0||(triggerUnit!=null&&t._team==triggerUnit.Team)))return true;
        }
        if(_subs.TryGetValue(e,out var subscribers))
        {
            var selected=subscribers.Select(triggerUnit);
            while(selected.MoveNext(out var s))
                if((s.Unit==null||s.Unit==triggerUnit)
                    &&(s.Team<0||(triggerUnit!=null&&s.Team==triggerUnit.Team)))return true;
        }
        return false;
    }

    // 原始 handler 注册
    public Action On(GameEvent e, Action<EventContext> handler, Unit unit = null, int team = -1,
        EventSubscriptionScope scope=EventSubscriptionScope.Any)
    {
        if (!_subs.TryGetValue(e, out var l)) { l = new(); _subs[e] = l; }
        var subscription = new Sub { H = handler, Unit = unit, Team = team,Scope=scope };
        l.Add(_registrationSequence++,subscription,unit,team);
        return () =>
        {
            l.Remove(subscription,unit,team);
        };
    }

    // 触发器注册 (对标 TriggerRegisterUnitEvent: 限单位/阵营 + 条件 + 动作)
    public Trigger Register(GameEvent e, Unit unit = null, int team = -1)
    {
        var t = new Trigger();
        if (!_triggers.TryGetValue(e, out var l)) { l = new(); _triggers[e] = l; }
        l.Add(_registrationSequence++,t,unit,team);
        t._evt = e; t._bus = this; t._unit = unit; t._team = team;   // 记录过滤条件(内部用)
        return t;
    }

    // 派发: 触发单位由类型化上下文明确给出；施法事件不能把法术目标误当成注册单位。
    // 执行顺序必须与原版一致: 先跑 JASS 触发器, 再跑 C# 内部订阅者。
    // 反过来的话掉宝/赏金(AwardKillBounty)/选择清理会先改状态, JASS 死亡触发看到的是
    // 已被修改后的值; 而且赏金加金币会同步派发 PlayerStateChanged, 在死亡触发执行期间递归。
    public void Fire(EventContext c)
    {
        // Native publishers emit the player-unit message before the matching
        // specific-unit message (for example 65fed0: 244950/80222, then
        // 65d6e0/80247). Keep game/other receivers in the final, original pass.
        Fire(c,EventSubscriptionScope.PlayerUnit);
        Fire(c,EventSubscriptionScope.Unit);
        Fire(c,EventSubscriptionScope.Any);
    }

    // Separate native receiver messages use fresh contexts and fresh listener
    // snapshots; player scripts may change unit listeners before the unit phase.
    public void FireScoped(EventContext c,EventSubscriptionScope scope)
        =>Fire(c,scope);

    void Fire(EventContext c,EventSubscriptionScope? scope)
    {
        var trigger = c.GetTriggerUnit();
        if ((!scope.HasValue||scope==EventSubscriptionScope.Any)&&_triggers.TryGetValue(c.Event, out var tl))
        {
            var selected=tl.Select(trigger);
            while(selected.MoveNext(out var t))
            {
                if (t._unit != null && t._unit != trigger) continue;
                if (t._team >= 0 && (trigger == null || trigger.Team != t._team)) continue;
                t.EvaluateAndExecute(c);
            }
        }
        if (_subs.TryGetValue(c.Event, out var l))
        {
            var selected=l.Select(trigger);
            while(selected.MoveNext(out var s))
            {
                if(scope.HasValue&&s.Scope!=scope.Value)continue;
                if (s.Unit != null && s.Unit != trigger) continue;
                if (s.Team >= 0 && (trigger == null || trigger.Team != s.Team)) continue;
                s.H(c);
            }
        }
    }
}
