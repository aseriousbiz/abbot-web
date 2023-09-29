using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

[assembly: CLSCompliant(false)]

namespace Serious.Abbot
{
    public static class ScriptAssemblies
    {
        static List<AssemblyName>? _assemblies;
        public static IEnumerable<AssemblyName> Assemblies
        {
            get {
                if (_assemblies is not null)
                    return _assemblies;

                _assemblies = new List<AssemblyName>();
                const string resourceName = "Serious.Abbot.references.txt";

                var stream = typeof(ScriptAssemblies).Assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    return _assemblies;
                }

                using var reader = new StreamReader(stream);
                while (!reader.EndOfStream)
                {

                    var line = reader.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;
                    _assemblies.Add(new AssemblyName(line));
                }
                return _assemblies;
            }
        }
    }
}
