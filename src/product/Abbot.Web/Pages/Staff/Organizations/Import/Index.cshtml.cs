using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Abbot.Telemetry;
using Serious.Slack;
using Serious.Tasks;

namespace Serious.Abbot.Pages.Staff.Organizations.Import;

public class IndexPageModel : OrganizationDetailPage
{
    readonly IImportService _importService;
    readonly IRoomRepository _roomRepository;
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly IConversationsApiClient _apiClient;
    readonly ISettingsManager _settingsManager;

    public DomId UploadedFileDomId => new("uploaded-file-contents");

    [Display(Name = "Upload Import File")]
    public IFormFile FileUpload { get; set; } = null!;

    [Required]
    [BindProperty]
    [MaxLength(1)]
    public string Delimiter { get; set; } = ",";

    [BindProperty]
    public IList<ColumnDataType> ImportColumns { get; set; } = new List<ColumnDataType>();

    [BindProperty]
    public bool ReplaceExisting { get; set; }

    public UploadedFile? UploadedFile { get; private set; }

    public LookupFile? LookupFile { get; private set; }

    public IndexPageModel(
        AbbotContext db,
        IImportService importService,
        IRoomRepository roomRepository,
        IUserRepository userRepository,
        IRoleManager roleManager,
        IConversationsApiClient apiClient,
        ISettingsManager settingsManager,
        IAuditLog auditLog) : base(db, auditLog)
    {
        _importService = importService;
        _roomRepository = roomRepository;
        _userRepository = userRepository;
        _roleManager = roleManager;
        _apiClient = apiClient;
        _settingsManager = settingsManager;
    }

    public async Task OnGet(string id)
    {
        await InitializeDataAsync(id);
    }

    public async Task<IActionResult> OnPostUploadAsync(string id)
    {
        await InitializeDataAsync(id);

        // We're just going to read the file at this point and display it to the user.
        UploadedFile = await UploadedFile.FromFileUploadAsync(FileUpload, Delimiter[0]);

        await SaveRecordAsync(UploadedFile);

        return TurboUpdate(UploadedFileDomId, Partial("_UploadedFile", this));
    }

    public async Task<IActionResult> OnPostClearAsync(string id)
    {
        await InitializeDataAsync(id);

        await SaveRecordAsync<UploadedFile>(null);
        await SaveRecordAsync<LookupFileIds>(null);

        UploadedFile = null;
        LookupFile = null;

        return TurboStream(
            TurboFlash(""),
            TurboUpdate(
                UploadedFileDomId,
                Partial("_UploadForm", this)));
    }

    public async Task<IActionResult> OnPostLookupAsync(string id, IReadOnlyList<int> importRows)
    {
        await InitializeDataAsync(id);
        var result = await UpdateUploadFileAsync(importRows);
        return result ?? await LookupImportDataAsync(UploadedFile!);
    }

    async Task<IActionResult?> UpdateUploadFileAsync(IReadOnlyList<int> importRows)
    {
        if (UploadedFile is null)
        {
            return TurboUpdate(UploadedFileDomId, Partial("_UploadForm", this));
        }

        UploadedFile = UploadedFile
            .WithRows(importRows)
            .WithColumns(ImportColumns);

        await SaveRecordAsync(UploadedFile);

        return null;
    }

    async Task<IActionResult> LookupImportDataAsync(UploadedFile uploadedFile)
    {
        var apiToken = Organization.ApiToken!.Reveal();

        var customerNameColumn = uploadedFile
            .Columns
            .SingleOrDefault(c => c.ColumnDataType is ColumnDataType.Customer);

        var channelToRoomDictionary = (await uploadedFile
            .GetValues(ColumnDataType.Channel)
            .SelectFunc(LookupChannelAsync)
            .WhenAllOneAtATimeNotNullAsync())
            .ToDictionary(room => room.Name);

        var roomMembershipLookup = await channelToRoomDictionary
            .Select(l => l.Value.Entity)
            .SelectFunc(room => CreateChannelMemberLookupTableAsync(apiToken, room))
            .WhenAllOneAtATimeAsync();

        var membersLookup = await uploadedFile
            .GetValues(ColumnDataType.FirstResponder)
            .SelectFunc(GetMemberByEmailAsync)
            .WhenAllOneAtATimeAsync();

        var memberLookupTable = new MemberLookupTable(membersLookup, roomMembershipLookup);

        var channelColumn = uploadedFile.Columns.FirstOrDefault(c => c.ColumnDataType is ColumnDataType.Channel);

        if (channelColumn is null)
        {
            return TurboFlash("Could not find a channel column", isError: true);
        }

        var firstResponderColumns = uploadedFile
            .Columns
            .Where(c => c.ColumnDataType is ColumnDataType.FirstResponder)
            .ToList();
        /* TODO:
        var escalationResponderColumns = uploadedFile
            .Columns
            .Where(c => c.ColumnActionType is ImportColumnActionType.EscalationResponder)
            .ToList(); */

        var rows = new List<ImportedRow>();

        foreach (var row in uploadedFile.Rows)
        {
            var customerName = customerNameColumn is not null
                ? row[customerNameColumn]?.Value
                : null;

            var channelName = row[channelColumn].Require();
            var channel = channelToRoomDictionary[channelName.Value];
            var room = channel.Entity;
            // Create a lookup for members of this room.


            var firstResponderCandidates = new List<LookupResult<Member>>();

            foreach (var cell in row[firstResponderColumns])
            {
                var firstResponder = memberLookupTable.LookupMemberForRoom(cell.Value, room);
                firstResponderCandidates.Add(firstResponder);
            }

            var candidatesToImport = firstResponderCandidates
                .Where(r => r.NeedsImport)
                .Select(r => r.Entity)
                .WhereNotNull()
                .ToList();

            rows.Add(new ImportedRow(
                customerName ?? string.Empty,
                channel,
                firstResponderCandidates,
                candidatesToImport));
        }

        LookupFile = new LookupFile(uploadedFile.Columns, rows);

        await SaveRecordAsync(LookupFile.ToIds());

        return TurboUpdate(UploadedFileDomId, Partial("_LookupFile", this));
    }

