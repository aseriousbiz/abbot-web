using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Serious.Abbot.Serialization;

public class AbbotContractResolver : CamelCasePropertyNamesContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var prop = base.CreateProperty(member, memberSerialization);

        // If the member is marked with the C# required keyword, mark it as required in JSON.
        if (member.GetCustomAttribute<RequiredMemberAttribute>() is not null)
        {
            prop.Required = Required.Always;
        }

        return prop;
    }
}
