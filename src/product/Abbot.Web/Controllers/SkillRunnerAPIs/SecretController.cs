using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Controllers;

public class SecretController : SkillRunnerApiControllerBase
{
    readonly ISkillSecretRepository _secretRepository;

    public SecretController(ISkillSecretRepository secretRepository)
    {
        _secretRepository = secretRepository;
    }

    [HttpGet("secret")]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string key)
    {
        var secret = await _secretRepository.GetSecretAsync(key, Skill, Member.User);
        if (secret is null)
        {
            return NotFound();
        }

        return new ObjectResult(new SkillSecretResponse
        {
            Secret = secret
        });
    }
}
