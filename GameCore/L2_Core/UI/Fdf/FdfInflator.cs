using System.Numerics;
using OpenWC3.RHI;

namespace OpenWC3.UI;

// FDF Inflator: FrameDefinition -> UiFrame 树 (对齐 Warsmash GameUI.inflate)
// 两遍: 先实例化+注册名+应用非锚点字段, 再应用 SetPoint (允许前向引用兄弟帧)
public sealed class FdfInflator
{
    readonly IUiTextureResolver _textures;
    readonly FrameTemplateEnvironment _env;
    readonly Dictionary<string, UiFrame> _byName = new(StringComparer.Ordinal);
    readonly List<(UiFrame frame, FrameDefinition def)> _pendingSetPoints = new();
    readonly List<(UiFrame owner, string childName)> _pendingButtonText = new();
    readonly List<(ButtonFrame button, string field, string childName)> _pendingButtonVisuals = new();
    // 原版 FDF 用"字段值 = 子帧名"表达控件从属关系, 子帧此刻尚未实例化,
    // 因此与 SetPoint 同批延到 Inflate 尾部解析 (对齐 Warsmash inflate 的后置解析)。
    readonly List<(ScrollBarFrame bar, string thumbName)> _pendingThumb = new();
    readonly List<(TextAreaFrame area, string scrollName)> _pendingAreaScroll = new();
    readonly List<(CheckBoxFrame box, string checkName)> _pendingBoxCheck = new();
    // DIALOG 的 DialogBackdrop 字段指向默认背景背板；这些背板自身通常不写
    // SetAllPoints（AllianceDialog/GameResultDialog/ScriptDialog/SuspendDialog），
    // 原版 inflate 会把它铺满整个对话框。不处理会得到 0x0 背板，面板只剩按钮可见。
    readonly List<(UiFrame dialog, string backdropName)> _pendingDialogBackdrops = new();

    public FdfInflator(IUiTextureResolver textures, FrameTemplateEnvironment env = null)
    {
        _textures = textures;
        _env = env;
    }

    /// <summary>
    /// 实例后缀。同一个 FDF 模板会被原版反复克隆成列表项 (QuestListItem / QuestItemListItem),
    /// 若所有实例沿用模板原名, 按名查找就会全部撞到第一项。后缀让每次克隆得到独立的
    /// 名字空间, 而 FDF 内部的 SetPoint / SliderThumbButtonFrame 等引用仍按原名解析。
    /// </summary>
    public string NameSuffix { get; set; } = "";
    public Func<string, UiFrame> ExternalFrameResolver { get; set; }

    string N(string defName) =>
        string.IsNullOrEmpty(defName) || string.IsNullOrEmpty(NameSuffix)
            ? defName
            : defName + NameSuffix;

