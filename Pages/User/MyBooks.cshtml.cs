using System.Security.Claims;
using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlorenBooksWeb.Pages.User;

public sealed class MyBooksModel : PageModel
{
    private readonly ILibraryService _libraryService;

    public MyBooksModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public IReadOnlyList<BorrowedBook> Borrows { get; private set; } = [];
    public IReadOnlyList<PurchasedBook> Purchases { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        Borrows = await _libraryService.GetBorrowedBooksForUserAsync(userId, cancellationToken);
        Purchases = await _libraryService.GetPurchasedBooksForUserAsync(userId, cancellationToken);

        return Page();
    }
}
