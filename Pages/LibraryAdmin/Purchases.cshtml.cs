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
    public decimal TotalAmount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Purchases = await _libraryService.GetPurchasedBooksAsync(Search, cancellationToken);
        TotalAmount = Purchases.Sum(static purchase => purchase.Pret ?? 0);
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        var purchases = await _libraryService.GetPurchasedBooksAsync(Search, cancellationToken);
        var rows = new List<string[]>
        {
            new[] { "Utilizator", "Carte", "Autor", "Pret", "Data achizitie" }
        };

        rows.AddRange(purchases.Select(static purchase => new[]
        {
            purchase.UserEmail,
            purchase.Titlu,
            purchase.Autor,
            CsvExport.FormatAmount(purchase.Pret),
            CsvExport.FormatDate(purchase.PurchaseDate)
        }));

        return File(CsvExport.Create(rows), CsvExport.ContentType, "achizitii.csv");
    }
}
