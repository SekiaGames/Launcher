namespace OpenWC3.UI;

// FDF UI 工厂: 从资源读取 FDF -> 解析 (共享模板表 + include 递归) -> 实例化 UiFrame 树
// readText: FDF/TOC 路径 -> 文本 (null=不存在), 由 Game 层提供 (内置资源优先, 再查 MPQ)
public sealed class FdfUiFactory
{
    readonly IUiTextureResolver _textures;
    readonly Func<string, string> _readText;
    readonly FrameTemplateEnvironment _env = new();

    public FdfUiFactory(IUiTextureResolver textures, Func<string, string> readText)
    {
        _textures = textures;
        _readText = readText;
    }

    // 只解析进模板表, 不实例化。TOC 逐行一个 FDF 路径 (对齐 GameUI.loadTOCFile)。
    // 后加载者覆盖同名模板 —— 这是内置 SmashUI 覆盖 Blizzard 版本的机制。
    public int ParseToc(string tocPath)
    {
        string toc = _readText(tocPath);
        if (string.IsNullOrEmpty(toc)) return 0;
        int loaded = 0;
        foreach (string line in toc.Split('\n'))
        {
            string path = line.Trim().Trim('\r');
            if (path.Length == 0 || path.StartsWith("//")) continue;
            string source = _readText(path);
            if (string.IsNullOrEmpty(source)) continue;
            try { FdfParser.Parse(source, _env, _readText); loaded++; }
            catch (Exception ex) { RuntimeDiagnostics.WriteException($"FDF 解析 {path}", ex); }
        }
        return loaded;
    }

    // 从已解析的模板表按名实例化 (对齐 GameUI.createFrame/createSimpleFrame)
    // 模板缺失时返回 null, 由调用方决定回退
    public UiFrame Create(string templateName, UiFrame parent = null) =>
        Create(templateName, parent, null);

    /// <summary>
    /// 按名克隆一份模板。suffix 用来隔离同模板的多次克隆 (原版把同一个
    /// QuestListItem 模板克隆成列表里的每一项), 见 <see cref="FdfInflator.NameSuffix"/>。
    /// </summary>
    public UiFrame Create(string templateName, UiFrame parent, string suffix)
    {
        var def = _env.GetFrame(templateName);
        if (def == null) return null;
        return new FdfInflator(_textures, _env)
            { NameSuffix = suffix ?? "", ExternalFrameResolver = parent == null ? null : parent.FindByName }
            .Inflate(def, parent);
    }

    /// <summary>模板是否已加载 (缺失时 Create 返回 null)</summary>
    public bool Has(string templateName) => _env.GetFrame(templateName) != null;

    /// <summary>
    /// 解析一个 FDF 模板文件, 把其中的帧定义与 StringList 注册进共享环境。
    /// 原版 UI 的做法就是"用哪张面板就加载那张 fdf", 例如
    /// <c>UI\FrameDef\QuestDialog.fdf</c>、<c>UI\FrameDef\EscMenu\EscMenuMainPanel.fdf</c>。
    /// IncludeFile 递归由 <see cref="FdfParser"/> 处理, 共享同一个 env。
    /// </summary>
    public bool LoadTemplate(string path)
    {
        string source = _readText(path);
        if (string.IsNullOrEmpty(source)) return false;
        FdfParser.Parse(source, _env, _readText);
        return true;
    }

    /// <summary>
    /// 载入 <c>StringList</c> 装饰字符串表。原版 UI 文本全部走这张表
    /// (UI\FrameDef\GlobalStrings.fdf), 帧定义里的 <c>Text "QUESTS"</c> 之类
    /// 只是键, 真正文案由表提供; 换语言包时也只需换这张表。
    /// </summary>
    public int LoadStrings(string path)
    {
        string source = _readText(path);
        if (string.IsNullOrEmpty(source)) return 0;
        return FdfParser.Parse(source, _env, _readText).Count;
    }

    /// <summary>查装饰字符串; 表里没有该键时返回 fallback</summary>
    public string Localized(string key, string fallback = null)
    {
        string value = _env?.GetDecoratedString(key);
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    // 仅解析 (不实例化), 供调试/测试
    public List<FrameDefinition> Parse(string path)
    {
        string source = _readText(path);
        return string.IsNullOrEmpty(source) ? new List<FrameDefinition>()
            : FdfParser.Parse(source, _env, _readText);
    }
}
