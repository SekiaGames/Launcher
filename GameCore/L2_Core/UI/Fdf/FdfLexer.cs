namespace OpenWC3.UI;

// FDF 词法 token
public enum FdfTokenKind { Identifier, String, Number, Comma, OpenBrace, CloseBrace, EOF }

public readonly record struct FdfToken(FdfTokenKind Kind, string Text, int Line);

// FDF 词法分析: 跳过空白与注释 (// 行注释, /* */ 块注释)
public sealed class FdfLexer
{
    readonly string _src;
    int _pos, _line = 1;

    public FdfLexer(string source) => _src = source ?? "";

    public List<FdfToken> Tokenize()
    {
        var tokens = new List<FdfToken>();
        FdfToken t;
        do { t = Next(); tokens.Add(t); } while (t.Kind != FdfTokenKind.EOF);
        return tokens;
    }

    FdfToken Next()
    {
        while (_pos < _src.Length)
        {
            char c = _src[_pos];
            if (c == '\n') { _line++; _pos++; continue; }
            if (char.IsWhiteSpace(c)) { _pos++; continue; }
            if (c == '/' && _pos + 1 < _src.Length)
            {
                if (_src[_pos + 1] == '/') { SkipLineComment(); continue; }
                if (_src[_pos + 1] == '*') { SkipBlockComment(); continue; }
            }
            break;
        }
        if (_pos >= _src.Length) return new(FdfTokenKind.EOF, "", _line);

        char ch = _src[_pos];
        if (ch == '{') { _pos++; return new(FdfTokenKind.OpenBrace, "{", _line); }
        if (ch == '}') { _pos++; return new(FdfTokenKind.CloseBrace, "}", _line); }
        if (ch == ',') { _pos++; return new(FdfTokenKind.Comma, ",", _line); }
        if (ch == '"') return ReadString();
        if (char.IsDigit(ch) || (ch == '-' && _pos + 1 < _src.Length && char.IsDigit(_src[_pos + 1]))
            || (ch == '.' && _pos + 1 < _src.Length && char.IsDigit(_src[_pos + 1])))
            return ReadNumber();
        if (char.IsLetter(ch) || ch == '_') return ReadIdentifier();

        // 未知字符: 当作单字符标识符跳过
        _pos++;
        return Next();
    }

    void SkipLineComment()
    {
        _pos += 2;
        while (_pos < _src.Length && _src[_pos] != '\n') _pos++;
    }

    void SkipBlockComment()
    {
        _pos += 2;
        while (_pos + 1 < _src.Length)
        {
            if (_src[_pos] == '*' && _src[_pos + 1] == '/') { _pos += 2; return; }
            if (_src[_pos] == '\n') _line++;
            _pos++;
        }
        _pos = _src.Length;
    }

    FdfToken ReadString()
    {
        _pos++; // 跳过开引号
        int start = _pos;
        var sb = new System.Text.StringBuilder();
        while (_pos < _src.Length && _src[_pos] != '"')
        {
            sb.Append(_src[_pos]);
            _pos++;
        }
        _pos++; // 跳过闭引号
        return new(FdfTokenKind.String, sb.ToString(), _line);
    }

    FdfToken ReadNumber()
    {
        int start = _pos;
        if (_src[_pos] == '-') _pos++;
        while (_pos < _src.Length && (char.IsDigit(_src[_pos]) || _src[_pos] == '.')) _pos++;
        // Authored FDF floats use a C-style suffix, including every component
        // of BackdropBackgroundInsets. Keep it in the numeric token so it does
        // not become a new field and split the remaining components.
        if (_pos < _src.Length && (_src[_pos] == 'f' || _src[_pos] == 'F')) _pos++;
        return new(FdfTokenKind.Number, _src[start.._pos], _line);
    }

    FdfToken ReadIdentifier()
    {
        int start = _pos;
        while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_')) _pos++;
        return new(FdfTokenKind.Identifier, _src[start.._pos], _line);
    }
}
