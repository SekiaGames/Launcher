namespace OpenWC3.Game;

public enum MeleeAiPreset
{
    Normal,
    Crazy,
    TotallyCrazy,
}

public static class MeleeAiPresetRules
{
    public static int Difficulty(MeleeAiPreset preset) => preset switch
    {
        MeleeAiPreset.Normal => 1,
        MeleeAiPreset.Crazy or MeleeAiPreset.TotallyCrazy => 2,
        _ => 1,
    };
}