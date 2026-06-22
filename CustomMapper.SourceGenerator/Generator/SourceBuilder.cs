using System.Text;

namespace CustomMapper.SourceGenerator.Generator
{
    /// <summary>
    /// A tiny indentation-aware wrapper over <see cref="StringBuilder"/> for emitting C#.
    /// </summary>
    internal sealed class SourceBuilder
    {
        private readonly StringBuilder _sb = new();
        private int _indent;

        public SourceBuilder AppendLine(string line)
        {
            if (line.Length == 0)
            {
                _sb.Append('\n');
                return this;
            }

            for (int i = 0; i < _indent; i++) _sb.Append("    ");
            _sb.Append(line).Append('\n');
            return this;
        }

        public SourceBuilder AppendLine()
        {
            _sb.Append('\n');
            return this;
        }

        public SourceBuilder OpenBrace()
        {
            AppendLine("{");
            _indent++;
            return this;
        }

        public SourceBuilder CloseBrace()
        {
            _indent--;
            AppendLine("}");
            return this;
        }

        public SourceBuilder CloseBraceWithSemicolon()
        {
            _indent--;
            AppendLine("};");
            return this;
        }

        public override string ToString() => _sb.ToString();
    }
}
