using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using NodaTime;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Storage;
using Serious.TestHelpers;
using Xunit;

public class BrainSerializerTests
{
    [Fact]
    public async Task CanRoundTripInstantToString()
    {
        var time = NodaTime.Text.InstantPattern.General.Parse("1985-10-26T01:21:00Z").Value;

        const string codeToSerialize = @"
using System;
public class SomeType {
    public NodaTime.Instant SomeProperty { get; set; }
}
return new SomeType { SomeProperty = NodaTime.Text.InstantPattern.General.Parse(""1985-10-26T01:21:00Z"").Value };
";

        const string codeToReadSerializedData = @"
using System;
public class SomeType {
    public string SomeProperty { get; set; }
    public string MoreProperty { get; set; }
}
var deserialized = Brain.Deserialize<SomeType>(Serialized);
return deserialized.SomeProperty;
";


        var script = CSharpScript.Create<object>(codeToSerialize, ScriptOptions.Default.AddReferences(typeof(Instant).Assembly));

        var scriptState = await script.RunAsync();
        var instance = scriptState.ReturnValue;
        var serializer = new BrainSerializer(new FakeSkillContextAccessor());
        var serialized = serializer.SerializeObject(instance);


        var deserializeScript = CSharpScript.Create<string>(codeToReadSerializedData, globalsType: typeof(TestScriptGlobals));

        var assemblyName = deserializeScript.GetCompilation().AssemblyName;
        Assert.NotNull(assemblyName);
        var deserializer = new BrainSerializer(new FakeSkillContextAccessor(assemblyName: assemblyName));
        var globals = new TestScriptGlobals(deserializer, serialized);
        var result = await deserializeScript.RunAsync(globals);

        Assert.Equal(time.ToString(), result.ReturnValue);
    }

    [Fact]
    public async Task CanRoundTripStringToInstant()
    {
        var time = NodaTime.Text.InstantPattern.General.Parse("1985-10-26T01:21:00Z").Value;

        const string codeToSerialize = @"
using System;
public class SomeType {
    public string SomeProperty { get; set; }
}
return new SomeType { SomeProperty = ""1985-10-26T01:21:00Z"" };
";

        const string codeToReadSerializedData = @"
using System;
public class SomeType {
    public NodaTime.Instant SomeProperty { get; set; }
    public string MoreProperty { get; set; }
}
var deserialized = Brain.Deserialize<SomeType>(Serialized);
return deserialized.SomeProperty.ToString();
";


        var script = CSharpScript.Create<object>(codeToSerialize);

        var scriptState = await script.RunAsync();
        var instance = scriptState.ReturnValue;
        var serializer = new BrainSerializer(new FakeSkillContextAccessor());
        var serialized = serializer.SerializeObject(instance);


        var deserializeScript = CSharpScript.Create<string>(codeToReadSerializedData,
            options: ScriptOptions.Default.AddReferences(typeof(Instant).Assembly),
            globalsType: typeof(TestScriptGlobals));

        var assemblyName = deserializeScript.GetCompilation().AssemblyName;
        Assert.NotNull(assemblyName);
        var deserializer = new BrainSerializer(new FakeSkillContextAccessor(assemblyName: assemblyName));
        var globals = new TestScriptGlobals(deserializer, serialized);
        var result = await deserializeScript.RunAsync(globals);

        Assert.Equal(time.ToString(), result.ReturnValue);
    }

