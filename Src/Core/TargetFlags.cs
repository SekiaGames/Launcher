namespace OpenWC3;

// CTargetType masks shared by spells, auras and weapons (project bit values).
[Flags]
public enum TargetFlags
{
    None = 0, Ground = 1, Air = 2, Structure = 4,
    Enemy = 8, Ally = 16, Self = 32, NotSelf = 64,
    NonMagicImmune = 128, NonEthereal = 256, Vulnerable = 512, Invulnerable = 1024,
    Hero = 2048, NonHero = 4096, Organic = 8192, Mechanical = 16384, Dead = 32768,
    Alive = 65536, Ancient = 131072, NonAncient = 262144,
    Player = 524288, // same owning player; distinct from allied players
    Neutral = 1048576, // PASSIVE without directional SHARED_SPELLS
    NoTargets = 2097152, // explicit "none"; distinct from unspecified None=0
    Ward = 1 << 22, Item = 1 << 23, Tree = 1 << 24, Wall = 1 << 25,
    Debris = 1 << 26, Decoration = 1 << 27, Bridge = 1 << 28,
    Sapper = 1 << 29, NonSapper = 1 << 30,
}
