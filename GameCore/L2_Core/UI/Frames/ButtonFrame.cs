using System.Numerics;
using OpenWC3.RHI;

namespace OpenWC3.UI;

// 可点击接口: 对齐 Warsmash ClickableFrame
public interface IClickable
{
    bool Enabled { get; }
    void OnPointerDown(Vector2 pos);
    void OnPointerUp(Vector2 pos);
    void OnClick(int button);
}

// 可拖拽接口: 按下后即使指针移出本帧, 也继续收到移动 (小地图拖拽移镜头)
public interface IDraggable
{
    void OnPointerDrag(Vector2 pos);
}

// 焦点接口: 对齐 Warsmash FocusableFrame (键盘字符输入)
public interface IFocusable
{
    void OnFocusGained();
    void OnFocusLost();
    void OnKeyChar(char c);
}

// 命令卡按钮 Frame: 对齐 Warsmash CommandCardIcon
// 含 5 子帧: icon / activeHighlight / cooldown / autocast / numberOverlay
public class ButtonFrame : UiFrame, IClickable
{
    public ITexture Icon;
    public ITexture DisabledIcon;
    public ITexture ActiveHighlight;
    public Vector4 IconColor = Vector4.One;
    public float Cooldown;          // 0..1, >0 显示冷却遮罩
    object _cooldownOwner,_cooldownContent;
    UiCommand? _cooldownCommand;
    bool _cooldownSeen,_completionPending;
    double _completionStart=-1;
    // 原版 CommandButtonCooldown 使用模型动画；缺失模型时退回覆盖中心的方形扫描。
    const float CooldownInnerRatio = 0f;
    const float CooldownShadeAlpha = 0.6f;
    // Cooldown blocks clicks without also darkening the icon beneath its own mask.
    public bool DimWhenDisabled = true;
    // 置灰只改观感与点击(路由器按 IClickable.Enabled 拦截, OnClick 里也有守卫),
    // 不再连带关 Enabled: HitTest 对 !Enabled 的帧整棵子树不命中,
    // 禁用按钮会连悬浮提示都收不到 —— 原版禁用/冷却中的命令按钮同样显示 Tip/Ubertip。
    public bool Disabled { get => _disabled; set => _disabled = value; }
    bool _disabled;
    public bool IsHovered;
    public string Hotkey;
    public string OverlayText;
    /// <summary>
    /// 基类是否用通用样式绘制 <see cref="OverlayText"/> (右下角 14px 右对齐)。
    /// 原版命令按钮的数字走独立叠加层 (0x6F390420/: 0.015625² 容器锚右下角 +
    /// human-button-lvls-overlay.blp + MusicUI.txt 的 CommandButtonNumber=0.009 字号),
    /// 需要原版几何的子类置 false 并自绘。
    /// </summary>
    protected virtual bool UsesBuiltInOverlayText => true;
    public string TooltipTitle, TooltipBody;   // 悬浮提示 (物编 Tip / Ubertip)
    /// <summary>
    /// 悬浮提示的独立成本行, 固定顺序 法力/金币/木材/人口 (原版 CToolTipWar3 的 4 个成本槽);
    /// <b>0 或 null 表示该槽不显示</b> —— 原版判据是 <c>test eax,eax</c>, 只有 0 隐藏, 负数照显。
    /// 物品价值走 金币/木材 两槽, 不再拼进正文; 训练/建造/研究的人口与资源消耗同理。
    /// </summary>
    public int[] TooltipCosts;
    /// <summary>
    /// 悬浮提示的扩展说明块 (原版 <c>CToolTipWar3Ex::SetTooltip</c> 的第 3 个参数, 0x6f3b3f40)。
    /// 数据来自悬停控件 <c>+0x578</c> 字段 (0x6f3a21fe 取址 → 0x6f3a2206 传参),
    /// 写入 <c>[+0x180]</c> 独立白色 FontString; 白色正文、独立换行、贴在描述下方 6.4。
    /// 由 <c>CommandSlot.Ext</c> 赋值 (建筑升级的"需要: X"走这里), 不再拼进
    /// <see cref="TooltipBody"/> 让整块内容共享同一个颜色。非空时面板宽度抬到 0.21 下限。
    /// </summary>
    public string TooltipExt;
    public bool PendingHighlight;   // 待定命令高亮
    bool _autoCastHighlight;
    double _autoCastStart=-1;
    public bool AutoCastHighlight
    {
        get=>_autoCastHighlight;
        set {if(value!=_autoCastHighlight)_autoCastStart=-1;_autoCastHighlight=value;}
    }
    public ITexture NormalTexture, DisabledTexture, HighlightTexture, PushedTexture;  // 4 状态背景 (TopBar 菜单按钮)
    public UiRect NormalUV, DisabledUV, HighlightUV, PushedUV;
    bool _pressed;
    readonly Dictionary<string, UiFrame> _controlVisuals = new();
    // Native quest buttons use the pushed/disabled-pushed backdrops as persistent
    // completed/failed states while the row remains selectable.
    public string BackdropState;
    public void SetControlVisual(string field, UiFrame frame)
    {
        _controlVisuals[field] = frame;
        UpdateControlVisuals();
    }

