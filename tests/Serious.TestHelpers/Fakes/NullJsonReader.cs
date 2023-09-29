using Newtonsoft.Json;

namespace Serious.TestHelpers;

public class NullJsonReader : JsonReader
{
    public NullJsonReader()
    {
        SetToken(JsonToken.Null);
    }

    public override bool Read()
    {
        return false;
    }
}
