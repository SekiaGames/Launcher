namespace OpenWC3.UI;

// FDF 递归下降语法分析: 文本 -> FrameDefinition 树
// 支持 IncludeFile (递归共享模板表) / StringList (装饰字符串) / INHERITS (模板继承)
// 语法:
//   file := (IncludeFile STRING | StringList {...} | frame)*
//   frame := IDENT STRING [INHERITS [WITHCHILDREN] STRING] "{" body "}"
//   body := (field | frame)*
//   field := IDENT value* ","?               // value = STRING | NUMBER | IDENT(枚举)
public sealed class FdfParser
{
    readonly List<FdfToken> _tokens;
    int _pos;
    readonly FrameTemplateEnvironment _env;
    readonly Func<string, string> _includeLoader;

    FdfParser(List<FdfToken> tokens, FrameTemplateEnvironment env, Func<string, string> includeLoader)
    {
        _tokens = tokens;
        _env = env ?? new FrameTemplateEnvironment();
        _includeLoader = includeLoader;
    }

    public static List<FrameDefinition> Parse(string source, FrameTemplateEnvironment env, Func<string, string> includeLoader)
    {
        var parser = new FdfParser(new FdfLexer(source).Tokenize(), env, includeLoader);
        var frames = new List<FrameDefinition>();
        while (parser.Peek().Kind != FdfTokenKind.EOF)
        {
            var t = parser.Peek();
            if (t.Kind == FdfTokenKind.Identifier && t.Text == "IncludeFile")
            {
                parser.Consume();
                if (parser.Peek().Kind == FdfTokenKind.String)
                {
                    string path = parser.Consume().Text;
                    if (parser.Peek().Kind == FdfTokenKind.Comma) parser.Consume();  // 消费可选逗号
                    if (includeLoader != null && env != null)
                    {
                        string inc = includeLoader(path);
                        if (inc != null) Parse(inc, env, includeLoader);  // 递归, 共享 env
                    }
                }
                continue;
            }
            if (t.Kind == FdfTokenKind.Identifier && t.Text == "StringList")
            {
                parser.ParseStringList();
                continue;
            }
            frames.Add(parser.ParseFrame());
        }
        return frames;
    }

    void ParseStringList()
    {
        Consume();  // StringList
        Expect(FdfTokenKind.OpenBrace);
        while (Peek().Kind != FdfTokenKind.CloseBrace && Peek().Kind != FdfTokenKind.EOF)
        {
            string id = Consume().Text;
            string value = Peek().Kind == FdfTokenKind.String ? Consume().Text : "";
            _env.AddDecoratedString(id, value);
            if (Peek().Kind == FdfTokenKind.Comma) Consume();
        }
        Expect(FdfTokenKind.CloseBrace);
    }

    FdfToken Peek(int offset = 0) =>
        _pos + offset < _tokens.Count ? _tokens[_pos + offset] : _tokens[^1];

    FdfToken Consume() => _tokens[_pos++];

    // 判断 body 内是否为子 frame:
    //   形式1: IDENT("Frame") STRING(class) STRING(name) [INHERITS [WITHCHILDREN] STRING] OpenBrace
    //   形式2: IDENT(class) STRING(name) [INHERITS [WITHCHILDREN] STRING] OpenBrace
    // Peek 自带越界钳制 (返回 EOF token), 无需手写下标检查
    bool IsChildFrame()
    {
        if (Peek().Kind != FdfTokenKind.Identifier) return false;
        int i = 1;
        if (Peek().Text == "Frame")
        {
            if (Peek(1).Kind != FdfTokenKind.String || Peek(2).Kind != FdfTokenKind.String) return false;
            i = 3;
        }
        // CLASS "NAME" ... (命名) 或 CLASS ... (匿名, 直接 INHERITS 或 {)
        else if (Peek(1).Kind == FdfTokenKind.String) i = 2;
        if (Peek(i).Kind == FdfTokenKind.Identifier && Peek(i).Text == "INHERITS")
        {
            i++;
            if (Peek(i).Kind == FdfTokenKind.Identifier && Peek(i).Text == "WITHCHILDREN") i++;
            if (Peek(i).Kind == FdfTokenKind.String) i++;
        }
        return Peek(i).Kind == FdfTokenKind.OpenBrace;
    }

    FrameDefinition ParseFrame()
    {
        var classTok = Consume();
        if (classTok.Kind != FdfTokenKind.Identifier)
            throw new InvalidDataException($"FDF: expected frame class at line {classTok.Line}, got {classTok.Kind} '{classTok.Text}'");

        string frameClass, name;
        if (classTok.Text == "Frame")
        {
            var classStr = Consume();
            var nameStr = Consume();
            frameClass = classStr.Text;
            name = nameStr.Text;
        }
        else
        {
            frameClass = classTok.Text;
            name = Peek().Kind == FdfTokenKind.String ? Consume().Text : null;  // 可选 name (匿名子帧)
        }

        string inherits = null;
        bool withChildren = false;
        if (Peek().Kind == FdfTokenKind.Identifier && Peek().Text == "INHERITS")
        {
            Consume();
            if (Peek().Kind == FdfTokenKind.Identifier && Peek().Text == "WITHCHILDREN")
            {
                Consume();
                withChildren = true;
            }
            inherits = Consume().Text;
        }
        Expect(FdfTokenKind.OpenBrace);

        var def = new FrameDefinition { FrameClass = frameClass, Name = name };
        // INHERITS: 先继承模板字段, 本帧字段后解析覆盖 (对齐 Warsmash inheritFrom)
        if (inherits != null)
        {
            var parent = _env.GetFrame(inherits);
            if (parent != null) def.InheritFrom(parent, withChildren);
        }

        while (Peek().Kind != FdfTokenKind.CloseBrace && Peek().Kind != FdfTokenKind.EOF)
        {
            if (IsChildFrame()) def.Children.Add(ParseFrame());
            else
            {
                var field = ParseField();
                def.Fields.Add(field);
                if (field.Values.Count == 0) def.Flags.Add(field.Key);  // 无值 flag (DecorateFileNames 等)
            }
        }
        Expect(FdfTokenKind.CloseBrace);
        _env.Put(name, def);
        return def;
    }

    FrameField ParseField()
    {
        var keyTok = Consume();
        if (keyTok.Kind != FdfTokenKind.Identifier)
            throw new InvalidDataException($"FDF: expected field name at line {keyTok.Line}, got {keyTok.Kind} '{keyTok.Text}'");
        var field = new FrameField { Key = keyTok.Text };
        while (true)
        {
            var t = Peek();
            if (t.Kind == FdfTokenKind.String || t.Kind == FdfTokenKind.Number)
                field.Values.Add(Consume().Text);
            else if (t.Kind == FdfTokenKind.Identifier && IsEnumValue(t.Text))
                field.Values.Add(Consume().Text);
            else if (t.Kind == FdfTokenKind.Comma)
                Consume();
            else
                break;
        }
        return field;
    }

    // 全大写标识符是枚举值 (LEFT/RIGHT/JUSTIFYLEFT/ALPHAKEY/TOPLEFT); CamelCase 是字段名
    static bool IsEnumValue(string s) =>
        s.Length > 0 && s.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_');

    void Expect(FdfTokenKind kind)
    {
        var t = Consume();
        if (t.Kind != kind)
            throw new InvalidDataException($"FDF: expected {kind} at line {t.Line}, got {t.Kind} '{t.Text}'");
    }
}
