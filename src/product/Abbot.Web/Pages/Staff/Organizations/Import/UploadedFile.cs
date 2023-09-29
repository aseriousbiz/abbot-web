using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Serious.Abbot.Pages.Staff.Organizations.Import;

public record UploadedFile(IReadOnlyList<UploadRow> Rows, IReadOnlyList<UploadColumn> Columns)
{
    /// <summary>
    /// Returns distinct values of the specified data type. Use this when looking up data.
    /// </summary>
    /// <param name="columnDataType">The data type to look for.</param>
    public IEnumerable<string> GetValues(ColumnDataType columnDataType)
    {
        return Rows.SelectMany(r => r.GetValues(columnDataType)).Distinct();
    }

    public UploadedFile WithRows(IEnumerable<int> rowIndexes)
    {
        var newRows = Rows
            .Where(r => rowIndexes.Contains(r.Index))
            .ToList();
        return this with { Rows = newRows };
    }

    public UploadedFile WithColumns(IEnumerable<ColumnDataType> columnDataTypes)
    {
        var columnsToKeep = columnDataTypes
            .Select((type, i) => new { ColumnIndex = i, ColumnDataType = type })
            .Where(c => c.ColumnDataType is not ColumnDataType.Ignore)
            .ToDictionary(c => c.ColumnIndex, c => c.ColumnDataType);

        var newColumns = columnsToKeep
            .Select(kvp => new UploadColumn(kvp.Key, kvp.Value))
            .ToList();

        var newRows = Rows
            .Select(r => r.WithColumns(columnsToKeep))
            .ToList();

        return new UploadedFile(Columns: newColumns, Rows: newRows);
    }

    public static async Task<UploadedFile> FromFileUploadAsync(IFormFile formFile, char delimiter)
    {
        if (delimiter is '"' or '\'')
        {
            throw new ArgumentException("Delimiter cannot be a quote character", nameof(delimiter));
        }

        var stream = new MemoryStream();
        await formFile.CopyToAsync(stream);
        stream.Position = 0;
        return await FromStreamAsync(stream, delimiter);
    }

    public static async Task<UploadedFile> FromStreamAsync(Stream stream, char delimiter)
    {
        if (delimiter is '"' or '\'')
        {
            throw new ArgumentException("Delimiter cannot be a quote character", nameof(delimiter));
        }

        using var reader = new StreamReader(stream);
        return await FromStreamReaderAsync(reader, delimiter);
    }

    public static async Task<UploadedFile> FromStreamReaderAsync(StreamReader reader, char delimiter)
    {
        if (delimiter is '"' or '\'')
        {
            throw new ArgumentException("Delimiter cannot be a quote character", nameof(delimiter));
        }

        var rows = new List<UploadRow>();
        int columnCount = 0;

        // This regular expression splits string on the separator character NOT inside double quotes.
        // delimiter can be any character except double quotes - like comma or semicolon etc.
        // it also allows single quotes inside the string value: e.g. "Mike's Kitchen","Jane's Room"
        // Source: https://stackoverflow.com/a/48275050/598
        // Licences: https://creativecommons.org/licenses/by-sa/3.0/
        Regex regex = new Regex(delimiter + "(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))",
            RegexOptions.Compiled,
            matchTimeout: TimeSpan.FromMinutes(5));
        int rowIndex = 0;
        while (!reader.EndOfStream)
        {
            if (await reader.ReadLineAsync() is { Length: > 0 } line)
            {
                var result = regex.Split(line);
                columnCount = Math.Max(result.Length, columnCount);
                rows.Add(UploadRow.ReadFromSplitLine(result, rowIndex++));
            }
        }

        var columns = Enumerable
            .Range(0, columnCount)
            .Select(index => UploadColumn.FromIndex(index, rows))
            .ToList();
        return new UploadedFile(rows, columns);
    }
}

