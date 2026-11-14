using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 物编 `EfctID`(X* 特效 ID) → 模型路径的公共解析器。对标客户端
/// `GameCore/L3_Simulation/Simulation/Configuration/War3EffectArtResolver.cs`。
///
/// 1.27 的 `Units\AbilityBuffData.slk` 只有 9 个结构列，没有任何模型路径列，
/// 所以 `GetBuff("X***").GetString("EffectArt")` 恒为空。特效 ID 的真实美术写在
/// `Units\*AbilityFunc.txt` 的同名条目里：
///   [XHbz] Effectart=Abilities\Spells\Human\Blizzard\BlizzardTarget.mdl
/// 这些文本段已经并进 <see cref="War3ObjectData"/>，这里按特效 ID 取合并后的字段。
/// </summary>
public static class War3EffectArtResolver
{
    static readonly string[] ArtFields = { "Effectart", "Buffart", "Targetart" };

    public static IReadOnlyList<string> Candidates(string effectRawcode)
    {
        string id = EffectId(effectRawcode);
        if (id.Length < 2) return Array.Empty<string>();
        List<string> arts = FromObjectData(id);
        if (arts != null) return arts;
        return KnownCandidates(id);
    }

    public static string Art(string effectRawcode)
    {
        IReadOnlyList<string> arts = Candidates(effectRawcode);
        return arts.Count > 0 ? arts[0] : "";
    }

    static string EffectId(string rawcode)
    {
        if (string.IsNullOrWhiteSpace(rawcode)) return "";
        string id = rawcode.Trim();
        int comma = id.IndexOf(',');
        if (comma >= 0) id = id[..comma].Trim();
        return id;
    }

    static List<string> FromObjectData(string id)
    {
        IRuntimeObjectData data = War3ObjectData.GetBuffType(id);
        data ??= War3ObjectData.GetAbility(id);
        if (data == null) return null;

        List<string> arts = null;
        foreach (string field in ArtFields)
        {
            IReadOnlyList<string> found = AbilityArt.Arts(data, 1, field, "");
            for (int index = 0; index < found.Count; index++)
            {
                string art = found[index];
                arts ??= new List<string>(found.Count);
                bool duplicate = false;
                for (int existing = 0; existing < arts.Count; existing++)
                {
                    if (!string.Equals(arts[existing], art, StringComparison.OrdinalIgnoreCase)) continue;
                    duplicate = true;
                    break;
                }
                if (!duplicate) arts.Add(art);
            }
        }
        return arts;
    }

    static IReadOnlyList<string> KnownCandidates(string id)
    {
        string key = id.StartsWith("X") && id.Length > 1 ? id[1..] : id;
        if (!KnownEffectModels.TryGetValue(key, out string model)) return Array.Empty<string>();
        return new[]
        {
            $@"Effects\{model}.mdl",
            $@"Effects\{model}.blp",
            $@"ReplaceableTextures\Effects\{model}.blp",
        };
    }

    /// <summary>
    /// 客户端登记的已知别名。1.27 资源里没有 `Effects\` 目录，这张表只在自定义地图
    /// 自带该目录时才有落点；真实美术由上面的物编字段解析给出。
    /// </summary>
    static readonly Dictionary<string, string> KnownEffectModels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["erc"] = "InfernoSpawn",
            ["hbz"] = "WispExplode",
            ["hbb"] = "SpellTargetParticles",
            ["oeq"] = "Earthquake",
            ["ore"] = "Rebirth",
            ["etq"] = "Tranquility",
            ["udd"] = "DeathAndDecay",
            ["nmo"] = "Monsoon",
            ["fla"] = "Flare",
            ["clf"] = "CloudOfFog",
            ["esn"] = "SentinelOwl",
            ["esf"] = "Starfall",
            ["bof"] = "BallsOfFire",
            ["hwd"] = "HealingWard",
            ["fsp"] = "FaerieDragon",
        };
}