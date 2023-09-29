using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Infrastructure.AzurePostgres;

#pragma warning disable CA1707 // Naming Styles
#pragma warning disable IDE1006 // Naming Styles

[Table("qs_view", Schema = "query_store")]
public class QueryStats
{
    [Key]
    public long? runtime_stats_entry_id { get; set; }

    [Column(TypeName = "oid")]
    [ForeignKey(nameof(User))]
    public uint user_id { get; set; }

    [Column(TypeName = "oid")]
    [ForeignKey(nameof(Database))]
    public uint db_id { get; set; }

    public long? query_id { get; set; }
    public string? query_sql_text { get; set; }
    public long? plan_id { get; set; }
    public DateTimeOffset? start_time { get; set; }
    public DateTimeOffset? end_time { get; set; }
    public long? calls { get; set; }
    public double? total_time { get; set; }
    public double? min_time { get; set; }
    public double? max_time { get; set; }
    public double? mean_time { get; set; }
    public double? stddev_time { get; set; }
    public long? rows { get; set; }
    public long? shared_blks_hit { get; set; }
    public long? shared_blks_read { get; set; }
    public long? shared_blks_dirtied { get; set; }
    public long? shared_blks_written { get; set; }
    public long? local_blks_hit { get; set; }
    public long? local_blks_read { get; set; }
    public long? local_blks_dirtied { get; set; }
    public long? local_blks_written { get; set; }
    public long? temp_blks_read { get; set; }
    public long? temp_blks_written { get; set; }
    public double? blk_read_time { get; set; }
    public double? blk_write_time { get; set; }
    public bool? is_system_query { get; set; }
    public string? query_type { get; set; }

    public PgDatabase? Database { get; set; }
    public PgUser? User { get; set; }
}

[Table("pg_database", Schema = "pg_catalog")]
public class PgDatabase
{
    [Key]
    [Column(TypeName = "oid")]
    public uint oid { get; set; }

    public string datname { get; set; } = null!;
    public int encoding { get; set; }
    public string datcollate { get; set; } = null!;
    public string datctype { get; set; } = null!;
    public bool datallowconn { get; set; }
    public int datconnlimit { get; set; }
}

[Table("pg_user", Schema = "pg_catalog")]
public class PgUser
{
    [Key]
    [Column(TypeName = "oid")]
    public uint? usesysid { get; set; }

    public string? usename { get; set; }
    public bool? usecreatedb { get; set; }
    public bool? usesuper { get; set; }
}

#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CA1707 // Naming Styles
