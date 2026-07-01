using Microsoft.AspNetCore.Mvc.RazorPages;
using FlorenBooksWeb.Services;

namespace FlorenBooksWeb.Pages.LibraryAdmin;

public sealed class DashboardModel : PageModel
{
    private readonly ILibraryService _libraryService;

    public DashboardModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public DashboardStats? Stats { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Stats = await _libraryService.GetDashboardStatsAsync(cancellationToken);
    }
}
