using Microsoft.Extensions.Options;

namespace FlorenBooksWeb.Services;

public sealed class RoleRedirectService : IRoleRedirectService
{
    private readonly AuthDatabaseOptions _options;

    public RoleRedirectService(IOptions<AuthDatabaseOptions> options)
    {
        _options = options.Value;
    }

    public string GetRedirectPath(string? role)
    {
        if (!string.IsNullOrWhiteSpace(role)
            && _options.RoleRedirects.TryGetValue(role, out var redirectPath)
            && !string.IsNullOrWhiteSpace(redirectPath))
        {
            return redirectPath;
        }

        return "/";
    }
}
