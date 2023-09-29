namespace Serious.Abbot.Scripting;

/// <summary>
/// Helper constants for Timex types.
/// The original definitions are in Microsoft.Recognizers.Text.DataTypes.TimexExpression.Constants.TimexTypes (https://github.com/microsoft/Recognizers-Text/blob/45897758e92d2bf2bf0fc398e8c6461a2f7d1d38/.NET/Microsoft.Recognizers.Text.DataTypes.TimexExpression/Constants.cs#L44)
/// But they are static strings, which cannot be used in switch statements. This is due to be fixed upstream but the PR hasn't merged yet
/// </summary>
public static class TimexConstants
{
    /// <summary />
    public const string Date = "date";
    /// <summary />
    public const string DateTime = "datetime";
    /// <summary />
    public const string Present = "present";
    /// <summary />
    public const string Definite = "definite";
    /// <summary />
    public const string DateRange = "daterange";
    /// <summary />
    public const string Duration = "duration";
    /// <summary />
    public const string Time = "time";
    /// <summary />
    public const string TimeRange = "timerange";
    /// <summary />
    public const string DateTimeRange = "datetimerange";
    /// <summary />
    public const string Set = Microsoft.Recognizers.Text.DateTime.Constants.SYS_DATETIME_SET;
}
