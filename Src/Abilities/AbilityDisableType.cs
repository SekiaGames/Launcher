namespace OpenWC3;

// 技能禁用原因位掩码 (对标 Warsmash CAbilityDisableType: 多来源禁用可并存)
[Flags]
public enum AbilityDisableType
{
    None = 0,
    Construction = 1,
    Silence = 2,
    Stun = 4,
    Cargo = 8,
    Channel = 16,
    TechTree = 32,
    Script = 64,
    PlayerAvailability = 128,
    Illusion = 256,
    Transformation = 512,
    Militia = 1024,
}
