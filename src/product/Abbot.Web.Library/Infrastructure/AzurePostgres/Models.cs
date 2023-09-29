using System.ComponentModel.DataAnnotations;

namespace Serious.Abbot.Infrastructure.AzurePostgres;

#pragma warning disable CA1707 // Naming Styles
#pragma warning disable IDE1006 // Naming Styles

public class DatabaseQuerySummary : QuerySummaryBase
{
    public string datname { get; set; } = null!;
}

public class QuerySummary : QuerySummaryBase
{
    public string? usename { get; set; }
    public long? query_id { get; set; }
    public string? query_sql_text { get; set; }
    public bool? is_system_query { get; set; }
    public string? query_type { get; set; }
}

public abstract class QuerySummaryBase
{
    [DisplayFormat(DataFormatString = "{0:n0}")]
    public long? calls { get; set; }

    [DisplayFormat(DataFormatString = "{0:n2}")]
    public double? total_time { get; set; }

    [DisplayFormat(DataFormatString = "{0:n4}")]
    public double? min_time { get; set; }

    [DisplayFormat(DataFormatString = "{0:n4}")]
    public double? max_time { get; set; }

    [DisplayFormat(DataFormatString = "{0:n4}")]
    public double? mean_time { get; set; }

    [DisplayFormat(DataFormatString = "{0:n0}")]
    public long? rows { get; set; }

    [DisplayFormat(DataFormatString = "{0:n0}")]
    public long? rows_per_call => rows / calls;
}

#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CA1707 // Naming Styles
