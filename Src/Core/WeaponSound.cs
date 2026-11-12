namespace OpenWC3;

// Game.dll 1.27 table 0x6FA4D708 and common.j ConvertWeaponType IDs.
// Zero is the native "Unknown" label prefix, not a default melee sound.
public enum WeaponSound
{
    Whoknows = 0,
    MetalLightChop = 1, MetalMediumChop = 2, MetalHeavyChop = 3,
    MetalLightSlice = 4, MetalMediumSlice = 5, MetalHeavySlice = 6,
    MetalMediumBash = 7, MetalHeavyBash = 8,
    MetalMediumStab = 9, MetalHeavyStab = 10,
    WoodLightSlice = 11, WoodMediumSlice = 12, WoodHeavySlice = 13,
    WoodLightBash = 14, WoodMediumBash = 15, WoodHeavyBash = 16,
    WoodLightStab = 17, WoodMediumStab = 18,
    ClawLightSlice = 19, ClawMediumSlice = 20, ClawHeavySlice = 21,
    AxeMediumChop = 22, RockHeavyBash = 23,
}
