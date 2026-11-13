namespace OpenWC3.UI;

// UI 命令载体: Frame 事件经 UiEventRouter 推送, 逻辑层 (MeleeHudController) 解释为 PlayerCommand
// 与 Game 层现有 GameUiCommand 分离, 避免命名冲突; 由 Controller 桥接
public readonly record struct UiCommand(UiCommandKind Kind, string Payload = "", int Slot = -1, object Context = null,
    UiNativeCommandBinding NativeBinding=null,int NativeEventKind=1);

public enum UiCommandKind
{
    Move, AttackMove, Patrol, Stop, Hold, Ability, PassiveAbility, OpenBuild, Back, Build, UseItem, PawnItem, SelectUnit,
    DialogButton, PurchaseStock, EntangleGoldMine, OpenLearnSkills, LearnSkill, CancelConstruction,
    Train, Research, CancelTraining, Rally, ReviveHero, UpgradeBuilding, ToggleAutoCast,
    CancelBuildPlacement,
    ClearSelection, RecallControlGroup, SetControlGroup,
    OpenCheatDialog, CheatAction,
    ReturnResources,
    OpenPlayerInput,
    CancelTargeting,
    // 盟友对话框资源图标: 左键赠送 / 右键请求 (CAllianceSlot 0x6f39f9b0)。
    AllianceResource,
}
