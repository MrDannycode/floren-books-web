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
    public int ActiveBorrows => Borrows.Count(borrow => borrow.ReturnDate is null);
    public int ReturnedBorrows => Borrows.Count(borrow => borrow.ReturnDate is not null);
    public decimal PurchasedTotal => Purchases.Sum(purchase => purchase.Pret ?? 0);

    [TempData]
    public string? StatusMessage { get; set; }

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

    public async Task<IActionResult> OnPostReturnAsync(int id, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        var returned = await _libraryService.MarkReturnedForUserAsync(id, userId, cancellationToken);
        StatusMessage = returned
            ? "Cartea a fost marcata ca returnata."
            : "Imprumutul nu a putut fi returnat.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        var borrows = await _libraryService.GetBorrowedBooksForUserAsync(userId, cancellationToken);
        var purchases = await _libraryService.GetPurchasedBooksForUserAsync(userId, cancellationToken);
        var rows = new List<string[]>
        {
            new[] { "Tip", "Carte", "Autor", "Data", "Status", "Pret" }
        };

        rows.AddRange(borrows.Select(static borrow => new[]
        {
            "Imprumut",
            borrow.Titlu,
            borrow.Autor,
            CsvExport.FormatDate(borrow.BorrowDate),
            borrow.ReturnDate is null ? "Activa" : "Returnata",
            string.Empty
        }));

        rows.AddRange(purchases.Select(static purchase => new[]
        {
            "Achizitie",
            purchase.Titlu,
            purchase.Autor,
            CsvExport.FormatDate(purchase.PurchaseDate),
            "Cumparata",
            CsvExport.FormatAmount(purchase.Pret)
        }));

        return File(CsvExport.Create(rows), CsvExport.ContentType, "cartile-mele.csv");
    }
}