    async Task<ChannelMembershipLookupTable?> CreateChannelMemberLookupTableAsync(string apiToken, Room? room)
    {
        if (room is null)
        {
            return null;
        }

        var response = await _apiClient.GetAllConversationMembersAsync(
            apiToken,
            channel: room.PlatformRoomId);

        return response.Ok
            ? new ChannelMembershipLookupTable(room.Id, new HashSet<string>(response.Body))
            : null;
    }

    public async Task<IActionResult> OnPostImportAsync(string id)
    {
        await InitializeDataAsync(id);

        var lookupFileIds = await GetStoredRecordAsync<LookupFileIds>();

        if (lookupFileIds is null)
        {
            return TurboFlash("Could not deserialize data to import.", isError: true);
        }

        // Clear the stored data.
        await SaveRecordAsync<UploadedFile>(null);
        await SaveRecordAsync<LookupFileIds>(null);

        var abbot = await _userRepository.EnsureAbbotMemberAsync(Organization);

        foreach (var row in lookupFileIds.RowIds)
        {
            await _importService.ImportRespondersAsync(
                 row.RoomId,
                 row.FirstResponderIds,
                 roomRole: RoomRole.FirstResponder,
                 ReplaceExisting,
                 abbot);
        }

        LookupFile = null;
        UploadedFile = null;

        return TurboStream(
            TurboFlash("Import complete"),
            TurboUpdate(
                UploadedFileDomId,
                Partial("_UploadForm", this)));
    }

    async Task<LookupResult<Room>> LookupChannelAsync(string channelName)
    {
        var lookup = channelName.TrimStart('#');
        var room = await Db.Rooms
            .Include(r => r.Assignments)
            .Where(r => r.Name == lookup && r.OrganizationId == Organization.Id)
            .FirstOrDefaultAsync();

        return room is not null
            ? new LookupResult<Room>(channelName, room, room.BotIsMember is true ? null : "Abbot is not a member of this room")
            : new LookupResult<Room>(channelName, null, "Could not find a room with that name.");
    }

    async Task<Member?> GetMemberByEmailAsync(string email)
    {
        return await Db.Members
            .Include(m => m.User)
            .Include(m => m.MemberRoles)
            .ThenInclude(m => m.Role)
            .Include(m => m.RoomAssignments)
            .Where(m => m.User.Email == email)
            .Where(m => m.OrganizationId == Organization.Id)
            .SingleEntityOrDefaultAsync();
    }

    static string GetSettingsKey<T>() => $"BulkImport:{typeof(T)}";

    async Task<T?> GetStoredRecordAsync<T>() where T : class
    {
        var setting = await _settingsManager.GetAsync(
            SettingsScope.Member(Viewer),
            GetSettingsKey<T>());

        var json = setting?.Value;
        return json is not null
            ? JsonConvert.DeserializeObject<T>(json)
            : null;
    }

    async Task SaveRecordAsync<T>(T? file) where T : class
    {
        var key = GetSettingsKey<T>();
        if (file is null)
        {
            await _settingsManager.RemoveAsync(SettingsScope.Member(Viewer),
                key,
                Viewer.User);
        }
        else
        {
            await _settingsManager.SetAsync(
                SettingsScope.Member(Viewer),
                key,
                JsonConvert.SerializeObject(file),
                Viewer.User,
                ttl: TimeSpan.FromHours(24));
        }
    }

    protected override async Task InitializeDataAsync(Organization organization)
    {
        UploadedFile = await GetStoredRecordAsync<UploadedFile>();
    }
}
