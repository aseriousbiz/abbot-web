using System;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Cryptography;

namespace Serious.Abbot.Pages.Staff.Stats;

public class ExportPageModel : StaffToolsPage
{
    readonly AbbotContext _db;

    public ExportPageModel(AbbotContext db)
    {
        _db = db;
    }

    public void OnGet()
    {
    }

    static CryptoRandom _random = new();

    public async Task<IActionResult> OnPostAsync()
    {
        var seed = $"394583487509184750192835701|{_random.Next()}";

        string Anonymize(int id) => $"{id}".ComputeHMACSHA256Hash(seed);

        var metrics = await _db.MetricObservations
            .Include(o => o.Conversation)
            .Include(o => o.Organization)
            .Where(o => o.Organization!.PlanType == PlanType.Business || (o.Organization.Trial != null && o.Organization.Trial.Expiry <= DateTime.UtcNow))
            .Select(o => new {
                o.Metric,
                o.OrganizationId,
                OrganizationCreated = o.Organization!.Created,
                OrganizationPlan = o.Organization!.PlanType,
                o.ConversationId,
                ConversationCreated = o.Conversation!.Created,
                o.Timestamp,
                o.RoomId,
                o.Value
            })
            .ToListAsync();

        var randomized = metrics
            .Select(m => $"{Anonymize(m.OrganizationId)},{Anonymize(m.RoomId)},{m.Metric},{m.Value},{m.Timestamp},{m.OrganizationCreated},{m.OrganizationPlan},{Anonymize(m.ConversationId)}, {m.ConversationCreated}");

        var csv = "Organization,Room,Metric,Value,Timestamp,OrganizationCreated,OrganizationPlan,ConversationId,ConversationCreated"
                  + Environment.NewLine
                  + string.Join(Environment.NewLine, randomized);
        // Write csv out to a file.
        ContentDisposition cd = new ContentDisposition
        {
            FileName = "Abbot-Metric-Export.csv",
            Inline = false  // false = prompt the user for downloading;  true = browser to try to show the file inline
        };
        Response.Headers.Add("Content-Disposition", cd.ToString());
        Response.Headers.Add("X-Content-Type-Options", "nosniff");
        return Content(csv, "text/csv");
    }
}
