using System.Security.Claims;
using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlorenBooksWeb.Pages.User;

public sealed class DashboardModel : PageModel
{
    private readonly ILibraryService _libraryService;

    public DashboardModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public int ActiveBorrows { get; private set; }
    public int ReturnedBorrows { get; private set; }
    public int Purchases { get; private set; }
    public IReadOnlyList<BorrowedBook> RecentBorrows { get; private set; } = [];
    public IReadOnlyList<PurchasedBook> RecentPurchases { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        var borrows = await _libraryService.GetBorrowedBooksForUserAsync(userId, cancellationToken);
        var purchases = await _libraryService.GetPurchasedBooksForUserAsync(userId, cancellationToken);

        ActiveBorrows = borrows.Count(static borrow => borrow.ReturnDate is null);
        ReturnedBorrows = borrows.Count(static borrow => borrow.ReturnDate is not null);
        Purchases = purchases.Count;
        RecentBorrows = borrows.Take(5).ToList();
        RecentPurchases = purchases.Take(5).ToList();

        return Page();
    }
}