public record UploadRow(IReadOnlyList<UploadCell> Cells, int Index, bool HasPotentialData, bool HasPotentialChannel)
{
    public IEnumerable<string> GetValues(ColumnDataType columnDataType)
    {
        return Cells.Where(c => c.ColumnDataType == columnDataType)
            .Select(c => c.Value)
            .Distinct();
    }

    public UploadRow WithColumns(IDictionary<int, ColumnDataType> columnActions)
    {
        var cells = Cells
            .Where(c => columnActions.ContainsKey(c.ColumnIndex))
            .Select(c => c with
            {
                ColumnDataType = columnActions[c.ColumnIndex]
            })
            .ToList();

        return this with
        {
            Cells = cells
        };
    }

    public static UploadRow ReadFromSplitLine(IEnumerable<string> line, int index)
    {
        var cells = line.Select(UploadCell.FromValue).ToList();
        var hasPotentialData = cells.Any(cell => cell.ColumnDataType is not ColumnDataType.Ignore);
        // Since channel is required, and it's possible that the first row we find with a cell that has
        // potential data does not have something we recognize as a channel, we're going to separately
        // flag a row that clearly has a channel (aka a cell that starts with a #).
        // This is based on a customer's import where the first row with a channel omitted the #. :P
        var hasPotentialChannel = cells.Any(cell => cell.ColumnDataType is ColumnDataType.Channel);
        return new UploadRow(cells, index, hasPotentialData, hasPotentialChannel);
    }

#pragma warning disable CA1043
    public UploadCell? this[UploadColumn column] => Cells.FirstOrDefault(c => c.ColumnIndex == column.Index);

    public IEnumerable<UploadCell> this[IEnumerable<UploadColumn> columns] => columns
#pragma warning restore CA1043
        .Select(c => this[c])
        .WhereNotNull();
}

public enum ColumnDataType
{
    Ignore,
    Customer,
    FirstResponder,
    // TODO: EscalationResponder,
    Channel,
}

public record UploadCell(string Value, int ColumnIndex, ColumnDataType ColumnDataType)
{
    static readonly Regex EmailRegex = new("^[^@]+@[^@]+$");

    public static UploadCell FromValue(string value, int columnIndex)
    {
        ColumnDataType columnDataType = ColumnDataType.Ignore;
        if (value.StartsWith('#'))
        {
            columnDataType = ColumnDataType.Channel;
        }
        else if (EmailRegex.IsMatch(value))
        {
            columnDataType = ColumnDataType.FirstResponder;
        }

        return new UploadCell(value, columnIndex, columnDataType);
    }
}

public record UploadColumn(int Index, ColumnDataType ColumnDataType)
{
    public static UploadColumn FromIndex(int index, IReadOnlyList<UploadRow> rows)
    {
        ColumnDataType columnDataType = ColumnDataType.Ignore;
        var firstRowWithPotentialData = rows.FirstOrDefault(row => row.HasPotentialData);
        if (firstRowWithPotentialData?.Cells.Count > index)
        {
            var cell = firstRowWithPotentialData.Cells[index];
            columnDataType = cell.ColumnDataType;
        }

        if (columnDataType is ColumnDataType.Ignore)
        {
            var firstRowWithPotentialChannel = rows.FirstOrDefault(row => row.HasPotentialChannel);
            if (firstRowWithPotentialChannel?.Cells.Count > index)
            {
                var cell = firstRowWithPotentialChannel.Cells[index];
                columnDataType = cell.ColumnDataType;
            }
        }

        if (index is 0 && columnDataType is ColumnDataType.Ignore)
        {
            // Usually, the first column is the customer name. We'll suggest it. User can set it to "Ignore" if they want.
            columnDataType = ColumnDataType.Customer;
        }

        return new UploadColumn(index, columnDataType);
    }

    public IEnumerable<SelectListItem> GetSelectListItems()
    {
        return Enum.GetValues<ColumnDataType>()
            .Select(columnDataType => new SelectListItem(
                $"{columnDataType}",
                columnDataType.ToString(),
                columnDataType == ColumnDataType))
            .ToList();
    }
}
