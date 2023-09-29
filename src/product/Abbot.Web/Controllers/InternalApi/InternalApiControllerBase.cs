using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Serious.Abbot.Controllers.InternalApi;

[ApiController]
[Area(Area)]
[ApiExplorerSettings(GroupName = "internal")]
// non-GET APIs that use cookie auth _must_ use an Anti-Froggery token to prevent CSRF attacks
[AutoValidateAntiforgeryToken]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public abstract class InternalApiControllerBase : UserControllerBase
{
    public const string Area = "InternalApi";
}
