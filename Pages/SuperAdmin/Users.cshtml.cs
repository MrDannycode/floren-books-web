using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace FlorenBooksWeb.Pages.SuperAdmin;

public sealed class UsersModel : PageModel
{
    private static readonly string[] AllowedRoles =
    [
        "superAdmin",
        "libraryAdmin",
        "borrowAdmin",
        "user"
    ];

    private readonly ILibraryService _libraryService;

    public UsersModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    [BindProperty]
    public UserForm Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IReadOnlyList<UserAccount> Users { get; private set; } = [];
    public IReadOnlyList<string> Roles => AllowedRoles;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadUsersAsync(cancellationToken);

        if (EditId.HasValue)
        {
            var user = await _libraryService.GetUserAsync(EditId.Value, cancellationToken);
            if (user is null)
            {
                return NotFound();
            }

            Input = new UserForm
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!AllowedRoles.Contains(Input.Role))
        {
            ModelState.AddModelError("Input.Role", "Rol invalid.");
        }

        if (!Input.Id.HasValue && string.IsNullOrWhiteSpace(Input.Password))
        {
            ModelState.AddModelError("Input.Password", "Introdu parola pentru utilizatorul nou.");
        }

        if (!ModelState.IsValid)
        {
            await LoadUsersAsync(cancellationToken);
            return Page();
        }

        var input = new UserInput(Input.Email, Input.Role, Input.Password);

        try
        {
            if (Input.Id.HasValue)
            {
                await _libraryService.UpdateUserAsync(Input.Id.Value, input, cancellationToken);
                StatusMessage = "Utilizatorul a fost actualizat.";
            }
            else
            {
                await _libraryService.CreateUserAsync(input, cancellationToken);
                StatusMessage = "Utilizatorul a fost creat.";
            }
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            ModelState.AddModelError("Input.Email", "Exista deja un utilizator cu acest email.");
            await LoadUsersAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)
            && currentUserId == id)
        {
            StatusMessage = "Nu poti sterge contul cu care esti autentificat.";
            return RedirectToPage();
        }

        try
        {
            await _libraryService.DeleteUserAsync(id, cancellationToken);
            StatusMessage = "Utilizatorul a fost sters.";
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            StatusMessage = "Utilizatorul nu poate fi sters deoarece are imprumuturi sau achizitii asociate.";
        }

        return RedirectToPage();
    }

    private async Task LoadUsersAsync(CancellationToken cancellationToken)
    {
        Users = await _libraryService.GetUsersAsync(cancellationToken);
    }

    public sealed class UserForm
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Introdu emailul.")]
        [EmailAddress(ErrorMessage = "Introdu un email valid.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Alege rolul.")]
        public string Role { get; set; } = "user";

        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Parola trebuie sa aiba minim 6 caractere.")]
        public string? Password { get; set; }
    }
}
