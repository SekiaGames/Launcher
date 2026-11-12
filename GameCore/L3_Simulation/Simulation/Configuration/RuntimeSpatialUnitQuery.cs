namespace OpenWC3.Game;

// Candidate source only: callers retain their exact target, distance and order
// predicates. Enumeration owns a snapshot until completion/early disposal.
internal static class RuntimeSpatialUnitQuery
{
    // Explicit value-type lease for callers which filter completely before
    // delivering callbacks. Nested queries rent their own independent lists.
    internal readonly struct Snapshot(World world,List<Unit> units) : IDisposable
    {
        internal List<Unit> Units=>units;
        public void Dispose()=>world.ReturnUnitList(units);
    }
    internal static Snapshot RentAround(World world,Vec2 center,float radius)
    {
        ArgumentNullException.ThrowIfNull(world);
        var candidates=world.RentUnitList();
        try
        {
            float broadRadius=float.IsFinite(radius)
                ?MathF.BitIncrement(Math.Max(0,radius)):float.PositiveInfinity;
            world.CollectNearbyUnitsInWorldOrder(center,broadRadius,candidates);
            return new Snapshot(world,candidates);
        }
        catch {world.ReturnUnitList(candidates);throw;}
    }
    internal static IEnumerable<Unit> Around(World world,Vec2 center,float radius)
    {
        if(world==null)yield break;
        List<Unit> candidates=world.RentUnitList();
        try
        {
            float broadRadius=float.IsFinite(radius)
                ?MathF.BitIncrement(Math.Max(0,radius)):float.PositiveInfinity;
            world.CollectNearbyUnitsInWorldOrder(center,broadRadius,candidates);
            for(int i=0;i<candidates.Count;i++)yield return candidates[i];
        }
        finally { world.ReturnUnitList(candidates); }
    }

    // DistanceTo measures collision edges rather than unit centers.
    internal static IEnumerable<Unit> InCastRange(Unit caster,float range)=>
        Around(caster?.World,caster?.Pos??default,range+(caster?.CollisionSize??0)
            +(caster?.World?.MaximumCollisionRadiusForQuery??0));

    // Native resurrection/animate-dead evaluate transported corpses at their
    // carrier's position. Local carriers expose their stored corpse records.
    internal static IEnumerable<Unit> CorpsesAround(World world,Vec2 center,float radius)
    {
        var seen=new HashSet<Unit>();
        foreach(Unit candidate in Around(world,center,radius))
        {
            if(seen.Add(candidate))yield return candidate;
            if(!candidate.Alive||candidate.Removed)continue;
            if(candidate.GetAbility<RuntimeMeatCargoAbility>() is not {} cargo)continue;
            foreach(Unit corpse in cargo.Corpses)
                if(corpse.World==world&&ReferenceEquals(corpse.CorpseCarrier,candidate)
                    &&seen.Add(corpse))yield return corpse;
        }
    }
}