    [Fact]
    public async Task CanRoundTripNodaTime()
    {
        var time = NodaTime.Text.InstantPattern.General.Parse("1985-10-26T01:21:00Z").Value;

        const string codeToSerialize = @"
using System;
using NodaTime;
using NodaTime.Text;
class DbState
{
	public string Key { get; set; }
	public string CreatedBy { get; set; }
	public Instant Expires { get; set; }
	public string Name { get; set; }
}

var state = new DbState
{
	Key = ""the key"",
	CreatedBy = Bot.From.Name,
	Expires = InstantPattern.General.Parse(""1985-10-26T01:21:00Z"").Value,
	Name = ""the name"",
};
await Bot.Brain.WriteAsync( ""brainkey"", state );
";

        const string codeToReadSerializedData = @"
using System;
class DbState
{
	public string Key { get; set; }
	public string CreatedBy { get; set; }
	public Instant Expires { get; set; }
	public string Name { get; set; }
}
DbState state = await Bot.Brain.GetAsAsync<DbState>( ""brainKey"" );
await Bot.ReplyAsync(state.Expires.ToString());
";


        var compiledSkill = TestSkillCompiler.CompileAsync(codeToSerialize);
        var brain = new FakeBotBrain(compiledSkill.Name);
        var bot = new FakeBot(brain)
        {
            SkillName = nameof(CanRoundTripNodaTime),
            From = new FakeChatUser("1", "bot", "Bot")
        };

        var exception = await compiledSkill.RunAsync(bot);
        Assert.Null(exception);


        compiledSkill = TestSkillCompiler.CompileAsync(codeToReadSerializedData);
        bot = new FakeBot(brain)
        {
            SkillName = nameof(CanRoundTripNodaTime)
        };

        exception = await compiledSkill.RunAsync(bot);
        Assert.Null(exception);
        Assert.Collection(bot.Replies, r => {
            Assert.Equal(time.ToString(), r);
        });

    }

