using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CustomMapper.SourceGenerator.Generator
{
    /// <summary>
    /// A serializable, value-equatable stand-in for a <see cref="Diagnostic"/>, so diagnostics
    /// can travel through the incremental pipeline without pinning symbols or syntax.
    /// </summary>
    internal sealed class EquatableDiagnostic : IEquatable<EquatableDiagnostic>
    {
        private readonly DiagnosticDescriptor _descriptor;
        private readonly LocationInfo? _location;
        private readonly EquatableArray<string> _messageArgs;

        private EquatableDiagnostic(DiagnosticDescriptor descriptor, LocationInfo? location, EquatableArray<string> messageArgs)
        {
            _descriptor = descriptor;
            _location = location;
            _messageArgs = messageArgs;
        }

        public static EquatableDiagnostic Create(DiagnosticDescriptor descriptor, ISymbol symbol, params string[] messageArgs)
        {
            var location = symbol.Locations.FirstOrDefault();
            return new EquatableDiagnostic(
                descriptor,
                LocationInfo.From(location),
                messageArgs.ToEquatableArray());
        }

        public Diagnostic ToDiagnostic() =>
            Diagnostic.Create(
                _descriptor,
                _location?.ToLocation() ?? Location.None,
                _messageArgs.AsImmutableArray().Cast<object?>().ToArray());

        public bool Equals(EquatableDiagnostic? other)
        {
            if (other is null) return false;
            return _descriptor.Id == other._descriptor.Id
                && Equals(_location, other._location)
                && _messageArgs.Equals(other._messageArgs);
        }

        public override bool Equals(object? obj) => Equals(obj as EquatableDiagnostic);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _descriptor.Id.GetHashCode();
                hash = (hash * 397) ^ (_location?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ _messageArgs.GetHashCode();
                return hash;
            }
        }
    }

    internal sealed class LocationInfo : IEquatable<LocationInfo>
    {
        private readonly string _filePath;
        private readonly TextSpanInfo _span;
        private readonly LinePositionSpanInfo _lineSpan;

        private LocationInfo(string filePath, TextSpanInfo span, LinePositionSpanInfo lineSpan)
        {
            _filePath = filePath;
            _span = span;
            _lineSpan = lineSpan;
        }

        public static LocationInfo? From(Location? location)
        {
            if (location is null || location.SourceTree is null) return null;
            var span = location.SourceSpan;
            var lineSpan = location.GetLineSpan().Span;
            return new LocationInfo(
                location.SourceTree.FilePath,
                new TextSpanInfo(span.Start, span.Length),
                new LinePositionSpanInfo(
                    lineSpan.Start.Line, lineSpan.Start.Character,
                    lineSpan.End.Line, lineSpan.End.Character));
        }

        public Location ToLocation() =>
            Location.Create(
                _filePath,
                Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(_span.Start, _span.Start + _span.Length),
                new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                    new Microsoft.CodeAnalysis.Text.LinePosition(_lineSpan.StartLine, _lineSpan.StartChar),
                    new Microsoft.CodeAnalysis.Text.LinePosition(_lineSpan.EndLine, _lineSpan.EndChar)));

        public bool Equals(LocationInfo? other)
        {
            if (other is null) return false;
            return _filePath == other._filePath && _span.Equals(other._span) && _lineSpan.Equals(other._lineSpan);
        }

        public override bool Equals(object? obj) => Equals(obj as LocationInfo);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _filePath.GetHashCode();
                hash = (hash * 397) ^ _span.GetHashCode();
                hash = (hash * 397) ^ _lineSpan.GetHashCode();
                return hash;
            }
        }

        private readonly struct TextSpanInfo : IEquatable<TextSpanInfo>
        {
            public TextSpanInfo(int start, int length) { Start = start; Length = length; }
            public int Start { get; }
            public int Length { get; }
            public bool Equals(TextSpanInfo other) => Start == other.Start && Length == other.Length;
            public override bool Equals(object? obj) => obj is TextSpanInfo o && Equals(o);
            public override int GetHashCode() => unchecked((Start * 397) ^ Length);
        }

        private readonly struct LinePositionSpanInfo : IEquatable<LinePositionSpanInfo>
        {
            public LinePositionSpanInfo(int startLine, int startChar, int endLine, int endChar)
            {
                StartLine = startLine; StartChar = startChar; EndLine = endLine; EndChar = endChar;
            }
            public int StartLine { get; }
            public int StartChar { get; }
            public int EndLine { get; }
            public int EndChar { get; }
            public bool Equals(LinePositionSpanInfo other) =>
                StartLine == other.StartLine && StartChar == other.StartChar
                && EndLine == other.EndLine && EndChar == other.EndChar;
            public override bool Equals(object? obj) => obj is LinePositionSpanInfo o && Equals(o);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StartLine;
                    hash = (hash * 397) ^ StartChar;
                    hash = (hash * 397) ^ EndLine;
                    hash = (hash * 397) ^ EndChar;
                    return hash;
                }
            }
        }
    }

    /// <summary>
    /// Result of the transform: the (optional) class model plus diagnostics.
    /// </summary>
    internal sealed class TransformResult : IEquatable<TransformResult>
    {
        public TransformResult(MapperClassModel? model, EquatableArray<EquatableDiagnostic> diagnostics)
        {
            Model = model;
            Diagnostics = diagnostics;
        }

        public MapperClassModel? Model { get; }
        public EquatableArray<EquatableDiagnostic> Diagnostics { get; }

        public bool Equals(TransformResult? other)
        {
            if (other is null) return false;
            return Equals(Model, other.Model) && Diagnostics.Equals(other.Diagnostics);
        }

        public override bool Equals(object? obj) => Equals(obj as TransformResult);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Model?.GetHashCode() ?? 0) * 397) ^ Diagnostics.GetHashCode();
            }
        }
    }
}
