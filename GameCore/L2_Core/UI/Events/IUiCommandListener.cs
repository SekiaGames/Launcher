namespace OpenWC3.UI;

// UI 命令回调: 逻辑层实现, Frame 事件经 UiEventRouter 推送到此
// 对齐 Warsmash CommandCardCommandListener
public interface IUiCommandListener
{
    void OnUiCommand(UiCommand command);
}
