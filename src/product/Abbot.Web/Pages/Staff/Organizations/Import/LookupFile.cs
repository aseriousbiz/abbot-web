using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Staff.Organizations.Import;

public record LookupFile(IReadOnlyList<UploadColumn> Columns, IReadOnlyList<ImportedRow> Rows)
{
    public LookupFileIds ToIds()
    {
        var idRows = Rows
            .Select(r => r.ToImportRowIds())
            .WhereNotNull()
            .ToList();
        return new LookupFileIds(idRows);
    }
}

public record LookupFileIds(IReadOnlyList<ImportRowIds> RowIds);

// This is the imported row we use to display.
public record ImportedRow(
    string CustomerName,
    LookupResult<Room> RoomResult,
    IReadOnlyList<LookupResult<Member>> FirstResponders,
    IReadOnlyList<Member> FirstResponderCandidates)
{
    public ImportRowIds? ToImportRowIds()
    {
        if (RoomResult.Entity is not { } room)
        {
            return null;
        }

        var firstResponderIds = FirstResponderCandidates
            .Select(m => (Id<Member>)m)
            .ToList();

        return new ImportRowIds(room, firstResponderIds);
    }

    public bool HasErrors => RoomResult.Error is not null
        || FirstResponders.Any(r => r.Error is { Length: > 0 });

    public bool HasStatus => FirstResponders.Any(r => r.Status is { Length: > 0 });
}

// This is used for serializing to hidden inputs.
public record ImportRowIds(Id<Room> RoomId, IReadOnlyList<Id<Member>> FirstResponderIds);

public record LookupResult<T>(string Name, T? Entity, string? Error = null, string? Status = null)
{
    [JsonIgnore]
    public bool NeedsImport => Entity is not null && Error is not { Length: > 0 };
}
