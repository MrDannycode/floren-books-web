using Microsoft.AspNetCore.Mvc.RazorPages;
using FlorenBooksWeb.Services;

namespace FlorenBooksWeb.Pages.BorrowAdmin;

public sealed class DashboardModel : PageModel
{
    private readonly ILibraryService _libraryService;
    private const int RecentItemsCount = 5;

    public DashboardModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public DashboardStats? Stats { get; private set; }
    public IReadOnlyList<BorrowedBook> RecentBorrows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Stats = await _libraryService.GetDashboardStatsAsync(cancellationToken);
        RecentBorrows = await _libraryService.GetRecentBorrowedBooksAsync(RecentItemsCount, cancellationToken);
    }
}
