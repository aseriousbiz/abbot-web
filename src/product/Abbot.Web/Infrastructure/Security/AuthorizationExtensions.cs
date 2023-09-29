using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Serious.Abbot.Infrastructure.Security;

public static class AuthorizationExtensions
{
    public static PageConventionCollection AuthorizeFolders(
        this PageConventionCollection conventions,
        IEnumerable<string> folderPaths, string policy)
    {
        foreach (var folder in folderPaths)
        {
            conventions.AuthorizeFolder(folder, policy);
        }

        return conventions;
    }
}
