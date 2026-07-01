using Microsoft.AspNetCore.Mvc.RazorPages;
using FlorenBooksWeb.Services;

namespace FlorenBooksWeb.Pages.LibraryAdmin;

public sealed class DashboardModel : PageModel
{
    private readonly ILibraryService _libraryService;
    private const int RecentItemsCount = 5;

    public DashboardModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public DashboardStats? Stats { get; private set; }
    public IReadOnlyList<PurchasedBook> RecentPurchases { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Stats = await _libraryService.GetDashboardStatsAsync(cancellationToken);
        RecentPurchases = await _libraryService.GetRecentPurchasedBooksAsync(RecentItemsCount, cancellationToken);
    }
}
