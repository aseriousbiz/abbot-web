using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Runtime;
using Serious.Abbot.Scripting;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Execution;

public class HttpTriggerResponse : IHttpTriggerResponse
{
    public static readonly IHttpTriggerResponse Invalid = new InvalidHttpTriggerResponse();

    /// <summary>
    /// The raw content to return as the body of the response. Setting this will set <see cref="Content"/> to
    /// null if it's been set;
    /// </summary>
    public string? RawContent
    {
        get => _rawContent;
        set {
            _rawContent = value;
            _content = null;
        }
    }

    string? _rawContent;

    /// <summary>
    /// The content to return as the body of the response. This will
    /// be serialized as JSON and will overwrite <see cref="RawContent"/>.
    /// </summary>
    public object? Content
    {
        get => _content;
        set {
            _rawContent = AbbotJsonFormat.Default.Serialize(value, false);
            _content = value;
        }
    }

    object? _content;

    /// <summary>
    /// The content type to use in the response. If null, Abbot will
    /// choose the best content type using content negotiation.
    /// </summary>
    public string? ContentType { get; set; }

    public IResponseHeaders Headers { get; } = new ResponseHeaders();

    class InvalidHttpTriggerResponse : IHttpTriggerResponse
    {
        const string Message =
            "Properties of the Response may only be set when the skill is invoked via an HTTP trigger";
        public string? RawContent
        {
            get => null;
            set => throw new InvalidOperationException(Message);
        }

        public object? Content
        {
            get => null;
            set => throw new InvalidOperationException(Message);
        }

        public string? ContentType
        {
            get => null;
            set => throw new InvalidOperationException(Message);
        }

        public IResponseHeaders Headers { get; } = new InvalidResponseHeaders();
    }

    class InvalidResponseHeaders : IResponseHeaders
    {
        const string Message =
            "Response.Headers may not be changed when the skill is invoked via an HTTP trigger";
        public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator()
        {
            return Enumerable.Empty<KeyValuePair<string, StringValues>>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Enumerable.Empty<KeyValuePair<string, StringValues>>().GetEnumerator();
        }

        public int Count => 0;
        public bool ContainsKey(string key)
        {
            return false;
        }

        public bool TryGetValue(string key, out StringValues value)
        {
            value = StringValues.Empty;
            return false;
        }

        public ICollection<string> Keys => Enumerable.Empty<string>().ToReadOnlyCollection();
        public ICollection<StringValues> Values => Enumerable.Empty<StringValues>().ToReadOnlyCollection();

        public string? WebHookAllowedOrigin
        {
            get => string.Empty;
            set => throw new InvalidOperationException(Message);
        }

        public int WebHookAllowedRate
        {
            get => 0;
            set => throw new InvalidOperationException(Message);
        }

        public StringValues this[string key]
        {
            get => StringValues.Empty;
            set => throw new InvalidOperationException(Message);
        }
    }
}