    void UpdateControlVisuals()
    {
        string backdrop = BackdropState ?? (Disabled
            ? _pressed ? "ControlDisabledPushedBackdrop" : "ControlDisabledBackdrop"
            : _pressed ? "ControlPushedBackdrop" : "ControlBackdrop");
        if (!_controlVisuals.ContainsKey(backdrop)) backdrop = "ControlBackdrop";
        foreach (var (field, frame) in _controlVisuals)
            frame.Visible = field == backdrop
                || field == "ControlMouseOverHighlight" && IsHovered && !Disabled;
    }

    public override void Position()
    {
        UpdateControlVisuals();
        base.Position();
    }
    public UiCommand? Command;       // 点击触发的命令
    public event Action<int> Clicked;

    public ButtonFrame(string name = null) : base(name)
    {
        MouseTransparent = false;
    }

    bool IClickable.Enabled => !Disabled && Enabled;

    // A slot is reused for units, abilities and items. Only an observed cooldown
    // ending on the same binding may start its completion animation.
    public void SetCooldown(float remaining,object owner,UiCommand? command,object content=null)
    {
        if(!ReferenceEquals(owner,_cooldownOwner)||!Nullable.Equals(command,_cooldownCommand)
            ||!ReferenceEquals(content,_cooldownContent))ClearCooldown();
        _cooldownOwner=owner;_cooldownCommand=command;_cooldownContent=content;
        for(UiFrame frame=this;frame!=null;frame=frame.Parent)
            if(!frame.Visible)
            {_cooldownSeen=false;_completionPending=false;_completionStart=-1;break;}
        remaining=Math.Clamp(remaining,0,1);
        if(remaining>0){_completionPending=false;_completionStart=-1;}
        else if(Cooldown>0&&_cooldownSeen)_completionPending=true;
        Cooldown=remaining;
    }

