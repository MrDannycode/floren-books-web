using System.ComponentModel.DataAnnotations;
using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlorenBooksWeb.Pages.BorrowAdmin;

public sealed class BorrowedModel : PageModel
{
    private readonly ILibraryService _libraryService;

    public BorrowedModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    [BindProperty(SupportsGet = true)]
    public bool IncludeReturned { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public BorrowInput Input { get; set; } = new();

    public IReadOnlyList<BorrowedBook> Borrows { get; private set; } = [];
    public IReadOnlyList<UserAccount> Users { get; private set; } = [];
    public IReadOnlyList<Book> Books { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageDataAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostBorrowAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPageDataAsync(cancellationToken);
            return Page();
        }

        var borrowed = await _libraryService.BorrowBookAsync(Input.UserId, Input.BookId, cancellationToken);
        StatusMessage = borrowed
            ? "Imprumutul a fost inregistrat."
            : "Cartea este deja imprumutata sau datele selectate nu mai sunt valide.";

        return RedirectToPage(new { IncludeReturned, Search });
    }

    public async Task<IActionResult> OnPostReturnAsync(int id, CancellationToken cancellationToken)
    {
        await _libraryService.MarkReturnedAsync(id, cancellationToken);
        StatusMessage = "Imprumutul a fost marcat ca returnat.";

        return RedirectToPage(new { IncludeReturned, Search });
    }

    private async Task LoadPageDataAsync(CancellationToken cancellationToken)
    {
        Borrows = await _libraryService.GetBorrowedBooksAsync(IncludeReturned, Search, cancellationToken);
        Users = await _libraryService.GetUsersAsync(cancellationToken);
        Books = await _libraryService.GetBooksAsync(null, cancellationToken);
    }

    public sealed class BorrowInput
    {
        [Display(Name = "Utilizator")]
        [Range(1, int.MaxValue, ErrorMessage = "Alege utilizatorul.")]
        public int UserId { get; set; }

        [Display(Name = "Carte")]
        [Range(1, int.MaxValue, ErrorMessage = "Alege cartea.")]
        public int BookId { get; set; }
    }
}
