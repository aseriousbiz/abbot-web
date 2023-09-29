# Why is this here?

See https://github.com/aseriousbiz/abbot/pull/3548 for more context

When running ForbiddenAccessAnalyzerTests, memory usage for dotnet spikes HARD (upwards of 30GB on my machine). After some investigation I traced it to this issue https://github.com/dotnet/roslyn/issues/22219 .

The ScriptGlobals type we use in the test is in Abbot.Web.Library.Tests itself, which forces the script to reference that library and take a dependency on its entire closure of assembly references. This make big memory boom boom.