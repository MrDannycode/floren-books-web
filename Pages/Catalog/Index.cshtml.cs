using System.Security.Claims;
using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlorenBooksWeb.Pages.Catalog;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly ILibraryService _libraryService;

    public IndexModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public IReadOnlyList<Book> Books { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Books = await _libraryService.GetBooksAsync(Search, cancellationToken);
    }

    public async Task<IActionResult> OnPostPurchaseAsync(int bookId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("user") || !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        await _libraryService.PurchaseBookAsync(userId, bookId, cancellationToken);
        StatusMessage = "Cartea a fost adaugata in achizitiile tale.";

        return RedirectToPage(new { Search });
    }

    public async Task<IActionResult> OnPostBorrowAsync(int bookId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("user") || !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        var borrowed = await _libraryService.BorrowBookForUserAsync(userId, bookId, cancellationToken);
        StatusMessage = borrowed
            ? "Cartea a fost adaugata in imprumuturile tale."
            : "Cartea este deja imprumutata.";

        return RedirectToPage(new { Search });
    }
}
