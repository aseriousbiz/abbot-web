using System;
using System.Collections.Generic;
using Serious.Abbot.Storage;
using Serious.TestHelpers;
using Xunit;

public class BrainSerializationBinderTests
{
    [Theory]
    [InlineData("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "System.Collections.Generic.Dictionary`2[[System.String, mscorlib,Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]")]
    [InlineData("System.Private.CoreLib", "System.Collections.Generic.List`1[[Submission#0+DbState, ℛ*0ad31ee8-fec5-43bc-82d0-bc0a5b272917#21-0]]")]
    [InlineData("<>f__AnonymousType5", "Abbot.Functions.Tests")]
    public void SplitFullyQualifiedTypeNameTest(string assemblyName, string typeName)
    {
        (string?, string) typeAndAssemblyInfo = BrainSerializationBinder.SplitFullyQualifiedTypeName($"{typeName}, {assemblyName}".AsSpan());
        Assert.Equal(assemblyName, typeAndAssemblyInfo.Item1);
        Assert.Equal(typeName, typeAndAssemblyInfo.Item2);
    }

    [Theory]
    [InlineData("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        "System.Collections.Generic.Dictionary`2[[System.String, mscorlib,Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]",
        typeof(System.Collections.Generic.Dictionary<string, string>))]
    [InlineData("System.Private.CoreLib",
        "System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, System.Private.CoreLib]]",
        typeof(System.Collections.Generic.Dictionary<string, string>))]
    public void GetTypeByName(string assemblyName, string typeName, Type expectedType)
    {
        var binder = new BrainSerializationBinder("ℛ*0ad31ee8-fec5-43bc-82d0-bc0a5b272917#1-0");
        var type = binder.GetTypeByName(typeName, assemblyName);
        Assert.Equal(expectedType, type);
    }

    [Fact]
    public void GetTypeByNameWithUserTypes()
    {
        var code = "class DbState {}";
        var assembly = TestSkillCompiler.CompileCSharp(code);

        var userType = assembly.GetType("Submission#0+DbState")!;
        var expectedType = typeof(List<>).MakeGenericType(userType);

        var assemblyName = "System.Private.CoreLib";
        var typeName = "System.Collections.Generic.List`1[[Submission#0+DbState, ℛ*0ad31ee8-fec5-43bc-82d0-bc0a5b272917#1-0]]";
        var binder = new BrainSerializationBinder(assembly.GetName().Name!);
        var type = binder.GetTypeByName(typeName, assemblyName);
        Assert.Equal(expectedType, type);
    }

    [Fact]
    public void GetTypeByNameWithUserTypesInsideListList()
    {
        var code = "class DbState {}";
        var assembly = TestSkillCompiler.CompileCSharp(code);

        var userType = assembly.GetType("Submission#0+DbState")!;
        var expectedType = typeof(List<>).MakeGenericType(typeof(List<>).MakeGenericType(userType));

        var outerAssemblyName = "System.Private.CoreLib";
        var typeName = "System.Collections.Generic.List`1[[System.Collections.Generic.List`1[[Submission#0+DbState, ℛ*0ad31ee8-fec5-43bc-82d0-bc0a5b272917#1-0]], System.Private.CoreLib]], System.Private.CoreLib";
        var binder = new BrainSerializationBinder(assembly.GetName().Name!);
        var type = binder.GetTypeByName(typeName, outerAssemblyName);
        Assert.Equal(expectedType, type);
    }

    [Theory]
    [InlineData("System.Private.CoreLib", "System.Collections.Generic.List`1[[Submission#0+NotFound, ℛ*0ad31ee8-fec5-43bc-82d0-bc0a5b272917#21-0]]")]
    public void GetTypeByNameThrowsIfNotFound(string assemblyName, string typeName)
    {
        var binder = new BrainSerializationBinder("ℛ*0ad31ee8-fec5-43bc-82d0-bc0a5b272917#1-0");
        Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => binder.GetTypeByName(typeName, assemblyName));
    }
}
