namespace FlorenBooksWeb.Services;

public interface ILibraryService
{
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Book>> GetBooksAsync(string? search, CancellationToken cancellationToken);
    Task<Book?> GetBookAsync(int id, CancellationToken cancellationToken);
    Task<int> CreateBookAsync(BookInput input, CancellationToken cancellationToken);
    Task UpdateBookAsync(int id, BookInput input, CancellationToken cancellationToken);
    Task DeleteBookAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken);
    Task<UserAccount?> GetUserAsync(int id, CancellationToken cancellationToken);
    Task<int> CreateUserAsync(UserInput input, CancellationToken cancellationToken);
    Task UpdateUserAsync(int id, UserInput input, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(int id, string currentPassword, string newPassword, CancellationToken cancellationToken);
    Task DeleteUserAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<BorrowedBook>> GetBorrowedBooksAsync(bool includeReturned, string? search, CancellationToken cancellationToken);
    Task<bool> BorrowBookAsync(int userId, int bookId, CancellationToken cancellationToken);
    Task<bool> BorrowBookForUserAsync(int userId, int bookId, CancellationToken cancellationToken);
    Task MarkReturnedAsync(int borrowId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BorrowedBook>> GetBorrowedBooksForUserAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PurchasedBook>> GetPurchasedBooksAsync(string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<PurchasedBook>> GetPurchasedBooksForUserAsync(int userId, CancellationToken cancellationToken);
    Task PurchaseBookAsync(int userId, int bookId, CancellationToken cancellationToken);
}
