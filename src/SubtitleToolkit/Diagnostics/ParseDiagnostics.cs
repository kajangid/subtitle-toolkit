using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SubtitleToolkit.Model;

namespace SubtitleToolkit.Diagnostics;

/// <summary>
/// Severity level of a parsing diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Represents a message or warning emitted during subtitle parsing.
/// </summary>
public sealed class ParseDiagnostic : IEquatable<ParseDiagnostic>
{
    public DiagnosticSeverity Severity { get; }
    public int LineNumber { get; }
    public string Code { get; }
    public string Message { get; }

    public ParseDiagnostic(DiagnosticSeverity severity, int lineNumber, string code, string message)
    {
        Severity = severity;
        LineNumber = lineNumber;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public bool Equals(ParseDiagnostic? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Severity == other.Severity
            && LineNumber == other.LineNumber
            && string.Equals(Code, other.Code, StringComparison.Ordinal)
            && string.Equals(Message, other.Message, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as ParseDiagnostic);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Severity.GetHashCode();
            hash = (hash * 31) + LineNumber.GetHashCode();
            hash = (hash * 31) + Code.GetHashCode();
            return hash;
        }
    }

    public override string ToString() => $"[Line {LineNumber}] {Severity} ({Code}): {Message}";
}

/// <summary>
/// Result of a subtitle parsing operation, containing the document and any diagnostics.
/// </summary>
public sealed class ParseResult
{
    /// <summary>The parsed subtitle document.</summary>
    public SubtitleDocument Document { get; }

    /// <summary>List of diagnostics (warnings or errors) collected during parsing.</summary>
    public IReadOnlyList<ParseDiagnostic> Diagnostics { get; }

    /// <summary>Whether any warnings were recorded.</summary>
    public bool HasWarnings => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

    /// <summary>Whether any errors were recorded.</summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public ParseResult(SubtitleDocument document, IEnumerable<ParseDiagnostic>? diagnostics = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        var diagList = diagnostics != null ? diagnostics.ToList() : new List<ParseDiagnostic>();
        Diagnostics = new ReadOnlyCollection<ParseDiagnostic>(diagList);
    }

    /// <summary>
    /// Implicitly converts a ParseResult to its underlying SubtitleDocument for ergonomic usage.
    /// </summary>
    public static implicit operator SubtitleDocument(ParseResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        return result.Document;
    }
}

/// <summary>
/// Exception thrown when subtitle parsing fails under Strict parsing mode.
/// </summary>
public class SubtitleParseException : Exception
{
    public int LineNumber { get; }

    public SubtitleParseException(string message, int lineNumber = 0)
        : base(lineNumber > 0 ? $"[Line {lineNumber}] {message}" : message)
    {
        LineNumber = lineNumber;
    }

    public SubtitleParseException(string message, int lineNumber, Exception innerException)
        : base(lineNumber > 0 ? $"[Line {lineNumber}] {message}" : message, innerException)
    {
        LineNumber = lineNumber;
    }
}
