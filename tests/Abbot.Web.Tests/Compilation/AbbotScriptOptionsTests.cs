using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Abbot.Scripting;
using Xunit;

public class AbbotScriptOptionsTests
{
    public class TheGetReferenceAssemblyNamesMethod
    {
        [Theory]
        [InlineData("mscorlib", false)]
        [InlineData("netstandard", false)]
        [InlineData("System", false)]
        [InlineData("System.Core", false)]
        [InlineData("System.Collections", true)]
        [InlineData("System.Collections.Concurrent", true)]
        [InlineData("System.Collections.Specialized", true)]
        [InlineData("System.Globalization", false)]
        [InlineData("System.Globalization.Calendars", false)]
        [InlineData("System.Globalization.Extensions", false)]
        [InlineData("System.Linq", true)]
        [InlineData("System.Linq.Expressions", true)]
        [InlineData("System.Linq.Parallel", true)]
        [InlineData("System.Net", false)]
        [InlineData("System.Net.Http", true)]
        [InlineData("System.Runtime", true)]
        [InlineData("System.Runtime.Extensions", false)]
        [InlineData("System.Text.Encoding", false)]
        [InlineData("System.Text.Encodings.Web", true)]
        [InlineData("System.Text.RegularExpressions", true)]
        [InlineData("System.Threading.Tasks.Parallel", true)]
        [InlineData("System.Web.HttpUtility", true)]
        public void IncludesTheSystemAssembliesWeNeedWhichExistAlongWithDocumentation(
            string assemblyName,
            bool expectedXmlExist)
        {
            var references = AbbotScriptOptions
                .ReferenceAssemblyNames
                .Select(a => a.Name)
                .ToHashSet(StringComparer.Ordinal);

            var refDllPath = Path.GetFullPath($"./refs/{assemblyName}.dll");
            var dllPath = Path.GetFullPath($"./{assemblyName}.dll");
            var xmlPath = Path.GetFullPath($"./refs/{assemblyName}.xml");

            Assert.Contains(assemblyName, references);
            Assert.True(File.Exists(refDllPath) || File.Exists(dllPath), $"{refDllPath} and {dllPath} do not exist.");
            Assert.True(!expectedXmlExist || File.Exists(xmlPath), $"{xmlPath} does not exist.");
        }
    }

    public class TheGetSkillEditorAssemblyNamesMethod
    {
        [Theory]
        [ClassData(typeof(AssemblyTestData))]
        public void ReturnsAssembliesThatAllExistInOutputDirectory(AssemblyName assemblyName)
        {
            var dllPath = Path.GetFullPath($"./{assemblyName.Name}.dll");
            var xmlPath = Path.GetFullPath($"./{assemblyName.Name}.xml");

            var ignoredDll = new HashSet<string>
            {
                "Microsoft.CSharp",
                "System.Collections.Immutable",
                "System.Threading.Tasks",
                "System.Diagnostics.Debug",
                "System.Runtime.Serialization.Primitives",
            };

            if (!ignoredDll.Contains(assemblyName.Name!))
            {
                Assert.True(File.Exists(dllPath), $"{dllPath} does not exist.");
            }

            var ignoredXml = new HashSet<string>
            {
                "Microsoft.Recognizers.Text",
                "Microsoft.Recognizers.Definitions",
                "Microsoft.Recognizers.Text.Choice",
                "Microsoft.Recognizers.Text.DataTypes.TimexExpression",
                "Microsoft.Recognizers.Text.DateTime",
                "Microsoft.Recognizers.Text.Number",
                "Microsoft.Recognizers.Text.NumberWithUnit",
                "Microsoft.Recognizers.Text.Sequence",
                "Serious.Slack.Messages" // TODO: Document everything in there and remove this.
            };

            if (!ignoredXml.Contains(assemblyName.Name!))
            {
                Assert.True(File.Exists(xmlPath), $"{xmlPath} does not exist.");
            }
        }

        class AssemblyTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                var referenceAssemblies = AbbotScriptOptions
                    .ReferenceAssemblyNames
                    .Select(asn => asn.Name)
                    .ToHashSet();

                return AbbotScriptOptions
                    .GetSkillEditorAssemblyNames()
                    .Where(asn => !referenceAssemblies.Contains(asn.Name))
                    .Where(a => a.Name is not "System.Memory")
                    .Select(assembly => new object[] { assembly })
                    .GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(AssemblyGroupedTestData))]
        public void ReturnsDistinctSetOfAssemblies(string assemblyName, int count)
        {
            Assert.True(count == 1, $"{assemblyName} was referenced {count} times.");
        }

        class AssemblyGroupedTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                return AbbotScriptOptions.GetSkillEditorAssemblyNames()
                    .GroupBy(a => a.Name)
                    .Select(group => new object[] { group.Key!, group.Count() })
                    .GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
