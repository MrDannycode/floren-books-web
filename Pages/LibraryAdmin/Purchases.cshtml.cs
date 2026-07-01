using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlorenBooksWeb.Pages.LibraryAdmin;

public sealed class PurchasesModel : PageModel
{
    private readonly ILibraryService _libraryService;

    public PurchasesModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public IReadOnlyList<PurchasedBook> Purchases { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Purchases = await _libraryService.GetPurchasedBooksAsync(Search, cancellationToken);
    }
}
