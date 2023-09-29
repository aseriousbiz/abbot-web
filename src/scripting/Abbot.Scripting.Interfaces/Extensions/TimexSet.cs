using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Serious.Abbot.Scripting;

/// <inheritdoc />
#pragma warning disable CS3009 // Base type is not CLS-compliant
public class TimexSet : Microsoft.Recognizers.Text.DataTypes.TimexExpression.TimexSet
#pragma warning restore CS3009 // Base type is not CLS-compliant
{
    /// <inheritdoc />
    public TimexSet(string timex) : base(timex)
    { }

    /// <inheritdoc />
    public override string ToString() =>
        Timex.ToString();

    /// <summary>
    /// Returns a natural language representation of this time set.
    /// </summary>
    public string ToNaturalLanguage() =>
        TimexConvert.ConvertTimexSetToString(this);
}
