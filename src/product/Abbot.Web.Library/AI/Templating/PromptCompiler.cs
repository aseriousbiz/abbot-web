using System.Collections.Generic;
using System.IO;
using System.Text;
using HandlebarsDotNet;
using HandlebarsDotNet.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Abbot.AI.Commands;

namespace Serious.Abbot.AI.Templating;

public class PromptCompiler
{
    readonly IHandlebars _handlebars;

    public PromptCompiler()
    {
        var config = new HandlebarsConfiguration();
        config.TextEncoder = NullEncoder.Instance;
        config.FormatterProviders.Add(new CommandFormatterProvider());
        _handlebars = Handlebars.Create(config);
    }

    public Func<object, string> Compile(string template)
    {
        var compiled = _handlebars.Compile(template);

        // Compile returns a HandlebarsTemplate<object, object> which is a delegate that takes two object parameters
        // We want to return a Func<object, string> which is a delegate that takes one object parameter and returns a string
        return context => compiled(context);
    }
}

public class NullEncoder : ITextEncoder
{
    public static readonly ITextEncoder Instance = new NullEncoder();

    NullEncoder()
    {
    }

    public void Encode(StringBuilder text, TextWriter target)
    {
        target.Write(text.ToString());
    }

    public void Encode(string text, TextWriter target)
    {
        target.Write(text);
    }

    public void Encode<T>(T text, TextWriter target) where T : IEnumerator<char>
    {
        var sb = new StringBuilder();
        while (text.MoveNext())
        {
            sb.Append(text.Current);
        }
        Encode(sb, target);
    }
}

public class CommandFormatterProvider : IFormatterProvider
{
    public bool TryCreateFormatter(Type type, out IFormatter? formatter)
    {
        if (type == typeof(CommandList) || type == typeof(Command) || type == typeof(JObject))
        {
            formatter = new CommandFormatter();
            return true;
        }

        formatter = null;
        return false;
    }
}

public class CommandFormatter : IFormatter
{
    public void Format<T>(T value, in EncodedTextWriter writer)
    {
        if (value is JObject jobj)
        {
            FormatExemplar(jobj, writer);
        }
        else if (value is CommandList list)
        {
            FormatList(list, writer);
        }
        else if (value is Command cmd)
        {
            FormatCommand(cmd, writer);
        }
        else
        {
            throw new ArgumentException("Value must be a Command or CommandList.");
        }
    }

    static void FormatExemplar(JObject jobj, in EncodedTextWriter writer)
    {
        writer.Write(jobj.ToString(Formatting.None), encode: false);
    }

    static void FormatCommand(Command cmd, in EncodedTextWriter writer)
    {
        writer.Write(JsonConvert.SerializeObject(cmd, CommandParser.BaseSettings), encode: false);
    }

    static void FormatList(CommandList list, in EncodedTextWriter writer)
    {
        writer.Write(JsonConvert.SerializeObject(list, CommandParser.BaseSettings), encode: false);
    }
}
