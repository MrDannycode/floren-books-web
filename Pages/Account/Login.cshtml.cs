using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace FlorenBooksWeb.Pages.Account;

public sealed class LoginModel : PageModel
{
    private readonly IUserAuthenticationService _authenticationService;
    private readonly IRoleRedirectService _roleRedirectService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        IUserAuthenticationService authenticationService,
        IRoleRedirectService roleRedirectService,
        ILogger<LoginModel> logger)
    {
        _authenticationService = authenticationService;
        _roleRedirectService = roleRedirectService;
        _logger = logger;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(GetSafeRedirect(ReturnUrl, User.FindFirstValue(ClaimTypes.Role)));
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        AuthenticatedUser? user;

        try
        {
            user = await _authenticationService.AuthenticateAsync(
                Input.Username.Trim(),
                Input.Password,
                cancellationToken);
        }
        catch (NpgsqlException exception)
        {
            _logger.LogError(exception, "Database login failed.");
            ModelState.AddModelError(string.Empty, "Conexiunea la baza de date a esuat. Verifica setarile PostgreSQL.");
            return Page();
        }

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email sau parola incorecta.");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return LocalRedirect(GetSafeRedirect(ReturnUrl, user.Role));
    }

    private string GetSafeRedirect(string? returnUrl, string? role)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return _roleRedirectService.GetRedirectPath(role);
    }

    public sealed class LoginInput
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Introdu emailul.")]
        [EmailAddress(ErrorMessage = "Introdu un email valid.")]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Parola")]
        [Required(ErrorMessage = "Introdu parola.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
