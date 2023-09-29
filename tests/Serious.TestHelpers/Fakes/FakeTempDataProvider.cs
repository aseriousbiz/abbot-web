using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Serious.TestHelpers;

public class FakeTempDataProvider : ITempDataProvider
{
    IDictionary<string, object> _data = new Dictionary<string, object>();

    public IDictionary<string, object> LoadTempData(HttpContext context)
    {
        return _data;
    }

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
        _data = values;
    }
}
