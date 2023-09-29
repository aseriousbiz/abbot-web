using System.Collections.Generic;
using Newtonsoft.Json;

namespace Serious.Abbot.AI.Commands;

public class CommandList : List<Command>
{
    public CommandList(IList<Command> commands)
        : base(commands)
    {
    }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}
