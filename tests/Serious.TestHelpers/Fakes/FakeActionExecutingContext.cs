using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Serious.TestHelpers
{
    public class FakeActionExecutingContext : ActionExecutingContext
    {
        public FakeActionExecutingContext(Controller controller)
            : this(new FakeActionContext(), new List<IFilterMetadata>(), new Dictionary<string, object>(), controller)
        {
        }

        public FakeActionExecutingContext(
            ActionContext actionContext,
            IList<IFilterMetadata> filters,
            IDictionary<string, object> actionArguments,
            object controller)
            : base(actionContext, filters, actionArguments, controller)
        {
        }
    }
}
