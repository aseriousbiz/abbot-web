using System;
using System.Linq;
using DiffPlex.DiffBuilder.Model;

namespace Serious.Abbot.Extensions;

public static class DiffExtensions
{
    /// <summary>
    /// Takes the diff and returns a text representation of a diff suitable for CodeMirror.
    /// </summary>
    /// <param name="diff">The diff</param>
    public static string ToDiffText(this DiffPaneModel diff)
    {
        static string GetDiffPrefix(ChangeType changeType)
        {
            return changeType switch
            {
                ChangeType.Inserted => "+ ",
                ChangeType.Deleted => "- ",
                _ => "  "
            };
        }

        var lines = diff.Lines.Select(line => $"{GetDiffPrefix(line.Type)} {line.Text}");
        return string.Join('\n', lines);
    }

    public static string FormatLineForWeb(this DiffPiece diffPiece)
    {
        return diffPiece.Text.Replace(' ', '\u00B7')
            .Replace("\t", "  ", StringComparison.Ordinal);
    }

    public static string GetDiffChangeTypeCharacter(this ChangeType changeType)
    {
        return changeType switch
        {
            ChangeType.Inserted => "+",
            ChangeType.Deleted => "-",
            _ => " "
        };
    }
}
