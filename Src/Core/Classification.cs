namespace OpenWC3;

[Flags]
public enum Classification
{
    None = 0,
    Peon = 1 << 0,
    Mechanical = 1 << 1,
    Hero = 1 << 2,
    Ancient = 1 << 3,
    Organic = 1 << 4,
    Undead = 1 << 5,
    Giant = 1 << 6,
    Summoned = 1 << 7,
    Sapper = 1 << 8,
    TownHall = 1 << 9,
    Tree = 1 << 10,
    Ward = 1 << 11,
    StandOn = 1 << 12,
    Neutral = 1 << 13,
    Tauren = 1 << 14,
}
