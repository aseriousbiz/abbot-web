using System;
using System.Threading.Tasks;

namespace Serious.Abbot.Clients;

/// <summary>
/// Retrieves a cached api token.
/// </summary>
public interface IApiTokenCache
{
    Task<string> GetAsync(ApiIdentifier apiIdentifier, TimeSpan maxAge);
}
