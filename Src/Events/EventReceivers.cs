namespace OpenWC3;

// Select global, team-bound and matching-unit receivers. Capture all
// three arrays before dispatch; merge by registration sequence, including when
// callbacks add/remove receivers and recursively dispatch the same event.
internal sealed class EventReceivers<T> where T : class
{
    internal readonly record struct Entry(long Sequence,T Value);
    sealed class Bucket
    {
        readonly List<Entry> _entries=new();
        Entry[] _snapshot;
        internal void Add(Entry entry){_entries.Add(entry);_snapshot=null;}
        internal void Remove(T value){_entries.RemoveAll(e=>ReferenceEquals(e.Value,value));_snapshot=null;}
        internal Entry[] Snapshot()=>_snapshot??=_entries.ToArray();
        internal int Count=>_entries.Count;
    }
    readonly Bucket _global=new();
    readonly Dictionary<Unit,Bucket> _units=new();
    // Team can change inside a preceding callback. Keep these receivers in
    // sequence and let EventBus test their current team at the original boundary.
    readonly Bucket _teams=new();
    internal void Add(long sequence,T value,Unit unit,int team)
    {
        Bucket bucket;
        if(unit!=null)
        {if(!_units.TryGetValue(unit,out bucket))_units.Add(unit,bucket=new());}
        else if(team>=0)
            bucket=_teams;
        else bucket=_global;
        bucket.Add(new(sequence,value));
    }
    internal void Remove(T value,Unit unit,int team)
    {
        if(unit!=null)
        {
            if(!_units.TryGetValue(unit,out var bucket))return;
            bucket.Remove(value);if(bucket.Count==0)_units.Remove(unit);
        }
        else if(team>=0)
        {
            _teams.Remove(value);
        }
        else _global.Remove(value);
    }
    internal Cursor Select(Unit unit)=>new(_global.Snapshot(),
        unit!=null&&_units.TryGetValue(unit,out var own)?own.Snapshot():Array.Empty<Entry>(),
        _teams.Snapshot());
    internal struct Cursor(Entry[] global,Entry[] unit,Entry[] team)
    {
        int _g,_u,_t;
        internal bool MoveNext(out T value)
        {
            long g=_g<global.Length?global[_g].Sequence:long.MaxValue;
            long u=_u<unit.Length?unit[_u].Sequence:long.MaxValue;
            long t=_t<team.Length?team[_t].Sequence:long.MaxValue;
            if(g==long.MaxValue&&u==long.MaxValue&&t==long.MaxValue){value=null;return false;}
            value=g<u&&g<t?global[_g++].Value:u<t?unit[_u++].Value:team[_t++].Value;
            return true;
        }
    }
}