    public void ClearCooldown()
    {
        Cooldown=0;_cooldownSeen=false;_completionPending=false;_completionStart=-1;
        _cooldownOwner=null;_cooldownCommand=null;_cooldownContent=null;
    }

    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        UpdateControlVisuals();
        var b = VisualBounds(device);
        if (NormalTexture != null)
        {
            // 4 状态背景按钮: Disabled > Pushed > (Normal + ADD Highlight)
            if (Disabled && DisabledTexture != null)
                device.DrawTexturedQuad(b, DisabledTexture, Vector4.One, DisabledUV);
            else if (_pressed && PushedTexture != null)
                device.DrawTexturedQuad(b, PushedTexture, Vector4.One, PushedUV);
            else
            {
                device.DrawTexturedQuad(b, NormalTexture, Vector4.One, NormalUV);
                if (IsHovered && HighlightTexture != null)
                    device.DrawTexturedQuadAdd(b, HighlightTexture, Vector4.One, HighlightUV);
            }
        }
        // 命令卡/库存槽的边框来自 console 贴图, 不再自画纯色背板
        if (Icon != null)
        {
            // 按下时图标缩到 0.95 (对齐 Warsmash CommandCardIcon.mouseDown)
            var iconRect = _pressed
                ? new UiRect(b.X + b.Width * .025f, b.Y + b.Height * .025f, b.Width * .95f, b.Height * .95f)
                : b;
            // Native color binding targets the normal icon child (+15c),
            // while the disabled child (+158) keeps its own default tint.
            Vector4 tint=Disabled&&DisabledIcon!=null?Vector4.One
                :Disabled&&DimWhenDisabled?IconColor*new Vector4(0.55f,0.55f,0.55f,1f):IconColor;
            device.DrawTexturedQuad(iconRect, Disabled?DisabledIcon??Icon:Icon,tint);
        }
        if (IsHovered && ActiveHighlight != null)
            device.DrawTexturedQuadAdd(b, ActiveHighlight, Vector4.One);
        if (Cooldown > 0)
        {
            // Cooldown 为剩余比例，模型从 Stand 起点按已过去的比例推进。
            float ratio = Math.Clamp(Cooldown, 0, 1);
            device.DrawCooldownSweep(b, ratio, CooldownInnerRatio,
                new Vector4(0, 0, 0, CooldownShadeAlpha));
            _cooldownSeen=true;
        }
        else
        {
            if(_completionPending)
            {
                _completionPending=false;_cooldownSeen=false;
                _completionStart=device.AnimationTimeSeconds;
            }
            if(_completionStart>=0&&!device.DrawCooldownCompletion(b,
                (float)Math.Max(0,device.AnimationTimeSeconds-_completionStart)))_completionStart=-1;
        }
        if (PendingHighlight)
            device.DrawTexturedQuad(b, device.WhiteTexture, new Vector4(1f, 0.9f, 0.3f, 0.35f));
        if(AutoCastHighlight)
        {
            if(_autoCastStart<0)_autoCastStart=device.AnimationTimeSeconds;
            device.DrawAutoCast(b,this,(float)Math.Max(0,device.AnimationTimeSeconds-_autoCastStart));
        }
        // ★ 两套覆盖文本都保留(2026-10-14 合并): 我方「内置徽标」优先, 否则走上游的右对齐文本。
        //   两个分支互斥, 不会重复绘制。
        if(UsesBuiltInOverlayText && !string.IsNullOrWhiteSpace(OverlayText))
        {
            // 右下角小徽：宽 18 高 17，距右/底各 2px；数字在徽内居中（与攻/防等级一致）
            const float OverlayW = 18f, OverlayH = 17f, OverlayMargin = 2f;
            device.DrawText(new UiRect(b.Right - OverlayW - OverlayMargin,
                    b.Bottom - OverlayH - OverlayMargin, OverlayW, OverlayH),
                OverlayText, 14f, Vector4.One, TextAlignment.Center,
                new Vector4(0f, 0f, 0f, .95f), new Vector2(1f, 1f),
                TextVerticalAlignment.Middle);
        }
        else if(!string.IsNullOrWhiteSpace(OverlayText))
            device.DrawText(new UiRect(b.X,b.Bottom-19f,b.Width-3f,17f),OverlayText,
                14f,Vector4.One,TextAlignment.Right,
                new Vector4(0f,0f,0f,.95f),new Vector2(1f,1f),
                TextVerticalAlignment.Bottom);
        base.Render(device);
    }

    /// <summary>
    /// 实际绘制区域; 默认等于 RenderBounds。正方形控件(单选钮/勾选框)可重写它,
    /// 在屏幕空间保持正方形, 避免 4:3 UI 拉到 16:9 时圆形变椭圆。
    /// 命中与裁剪仍使用 RenderBounds。
    /// </summary>
    protected virtual UiRect VisualBounds(IUiRenderDevice device) => RenderBounds;

    public void OnPointerDown(Vector2 pos) => _pressed = true;
    public void OnPointerUp(Vector2 pos) => _pressed = false;
    public void OnClick(int button)
    {
        _pressed = false;
        if (Disabled) return;
        Clicked?.Invoke(button);
    }
}
