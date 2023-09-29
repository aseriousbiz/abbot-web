using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Serious.TestHelpers
{
    public class FakeUrlHelperFactory : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext context)
        {

            return new UrlHelper(context);
        }
    }
}
