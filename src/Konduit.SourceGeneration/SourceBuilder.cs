using System.Text;

namespace Konduit.SourceGeneration;

/// <summary>A small indentation-aware writer, so emitted code is readable when you go looking at it.</summary>
internal sealed class SourceBuilder
{
    private readonly StringBuilder _builder = new();
    private int _indent;

    public SourceBuilder Line(string text = "")
    {
        if (text.Length > 0)
        {
            _builder.Append(' ', _indent * 4).Append(text);
        }

        _builder.Append('\n');
        return this;
    }

    /// <summary>Writes a header line followed by an opening brace, and indents.</summary>
    public SourceBuilder Open(string header) => Line(header).OpenBlock();

    /// <summary>Writes an opening brace on its own line, and indents.</summary>
    public SourceBuilder OpenBlock()
    {
        Line("{");
        _indent++;
        return this;
    }

    public SourceBuilder Close(string suffix = "")
    {
        _indent--;
        Line("}" + suffix);
        return this;
    }

    public override string ToString() => _builder.ToString();
}
