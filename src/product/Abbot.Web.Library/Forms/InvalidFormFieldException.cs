using System;

namespace Serious.Abbot.Forms;

public class InvalidFormFieldException : Exception
{
    public string? FieldId { get; }

    public string? TemplateParameterName { get; }

    public InvalidFormFieldException(string fieldId, string templateParameterName, string message)
        : this(fieldId, templateParameterName, message, null)
    {
    }

    public InvalidFormFieldException(string fieldId, string templateParameterName, string message, Exception? innerException)
        : base(message, innerException)
    {
        FieldId = fieldId;
        TemplateParameterName = templateParameterName;
    }

    public InvalidFormFieldException()
    {
    }

    public InvalidFormFieldException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public InvalidFormFieldException(string message) : base(message)
    {
    }
}
