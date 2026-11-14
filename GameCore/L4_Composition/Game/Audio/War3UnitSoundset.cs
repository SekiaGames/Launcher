namespace OpenWC3.Game;

public sealed class War3UnitSoundset
{
    public readonly War3UnitSound What, Pissed, YesAttack, Yes, Ready, Warcry;

    private War3UnitSoundset(War3UnitSound what, War3UnitSound pissed, War3UnitSound yesAttack,
        War3UnitSound yes, War3UnitSound ready, War3UnitSound warcry)
    {
        What = what;
        Pissed = pissed;
        YesAttack = yesAttack;
        Yes = yes;
        Ready = ready;
        Warcry = warcry;
    }

    public static War3UnitSoundset Create(string soundName)
    {
        if (string.IsNullOrWhiteSpace(soundName)) return null;
        War3UnitSound Pick(string kind)
        {
            War3SoundTables.TryGet(soundName + kind, out War3SoundTables.Entry entry);
            return War3UnitSound.Create(entry);
        }
        var set = new War3UnitSoundset(Pick("What"), Pick("Pissed"), Pick("YesAttack"),
            Pick("Yes"), Pick("Ready"), Pick("Warcry"));
        if (set.What.IsSilent && set.Yes.IsSilent && set.YesAttack.IsSilent) return null;
        return set;
    }
}
