using System.ComponentModel.DataAnnotations;
using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace FlorenBooksWeb.Pages.LibraryAdmin;

public sealed class BooksModel : PageModel
{
    private readonly ILibraryService _libraryService;

    public BooksModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    [BindProperty]
    public BookForm Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IReadOnlyList<Book> Books { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Books = await _libraryService.GetBooksAsync(Search, cancellationToken);

        if (EditId.HasValue)
        {
            var book = await _libraryService.GetBookAsync(EditId.Value, cancellationToken);
            if (book is null)
            {
                return NotFound();
            }

            Input = new BookForm
            {
                Id = book.Id,
                Titlu = book.Titlu,
                Autor = book.Autor,
                Editura = book.Editura,
                Anul = book.Anul,
                Pret = book.Pret
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Books = await _libraryService.GetBooksAsync(Search, cancellationToken);
            return Page();
        }

        var input = new BookInput(Input.Titlu, Input.Autor, Input.Editura, Input.Anul, Input.Pret);

        if (Input.Id.HasValue)
        {
            await _libraryService.UpdateBookAsync(Input.Id.Value, input, cancellationToken);
            StatusMessage = "Cartea a fost actualizata.";
        }
        else
        {
            await _libraryService.CreateBookAsync(input, cancellationToken);
            StatusMessage = "Cartea a fost adaugata.";
        }

        return RedirectToPage(new { Search });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _libraryService.DeleteBookAsync(id, cancellationToken);
            StatusMessage = "Cartea a fost stearsa.";
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            StatusMessage = "Cartea nu poate fi stearsa deoarece exista imprumuturi sau achizitii asociate.";
        }

        return RedirectToPage(new { Search });
    }

    public sealed class BookForm
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Introdu titlul.")]
        [Display(Name = "Titlu")]
        public string Titlu { get; set; } = string.Empty;

        [Required(ErrorMessage = "Introdu autorul.")]
        public string Autor { get; set; } = string.Empty;

        public string? Editura { get; set; }

        [Display(Name = "An")]
        public int? Anul { get; set; }

        [Display(Name = "Pret")]
        public decimal? Pret { get; set; }
    }
}
