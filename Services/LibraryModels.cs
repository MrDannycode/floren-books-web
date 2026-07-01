namespace FlorenBooksWeb.Services;

public sealed record Book(
    int Id,
    string Titlu,
    string Autor,
    string? Editura,
    int? Anul,
    decimal? Pret,
    DateTime? CreatedAt);

public sealed record BookInput(
    string Titlu,
    string Autor,
    string? Editura,
    int? Anul,
    decimal? Pret);

public sealed record BorrowedBook(
    int Id,
    int UserId,
    string UserEmail,
    int BookId,
    string Titlu,
    string Autor,
    DateTime? BorrowDate,
    DateTime? ReturnDate);

public sealed record UserAccount(
    int Id,
    string Email,
    string Role);

public sealed record PurchasedBook(
    int Id,
    int UserId,
    string UserEmail,
    int BookId,
    string Titlu,
    string Autor,
    decimal? Pret,
    DateTime? PurchaseDate);

public sealed record DashboardStats(
    int Books,
    int Users,
    int ActiveBorrows,
    int ReturnedBorrows,
    int Purchases);