    [Fact]
    public async Task CanRoundTripListsOfTypesFromDifferentAssemblies()
    {
        var script = CSharpScript.Create<object>(@"
using System;
using System.Collections.Generic;

public class SomeType {
    public string SomeProperty {
        get;
        set;
    }
}
return new List<SomeType> { new SomeType { SomeProperty = ""Serialize This!"" } };
");
        var scriptState = await script.RunAsync();
        var instance = scriptState.ReturnValue;
        var serializer = new BrainSerializer(new FakeSkillContextAccessor());
        var serialized = serializer.SerializeObject(instance);

        var deserializeScript = CSharpScript.Create<string>(@"
using System;
using System.Collections.Generic;

public class SomeType {
    public string SomeProperty {
        get;
        set;
    }
}
var deserialized = Brain.Deserialize<List<SomeType>>(Serialized);
return deserialized[0].SomeProperty;
", globalsType: typeof(TestScriptGlobals));
        var assemblyName = deserializeScript.GetCompilation().AssemblyName;
        Assert.NotNull(assemblyName);
        var deserializer = new BrainSerializer(new FakeSkillContextAccessor(assemblyName: assemblyName));
        var globals = new TestScriptGlobals(deserializer, serialized);
        var result = await deserializeScript.RunAsync(globals);

        Assert.Equal("Serialize This!", result.ReturnValue);
    }

    [Fact]
    public async Task CanRoundTripCustomListsOfTypesFromDifferentAssemblies()
    {
        var script = CSharpScript.Create<object>(@"
using System;
using System.Collections.Generic;

public class SomeType {
    public string SomeProperty {
        get;
        set;
    }
}

public class MyList<T> : List<T> {}

return new MyList<SomeType> { new SomeType { SomeProperty = ""Serialize This!"" } };
");
        var scriptState = await script.RunAsync();
        var instance = scriptState.ReturnValue;
        var serializer = new BrainSerializer(new FakeSkillContextAccessor());
        var serialized = serializer.SerializeObject(instance);

        var deserializeScript = CSharpScript.Create<string>(@"
using System;
using System.Collections.Generic;

public class SomeType {
    public string SomeProperty {
        get;
        set;
    }
}

public class MyList<T> : List<T> {}

var deserialized = Brain.Deserialize<MyList<SomeType>>(Serialized);
return deserialized[0].SomeProperty;
", globalsType: typeof(TestScriptGlobals));
        var assemblyName = deserializeScript.GetCompilation().AssemblyName;
        Assert.NotNull(assemblyName);
        var deserializer = new BrainSerializer(new FakeSkillContextAccessor(assemblyName: assemblyName));
        var globals = new TestScriptGlobals(deserializer, serialized);
        var result = await deserializeScript.RunAsync(globals);

        Assert.Equal("Serialize This!", result.ReturnValue);
    }

    [Fact]
    public async Task CanRoundTripSystemTypes()
    {
        var script = CSharpScript.Create<List<string>>(@"
using System;
using System.Collections.Generic;
return new List<string> { ""Serialize This!"" };
");
        var scriptState = await script.RunAsync();
        var instance = scriptState.ReturnValue;
        var serializer = new BrainSerializer(new FakeSkillContextAccessor());
        var serialized = serializer.SerializeObject(instance);

        var deserializeScript = CSharpScript.Create<string>(@"
using System;
using System.Collections.Generic;
var deserialized = Brain.Deserialize<List<string>>(Serialized);
return deserialized[0];
", globalsType: typeof(TestScriptGlobals));
        var assemblyName = deserializeScript.GetCompilation().AssemblyName;
        Assert.NotNull(assemblyName);
        var deserializer = new BrainSerializer(new FakeSkillContextAccessor(assemblyName: assemblyName));
        var globals = new TestScriptGlobals(deserializer, serialized);
        var result = await deserializeScript.RunAsync(globals);

        Assert.Equal("Serialize This!", result.ReturnValue);
    }

    public class TestScriptGlobals
    {
        public TestScriptGlobals(BrainSerializer brainSerializer, string serializedValue)
        {
            Brain = brainSerializer;
            Serialized = serializedValue;
        }

        public BrainSerializer Brain { get; }
        public string Serialized { get; }
    }

    public class TheDeserializeMethod
    {
        [Fact]
        public void DeserializesLegacyTypedJsonArrayDynamically()
        {
            const string json = @"{""$type"":""System.String[], System.Private.CoreLib"",""$values"":[""eye"",""bee"",""see""]}";
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());

            dynamic? result = serializer.Deserialize(json);

            Assert.NotNull(result);
            string[] array = Assert.IsType<string[]>(result);
            Assert.Collection(array,
                a1 => Assert.Equal("eye", a1),
                a2 => Assert.Equal("bee", a2),
                a3 => Assert.Equal("see", a3));
        }

        [Fact]
        public void DeserializesLegacyTypedJsonArray()
        {
            const string json = @"{""$type"":""System.String[], System.Private.CoreLib"",""$values"":[""eye"",""bee"",""see""]}";
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());

            var result = serializer.Deserialize<string[]>(json);

            Assert.NotNull(result);
            Assert.Collection(result,
                a1 => Assert.Equal("eye", a1),
                a2 => Assert.Equal("bee", a2),
                a3 => Assert.Equal("see", a3));
        }

        [Fact]
        public void DeserializesTypedObjectToObject()
        {
            const string code = @"
using System;
public class SomeType {
    public string Name { get; set; }
    public List<string> Stuff { get; set; }
}  
";
            var assembly = TestSkillCompiler.CompileCSharp(code);
            var someType = assembly.GetType("Submission#0+SomeType")!;

            const string json = @"{""$type"":""Submission#0+SomeType, ℛ*5cef1285-be03-44ce-aca7-196ff54719f8#4-0"",""Name"":""some name"",""Stuff"":{""$type"":""System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib"",""$values"":[""one"",""two"",""three""]}}";
            var serializer = new BrainSerializer(new FakeSkillContextAccessor(assemblyName: assembly.GetName().Name));

            dynamic? result = serializer.Deserialize(json, someType);

            Assert.NotNull(result);
            Assert.Equal(someType, result!.GetType());
            Assert.Equal("some name", result.Name);
            List<string> array = Assert.IsType<List<string>>(result.Stuff);
            Assert.Collection(array,
                a1 => Assert.Equal("one", a1),
                a2 => Assert.Equal("two", a2),
                a3 => Assert.Equal("three", a3));
        }
    }

    [Fact]
    public void DeserializesTypedObjectWithFuzzyMatchingTypes()
    {
        const string code = @"
using System;
public class SomeType {
    public string Name { get; set; }
    public string[] Stuff { get; set; }
}  
";
        var assembly = TestSkillCompiler.CompileCSharp(code);
        var someType = assembly.GetType("Submission#0+SomeType")!;

        // the json was serialized with a List<string> Stuff field, but the skill code now has string[] Stuff field
        const string json = @"{""$type"":""Submission#0+SomeType, ℛ*5cef1285-be03-44ce-aca7-196ff54719f8#4-0"",""Name"":""some name"",""Stuff"":{""$type"":""System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib"",""$values"":[""one"",""two"",""three""]}}";
        var serializer = new BrainSerializer(new FakeSkillContextAccessor(assemblyName: assembly.GetName().Name));

        dynamic? result = serializer.Deserialize(json, someType);

        Assert.NotNull(result);
        Assert.Equal(someType, result!.GetType());
        Assert.Equal("some name", result.Name);
        string[] array = Assert.IsType<string[]>(result.Stuff);
        Assert.Collection(array,
            a1 => Assert.Equal("one", a1),
            a2 => Assert.Equal("two", a2),
            a3 => Assert.Equal("three", a3));
    }

    [Fact]
    public async Task CanRoundTripSkillTypesFromDifferentAssemblies2()
    {
        var serializer = new BrainSerializer(new FakeSkillContextAccessor());
        var apiClient = new FakeBrainApiClient();
        var brain = new BotBrain(apiClient, serializer);

        string codeWithData = @"
using System;
public class SomeType {
    public string SomeProperty {
        get;
        set;
    }
}
var instance = new SomeType { SomeProperty = ""Serialize This!"" };
await WriteAsync(""record"", instance);
";

        var script = CSharpScript.Create(codeWithData, globalsType: typeof(BotBrain));
        await script.RunAsync(brain);
        string codeThatReadsAsValue = @"
using System;
public class SomeType {
    public string SomeProperty { get; set; }
}
var deserialized = await GetAllAsync();
var instance = deserialized[0].GetValueAs<SomeType>();
return instance.SomeProperty;
";


        var deserializeScript = CSharpScript.Create<string>(codeThatReadsAsValue, globalsType: typeof(BotBrain));
        var assemblyName = deserializeScript.GetCompilation().AssemblyName;
        Assert.NotNull(assemblyName);
        var accessor = new FakeSkillContextAccessor(assemblyName: assemblyName);
        var deserializer = new BrainSerializer(accessor);
        var deserializedBrain = new BotBrain(apiClient, deserializer);
        var result = await deserializeScript.RunAsync(deserializedBrain);

        Assert.Equal("Serialize This!", result.ReturnValue);

        string codeThatReadsWithCast = @"
using System;
public class SomeType {
    public string SomeProperty { get; set; }
}
var deserialized = await GetAllAsync();
var instance = (SomeType)deserialized[0].Value;
return instance.SomeProperty;
";


        deserializeScript = CSharpScript.Create<string>(codeThatReadsWithCast,
            globalsType: typeof(BotBrain),
            options: ScriptOptions.Default.AddReferences("Microsoft.CSharp")); // this is required for dynamic support

        assemblyName = deserializeScript.GetCompilation().AssemblyName;
        Assert.NotNull(assemblyName);
        deserializer = new BrainSerializer(new FakeSkillContextAccessor(assemblyName: assemblyName));
        deserializedBrain = new BotBrain(apiClient, deserializer);
        result = await deserializeScript.RunAsync(deserializedBrain);

        Assert.Equal("Serialize This!", result.ReturnValue);
    }
}
