using System.Collections;
using Serious.Abbot.Playbooks;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeTemplateEvaluator : ITemplateEvaluator, IEnumerable<KeyValuePair<string, string>>
{
    readonly Dictionary<string, string> _templates = new();

    public object Evaluate(string template)
    {
        return _templates.TryGetValue(template, out var result) ? result : template;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _templates.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(string template, string result)
    {
        _templates.Add(template, result);
    }

    public string this[string template]
    {
        get => _templates[template];
        set => _templates[template] = value;
    }
}
