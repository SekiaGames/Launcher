namespace OpenWC3.Game;

/// <summary>CAbilityItemPlaceMine（AIpm）：在目标点放置 UnitID 地雷。</summary>
public sealed class RuntimeItemPlaceMineAbility(War3ObjectData objects) : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Point;
    public string MineUnitRawcode { get; private set; }="";
    public float PlacementRadius { get; private set; }
    public Unit LastPlacedMine { get; private set; }

    public void RefreshPlaceMineData()
    {
        int level=Math.Max(1,Level);
        MineUnitRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"UnitID",level);
        if(MineUnitRawcode.Length>4)MineUnitRawcode=MineUnitRawcode[..4];
        PlacementRadius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,0));
    }

    public override void OnLevelUp()=>RefreshPlaceMineData();
    public override void OnLevelDown()=>RefreshPlaceMineData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null;
    public override void OnEffect(Unit target,Vec2 point)
    {
        LastPlacedMine=RuntimeAbilityFactory.SpawnSummoned(
            objects,Owner,MineUnitRawcode,point,0);
    }
}
