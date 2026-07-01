using System.Security.Claims;
using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlorenBooksWeb.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IRoleRedirectService _roleRedirectService;

    public IndexModel(
        ILogger<IndexModel> logger,
        IRoleRedirectService roleRedirectService)
    {
        _logger = logger;
        _roleRedirectService = roleRedirectService;
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(_roleRedirectService.GetRedirectPath(User.FindFirstValue(ClaimTypes.Role)));
        }

        return Page();
    }
}