    UiFrame ResolveFrame(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_byName.TryGetValue(N(name), out UiFrame own)) return own;
        return _byName.TryGetValue(name, out UiFrame shared) ? shared
            : ExternalFrameResolver?.Invoke(name);
    }

    T ResolveChild<T>(string name) where T : UiFrame => ResolveFrame(name) as T;

    public UiFrame Inflate(FrameDefinition def, UiFrame parent = null)
    {
        var frame = Instantiate(def, parent);
        foreach (var (f, d) in _pendingSetPoints) ApplySetPoints(d, f);
        // ButtonText 引用的子文字帧铺满按钮 (Warsmash 按钮文本机制)
        foreach (var (button, childName) in _pendingButtonText)
        {
            if (button == null) continue;
            if (ResolveFrame(childName) is not { } text) continue;
            text.SetPoints.Clear();
            text.SetAllPoints(button);
        }
        _pendingButtonText.Clear();
        foreach (var (button, field, childName) in _pendingButtonVisuals)
        {
            if (ResolveFrame(childName) is not { } visual) continue;
            visual.SetPoints.Clear();
            visual.SetAllPoints(button);
            button.SetControlVisual(field, visual);
        }
        _pendingButtonVisuals.Clear();
        // SliderThumbButtonFrame: 拇指必须实现 IDraggable, 否则按下后移出拇指就断了
        foreach (var (bar, thumbName) in _pendingThumb)
        {
            if (bar == null) continue;
            if (bar.Thumb == null)
                bar.Thumb = ResolveChild<ButtonFrame>(thumbName);
            // FDF 只声明普通 BUTTON 拇指时无法原地换型, 退化为"整条轨道可拖"
            if (bar.Thumb is not ScrollThumbButton && bar.Thumb != null) bar.Thumb = null;
            bar.WireUp();
        }
        _pendingThumb.Clear();
        foreach (var (area, scrollName) in _pendingAreaScroll)
        {
            if (area == null) continue;
            area.AttachScrollBar(ResolveChild<ScrollBarFrame>(scrollName));
        }
        _pendingAreaScroll.Clear();
        foreach (var (box, checkName) in _pendingBoxCheck)
        {
            if (box == null) continue;
            box.CheckHighlightTexture = ResolveChild<TextureFrame>(checkName)?.Texture;
        }
        _pendingBoxCheck.Clear();
        // DialogBackdrop: 原版 DIALOG 默认背景。背板声明的子帧必须铺满对话框。
        foreach (var (dialog, backdropName) in _pendingDialogBackdrops)
        {
            if (dialog == null) continue;
            if (ResolveFrame(backdropName) is not { } backdrop) continue;
            backdrop.SetPoints.Clear();
            backdrop.SetAllPoints(dialog);
        }
        _pendingDialogBackdrops.Clear();
        return frame;
    }

    UiFrame Instantiate(FrameDefinition def, UiFrame parent)
    {
        var frame = CreateByClass(def.FrameClass, parent);
        frame.Name = N(def.Name);
        // 按钮类帧的子帧(背板/文字/高亮)全是视觉件, 不能拦截命中测试——
        // 否则逆序命中先撞上 BackdropTemplate, 点击永远落不到按钮本身
        if (parent is ButtonFrame) frame.MouseTransparent = true;
        if (parent != null) parent.AddChild(frame);
        if (!string.IsNullOrEmpty(frame.Name)) _byName[frame.Name] = frame;
        ApplyCommonFields(frame, def);
        ApplyTypeFields(frame, def);
        // 原版 FDF 的文本帧尺寸由字体度量自动决定 —— 模板通常不写 Width/Height
        // (如 EscMenuLabelTextTemplate / QuestDetailsTitle 都没写)。不补这个估算,
        // 只锚一点的文本帧会解出 0x0, 标题/列表项文字整条不可见, 还会把挂在
        // 它下面的子树一起带偏 (QuestDetailsTitle 塌成 0 -> QuestItemListContainer
        // 跟着错 -> QuestDisplay 跟着错)。双锚(可拉伸)的帧不受影响:
        // SolveAxis 在 min+max 都存在时直接用跨度, 忽略这里的 size。
        if (frame is StringFrame text && text.Width == null)
            text.Width = EstimateTextWidth(text.Text, text.FontSize);
        if (frame is StringFrame text2 && text2.Height == null)
            text2.Height = MathF.Max(text2.FontSize * 1.4f, 8f);
        foreach (var childDef in def.Children) Instantiate(childDef, frame);
        _pendingSetPoints.Add((frame, def));
        return frame;
    }

    UiFrame CreateByClass(string frameClass, UiFrame parent = null) =>
        frameClass.ToUpperInvariant() switch
        {
            "BACKDROP" => new BackdropFrame(),
            // 原版 UI 的文字层从不单独接收鼠标: 真正的交互由 GLUEBUTTON/BUTTON 承担,
            // 按钮文字只是子帧。FdfInflator 创建的 TEXT/STRING 必须默认穿透,
            // 否则 ConsoleUI / EscMenu 里成片的按钮文字会盖在屏幕中央, 把那一大片
            // 区域的点击全部吃掉 (实机症状: 菜单文字浮屏且完全无法操作)。
            "TEXT" or "STRING" => new StringFrame { MouseTransparent = true },
            "TEXTURE" => new TextureFrame(),
            // HIGHLIGHT 帧承载 HighlightAlphaFile 贴图, 由按钮悬停机制驱动, 默认隐藏
            "HIGHLIGHT" => new TextureFrame() { Visible = false },
            "SCROLLBAR" or "SLIDER" => new ScrollBarFrame(),
            "TEXTAREA" => new TextAreaFrame(),
            // 滚动条的拇指按钮要额外实现 IDraggable, 才能在移出拇指后继续跟随
            "BUTTON" => parent is ScrollBarFrame ? new ScrollThumbButton() : new ButtonFrame(),
            "GLUEBUTTON" or "TEXTBUTTON" or "SIMPLEBUTTON" or "GLUETEXTBUTTON"
                => new ButtonFrame(),
            "CHECKBOX" or "GLUECHECKBOX" => new CheckBoxFrame(),
            "STATUSBAR" or "SIMPLESTATUSBAR" => new StatusBarFrame(),
            _ => new UiContainer(),
        };

    void ApplyCommonFields(UiFrame frame, FrameDefinition def)
    {
        foreach (var f in def.Fields)
        {
            switch (f.Key)
            {
                case "Width": frame.Width = FdfScale.S(f.ValueFloat()); break;
                case "Height": frame.Height = FdfScale.S(f.ValueFloat()); break;
                case "SetAllPoints": frame.SetAllPoints(); break;
                case "Hidden": frame.Visible = false; break;
                case "Visible": frame.Visible = f.Values.Count == 0 || f.ValueText != "0"; break;
                case "DialogBackdrop" when f.Values.Count >= 1:
                    _pendingDialogBackdrops.Add((frame, f.ValueText));
                    break;
            }
        }
    }

    void ApplyTypeFields(UiFrame frame, FrameDefinition def)
    {
        switch (frame)
        {
            case TextureFrame tex: ApplyTexture(tex, def); break;
            case BackdropFrame bd: ApplyBackdrop(bd, def); break;
            case StringFrame str: ApplyString(str, def); break;
            case TextAreaFrame area: ApplyTextArea(area, def); break;
            case StatusBarFrame bar: ApplyStatusBar(bar, def); break;
            case ScrollBarFrame scroll: ApplyScrollBar(scroll, def); break;
            case CheckBoxFrame box: ApplyCheckBox(box, def); break;
            case ButtonFrame btn: ApplyButton(btn, def); break;
        }
    }

    // SCROLLBAR/SLIDER: 布局方向 + 拇指子帧归属。轨道背板本身就是普通 BACKDROP
    // 子帧, 由 Instantiate 递归建好, 这里只处理方向与拇指绑定。
    void ApplyScrollBar(ScrollBarFrame scroll, FrameDefinition def)
    {
        foreach (var flag in def.Flags)
        {
            if (flag.Equals("SliderLayoutVertical", StringComparison.OrdinalIgnoreCase))
                scroll.Vertical = true;
            else if (flag.Equals("SliderLayoutHorizontal", StringComparison.OrdinalIgnoreCase))
                scroll.Vertical = false;
        }
        foreach (var f in def.Fields)
            if (f.Key == "SliderThumbButtonFrame" && f.Values.Count >= 1)
                _pendingThumb.Add((scroll, f.ValueText));
            else if (f.Key == "ControlBackdrop" && f.Values.Count == 1)
                _pendingButtonText.Add((scroll, f.ValueText));
    }

    // TEXTAREA: 原版按固定行高排版, 超出 MaxLines 出行滚动条
    void ApplyTextArea(TextAreaFrame area, FrameDefinition def)
    {
        foreach (var f in def.Fields)
        {
            switch (f.Key)
            {
                case "TextAreaLineGap": area.LineGap = FdfScale.S(f.ValueFloat()); break;
                case "TextAreaLineHeight": area.LineHeight = FdfScale.S(f.ValueFloat()); break;
                case "TextAreaMaxLines": area.MaxLines = (int)f.ValueFloat(); break;
                case "TextAreaInset": area.Inset = FdfScale.S(f.ValueFloat()); break;
                case "TextAreaScrollBar" when f.Values.Count >= 1:
                    _pendingAreaScroll.Add((area, f.ValueText));
                    break;
                case "Text" when f.Values.Count >= 1:
                    area.SetText(f.ValueText);
                    break;
                case "FrameFont" when f.Values.Count >= 2:
                case "Font" when f.Values.Count >= 2:
                    area.FontSize = FdfScale.S(f.ValueFloat(1)); break;
                case "FontColor" when f.Values.Count >= 4:
                    area.Color = new Vector4(f.ValueFloat(0), f.ValueFloat(1),
                        f.ValueFloat(2), f.ValueFloat(3));
                    break;
                case "FontJustificationH":
                    area.Alignment = f.ValueText?.ToUpperInvariant() switch
                    {
                        "JUSTIFYLEFT" => TextAlignment.Left,
                        "JUSTIFYCENTER" or "JUSTIFYHCENTER" => TextAlignment.Center,
                        "JUSTIFYRIGHT" => TextAlignment.Right,
                        _ => area.Alignment
                    };
                    break;
                case "FontShadowColor" when f.Values.Count >= 4:
                    area.FontShadowColor = new Vector4(f.ValueFloat(0), f.ValueFloat(1),
                        f.ValueFloat(2), f.ValueFloat(3));
                    break;
                case "FontShadowOffset" when f.Values.Count >= 2:
                    area.FontShadowOffset = new Vector2(FdfScale.S(f.ValueFloat(0)),
                        FdfScale.S(f.ValueFloat(1)));
                    break;
            }
        }
    }

    void ApplyCheckBox(CheckBoxFrame box, FrameDefinition def)
    {
        foreach (var f in def.Fields)
            if (f.Key == "CheckBoxCheckHighlight" && f.Values.Count >= 1)
                _pendingBoxCheck.Add((box, f.ValueText));
    }

    // SIMPLEBUTTON 的四态贴图是字段而非子帧 (对齐 GameUI.inflate SIMPLEBUTTON 分支)
    // NormalText/ButtonText 是 (模板名, 文本键) 字符串对, 文本键经装饰字符串表解析
    void ApplyButton(ButtonFrame btn, FrameDefinition def)
    {
        string label = null;
        foreach (var f in def.Fields)
        {
            switch (f.Key)
            {
                // 四态贴图的值是文件作用域 Texture 模板名 (带 File + TexCoord), 不是路径
                case "NormalTexture":
                    (btn.NormalTexture, btn.NormalUV) = ResolveTextureRef(f.ValueText); break;
                case "PushedTexture":
                    (btn.PushedTexture, btn.PushedUV) = ResolveTextureRef(f.ValueText); break;
                case "DisabledTexture":
                    (btn.DisabledTexture, btn.DisabledUV) = ResolveTextureRef(f.ValueText); break;
                case "UseHighlight":
                case "ControlMouseOverHighlight" when IsSimpleButton(def.FrameClass):
                    (btn.HighlightTexture, btn.HighlightUV) = ResolveTextureRef(f.ValueText); break;
                case "ControlShortcutKey" when f.Values.Count > 0: btn.Hotkey = f.ValueText; break;
                // 单值 ButtonText 是已有子文字帧的引用: 记下来延迟铺满按钮 (子帧
                // 此时尚未实例化, 与 SetPoint 同批在 Inflate 尾部处理)。
                case "ButtonText" when f.Values.Count == 1:
                    _pendingButtonText.Add((btn, f.ValueText)); break;
                // 模板按钮的 ControlXxx 是子背板帧引用, 同 ButtonText 机制铺满按钮
                case "ControlBackdrop" when f.Values.Count == 1:
                case "ControlPushedBackdrop" when f.Values.Count == 1:
                case "ControlDisabledBackdrop" when f.Values.Count == 1:
                case "ControlDisabledPushedBackdrop" when f.Values.Count == 1:
                case "ControlMouseOverHighlight" when f.Values.Count == 1:
                case "ControlFocusHighlight" when f.Values.Count == 1:
                    _pendingButtonVisuals.Add((btn, f.Key, f.ValueText)); break;
                // 只有模板同时给出文字键时才补一个合成 StringFrame。
                case "ButtonText" when f.Values.Count > 1: label = f.Values[^1]; break;
                case "NormalText" when f.Values.Count > 1: label ??= f.Values[^1]; break;
            }
        }
        if (string.IsNullOrWhiteSpace(label)) return;
        string text = _env?.GetDecoratedString(label) ?? label;
        if (string.IsNullOrWhiteSpace(text)) return;
        var strFrame = new StringFrame(N(def.Name + "Text"))
        {
            Text = text,
            Alignment = TextAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Middle,
            MouseTransparent = true,
            FontShadowColor = new Vector4(0, 0, 0, .9f),
        };
        btn.AddChild(strFrame);
        if (!string.IsNullOrEmpty(strFrame.Name)) _byName[strFrame.Name] = strFrame;
        strFrame.SetAllPoints();
    }

    // 引用名先按 Texture 模板查 (取它的 File + TexCoord), 查不到再当成贴图路径
    static bool IsSimpleButton(string frameClass) =>
        frameClass?.Equals("SIMPLEBUTTON", StringComparison.OrdinalIgnoreCase) == true;
    (ITexture Texture, UiRect Uv) ResolveTextureRef(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return (null, default);
        FrameDefinition def = _env?.GetFrame(reference);
        if (def == null) return (_textures.Resolve(reference), default);
        ITexture texture = null;
        UiRect uv = default;
        foreach (var f in def.Fields)
        {
            if (f.Key == "File") texture = _textures.Resolve(f.ValueText);
            else if (f.Key == "TexCoord" && f.Values.Count >= 4) uv = TexCoord(f);
        }
        return (texture ?? _textures.Resolve(reference), uv);
    }

    // FDF TexCoord 顺序是 (minU, maxU, minV, maxV)
    static UiRect TexCoord(FrameField f) => new(
        f.ValueFloat(0), f.ValueFloat(2),
        f.ValueFloat(1) - f.ValueFloat(0), f.ValueFloat(3) - f.ValueFloat(2));

    void ApplyTexture(TextureFrame tex, FrameDefinition def)
    {
        foreach (var f in def.Fields)
        {
            switch (f.Key)
            {
                case "File": tex.Texture = _textures.Resolve(f.ValueText); break;
                case "HighlightAlphaFile": tex.Texture = _textures.Resolve(f.ValueText); break;
                case "HighlightAlphaMode": tex.Additive = f.ValueText == "ADD"; break;
                case "TexCoord" when f.Values.Count >= 4: tex.UV = TexCoord(f); break;
            }
        }
    }

    void ApplyBackdrop(BackdropFrame bd, FrameDefinition def)
    {
        foreach (var f in def.Fields)
        {
            switch (f.Key)
            {
                case "BackdropBackground": bd.BackgroundTexture = _textures.Resolve(f.ValueText); break;
                case "BackdropEdgeFile": bd.EdgeTexture = _textures.Resolve(f.ValueText); break;
                case "BackdropCornerFile": bd.EdgeTexture = _textures.Resolve(f.ValueText); break;
                case "BackdropCornerSize": bd.CornerSize = FdfScale.S(f.ValueFloat()); break;
                case "BackdropBackgroundSize": bd.BackgroundSize = FdfScale.S(f.ValueFloat()); break;
                case "BackdropBackgroundInsets" when f.Values.Count >= 4:
                    // FDF (x,y,z,w)=(Left,Bottom,Right,Top) -> OpenWC3 Insets=(Left,Top,Right,Bottom)
                    bd.Insets = new UiRect(FdfScale.S(f.ValueFloat(0)), FdfScale.S(f.ValueFloat(3)),
                        FdfScale.S(f.ValueFloat(2)), FdfScale.S(f.ValueFloat(1)));
                    break;
                case "BackdropTileBackground": bd.TileBackground = true; break;
                case "BackdropMirrored": bd.Mirrored = true; break;
                case "BackdropCornerFlags":
                    bd.CornerFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var v in f.Values)
                        foreach (var part in v.Split('|', StringSplitOptions.RemoveEmptyEntries))
                            bd.CornerFlags.Add(part.Trim());
                    break;
            }
        }
    }

    // 文本宽度估算: CJK/全角按 1 个字宽, 其余按 0.55 字宽。
    // 对齐原版"文本帧按字体与内容自动定宽"的行为 (FDF 里没有 Width 时)。
    internal static float EstimateTextWidth(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text)) return MathF.Max(fontSize * 4f, 16f);
        float units = 0f;
        foreach (char c in text) units += c > 0x2E80 ? 1f : .55f;
        return MathF.Max(units * fontSize, 16f);
    }

    void ApplyString(StringFrame str, FrameDefinition def)
    {
        foreach (var f in def.Fields)
        {
            switch (f.Key)
            {
                case "Text": str.Text = Localize(f.ValueText ?? ""); break;
                case "FontJustificationV":
                    str.VerticalAlignment = f.ValueText?.ToUpperInvariant() switch
                    {
                        "JUSTIFYTOP" => TextVerticalAlignment.Top,
                        "JUSTIFYMIDDLE" or "JUSTIFYVCENTER" => TextVerticalAlignment.Middle,
                        "JUSTIFYBOTTOM" => TextVerticalAlignment.Bottom,
                        _ => str.VerticalAlignment
                    };
                    break;
                case "FontColor" when f.Values.Count >= 4:
                    str.Color = new Vector4(f.ValueFloat(0), f.ValueFloat(1), f.ValueFloat(2), f.ValueFloat(3));
                    break;
                case "FontDisabledColor" when f.Values.Count >= 4:
                    str.DisabledColor = new Vector4(f.ValueFloat(0), f.ValueFloat(1), f.ValueFloat(2), f.ValueFloat(3));
                    break;
                case "FontJustificationOffset" when f.Values.Count >= 2:
                    str.JustificationOffset = Offset(f.ValueFloat(0), f.ValueFloat(1));
                    break;
                // FDF 字号是 0.8x0.6 比例值 (如 0.010), 需转成设计单位
                case "FrameFont" when f.Values.Count >= 2:
                    str.FontSize = FdfScale.S(f.ValueFloat(1)); break;
                case "Font" when f.Values.Count >= 2:
                    str.FontSize = FdfScale.S(f.ValueFloat(1)); break;
                // ResourceBarTextTemplate 等原版模板只声明 TextLength，不声明
                // Width。按字符容量给出稳定布局宽度，等价于参考实现渲染前的
                // 字体度量，避免零宽裁剪或借用 Upkeep 宽度后覆盖图标。
                case "TextLength" when f.Values.Count > 0:
                    str.Width=Math.Max(str.Width??0f,
                        Math.Max(1f,f.ValueFloat())*Math.Max(1f,str.FontSize)*.62f);
                    break;
                case "FontJustificationH":
                    str.Alignment = f.ValueText?.ToUpperInvariant() switch
                    {
                        "JUSTIFYLEFT" => TextAlignment.Left,
                        "JUSTIFYCENTER" or "JUSTIFYHCENTER" => TextAlignment.Center,
                        "JUSTIFYRIGHT" => TextAlignment.Right,
                        _ => str.Alignment
                    };
                    break;
                case "FontShadowColor" when f.Values.Count >= 4:
                    str.FontShadowColor = new Vector4(f.ValueFloat(0), f.ValueFloat(1), f.ValueFloat(2), f.ValueFloat(3)); break;
                case "FontShadowOffset" when f.Values.Count >= 2:
                    str.FontShadowOffset = Offset(f.ValueFloat(0), f.ValueFloat(1)); break;
            }
        }
    }

    static void ApplyStatusBar(StatusBarFrame bar, FrameDefinition def)
    {
        foreach (var f in def.Fields)
        {
            switch (f.Key)
            {
                case "StatusBarColor" when f.Values.Count >= 3:
                    bar.FillColor = new Vector4(f.ValueFloat(0), f.ValueFloat(1), f.ValueFloat(2), 1f); break;
            }
        }
    }

    void ApplySetPoints(FrameDefinition def, UiFrame frame)
    {
        foreach (var f in def.Fields)
        {
            switch (f.Key)
            {
                // 原版 FDF 里偏移常写成 "0.002f" (带 f 后缀) 且末尾多一个逗号, 两种写法都
                // 只给到 3 个值 (锚点+目标点), 偏移可缺省 —— 所以门槛必须是 3 而不是 5,
                // 否则 QuestDetailsTitle 这类帧会整条丢掉 SetPoint 而塌成 0x0。
                case "SetPoint" when f.Values.Count >= 3:
                    {
                        var self = ParsePoint(f.Values[0]);
                        var target = ResolveFrame(f.Values[1]) ?? frame.Parent;
                        var targetPoint = ParsePoint(f.Values[2]);
                        frame.SetPoint(self, target, targetPoint,
                            Offset(f.ValueFloat(3), f.ValueFloat(4)));
                    }
                    break;
                case "Anchor" when f.Values.Count >= 3:
                    {
                        var self = ParsePoint(f.Values[0]);
                        frame.SetPoint(self, frame.Parent, self,
                            Offset(f.ValueFloat(1), f.ValueFloat(2)));
                    }
                    break;
            }
        }
    }

    // FDF 的 +Y 朝上, 设计空间 +Y 朝下, 故纵向偏移取反
    // (对齐 open-realm UI_NormalizeAnchorOffset: is_x ? offset : -offset)
    static Vector2 Offset(float x, float y) => new(FdfScale.S(x), -FdfScale.S(y));

    static FramePoint ParsePoint(string s) => s.ToUpperInvariant() switch
    {
        "CENTER" => FramePoint.Center,
        "TOPLEFT" => FramePoint.TopLeft,
        "TOP" => FramePoint.Top,
        "TOPRIGHT" => FramePoint.TopRight,
        "LEFT" => FramePoint.Left,
        "RIGHT" => FramePoint.Right,
        "BOTTOMLEFT" => FramePoint.BottomLeft,
        "BOTTOM" => FramePoint.Bottom,
        "BOTTOMRIGHT" => FramePoint.BottomRight,
        _ => FramePoint.Center,
    };

    // FDF 文本键 -> 实际文本: 走 StringList/GlobalStrings 装饰字符串表 (对齐 UI_GetString)
    // 表里查不到再退到最小内置映射
    string Localize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string resolved = _env?.GetDecoratedString(text);
        if (!string.IsNullOrEmpty(resolved) && resolved != text) return resolved;
        return text switch
        {
            "COLON_DAMAGE" => "Damage:",
            "COLON_ARMOR" => "Armor:",
            "COLON_SPEED" => "Speed:",
            "COLON_RANGE" => "Range:",
            _ => text,
        };
    }
}
