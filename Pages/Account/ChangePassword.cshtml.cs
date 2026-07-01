using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlorenBooksWeb.Pages.Account;

[Authorize]
public sealed class ChangePasswordModel : PageModel
{
    private readonly ILibraryService _libraryService;

    public ChangePasswordModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    [BindProperty]
    public ChangePasswordInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var changed = await _libraryService.ChangePasswordAsync(
            userId,
            Input.CurrentPassword,
            Input.NewPassword,
            cancellationToken);

        if (!changed)
        {
            ModelState.AddModelError(string.Empty, "Parola curenta este incorecta.");
            return Page();
        }

        StatusMessage = "Parola a fost schimbata.";
        return RedirectToPage();
    }

    public sealed class ChangePasswordInput
    {
        [Display(Name = "Parola curenta")]
        [Required(ErrorMessage = "Introdu parola curenta.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Display(Name = "Parola noua")]
        [Required(ErrorMessage = "Introdu parola noua.")]
        [MinLength(6, ErrorMessage = "Parola trebuie sa aiba minim 6 caractere.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "Confirma parola")]
        [Required(ErrorMessage = "Confirma parola noua.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Parolele nu coincid.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
