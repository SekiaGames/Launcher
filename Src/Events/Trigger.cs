namespace OpenWC3;

// 触发器 (对标 JASS Trigger): 条件(cond) 全为真时执行动作(action)
public sealed class Trigger
{
    readonly List<Func<EventContext, bool>> _conds = new();
    readonly List<Action<EventContext>> _actions = new();
    public bool Enabled = true;
    internal GameEvent _evt;
    internal EventBus _bus;
    internal Unit _unit;
    internal int _team = -1;

    public Trigger Condition(Func<EventContext, bool> cond) { _conds.Add(cond); return this; }
    public Trigger Action(Action<EventContext> act) { _actions.Add(act); return this; }

    // 求值条件 → 全真才执行动作 (对标 TriggerEvaluate + TriggerExecute)
    public bool EvaluateAndExecute(EventContext c)
    {
        if (!Enabled) return false;
        foreach (var cond in _conds) if (!cond(c)) return false;
        foreach (var act in _actions) act(c);
        return true;
    }
}
