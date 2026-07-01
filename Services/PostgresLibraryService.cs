using Npgsql;

namespace FlorenBooksWeb.Services;

public sealed class PostgresLibraryService : ILibraryService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IPasswordService _passwordService;

    public PostgresLibraryService(
        NpgsqlDataSource dataSource,
        IPasswordService passwordService)
    {
        _dataSource = dataSource;
        _passwordService = passwordService;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (SELECT count(*) FROM books) AS books,
                (SELECT count(*) FROM users) AS users,
                (SELECT count(*) FROM borrowed_books WHERE return_date IS NULL) AS active_borrows,
                (SELECT count(*) FROM borrowed_books WHERE return_date IS NOT NULL) AS returned_borrows,
                (SELECT count(*) FROM purchased_books) AS purchases
            """;

        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new DashboardStats(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4));
    }

    public async Task<IReadOnlyList<Book>> GetBooksAsync(string? search, CancellationToken cancellationToken)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var sql = """
            SELECT b.id, b.titlu, b.autor, b.editura, b.anul, b.pret, b.created_at,
                   (
                       SELECT count(*)::int
                       FROM borrowed_books bb
                       WHERE bb.book_id = b.id
                         AND bb.return_date IS NULL
                   ) AS active_borrow_count
            FROM books b
            WHERE @has_search = FALSE
               OR b.titlu ILIKE @search
               OR b.autor ILIKE @search
               OR COALESCE(b.editura, '') ILIKE @search
            ORDER BY b.titlu, b.autor
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("has_search", hasSearch);
        command.Parameters.AddWithValue("search", $"%{search?.Trim()}%");

        return await ReadBooksAsync(command, cancellationToken);
    }

    public async Task<Book?> GetBookAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT b.id, b.titlu, b.autor, b.editura, b.anul, b.pret, b.created_at,
                   (
                       SELECT count(*)::int
                       FROM borrowed_books bb
                       WHERE bb.book_id = b.id
                         AND bb.return_date IS NULL
                   ) AS active_borrow_count
            FROM books b
            WHERE b.id = @id
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);

        var books = await ReadBooksAsync(command, cancellationToken);
        return books.FirstOrDefault();
    }

    public async Task<int> CreateBookAsync(BookInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO books (titlu, autor, editura, anul, pret)
            VALUES (@titlu, @autor, @editura, @anul, @pret)
            RETURNING id
            """;

        await using var command = _dataSource.CreateCommand(sql);
        AddBookParameters(command, input);

        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id);
    }

    public async Task UpdateBookAsync(int id, BookInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE books
            SET titlu = @titlu,
                autor = @autor,
                editura = @editura,
                anul = @anul,
                pret = @pret
            WHERE id = @id
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        AddBookParameters(command, input);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteBookAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM books WHERE id = @id";

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, email, role::text, created_at
            FROM users
            ORDER BY email
            """;

        await using var command = _dataSource.CreateCommand(sql);
        return await ReadUsersAsync(command, cancellationToken);
    }

    public async Task<UserAccount?> GetUserAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, email, role::text, created_at
            FROM users
            WHERE id = @id
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);

        var users = await ReadUsersAsync(command, cancellationToken);
        return users.FirstOrDefault();
    }

    public async Task<int> CreateUserAsync(UserInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO users (email, password, role)
            VALUES (@email, @password, @role::user_role)
            RETURNING id
            """;

        if (string.IsNullOrWhiteSpace(input.Password))
        {
            throw new InvalidOperationException("Password is required when creating a user.");
        }

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("email", input.Email.Trim());
        command.Parameters.AddWithValue("password", _passwordService.HashPassword(input.Password));
        command.Parameters.AddWithValue("role", input.Role);

        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id);
    }

    public async Task UpdateUserAsync(int id, UserInput input, CancellationToken cancellationToken)
    {
        var newPassword = input.Password;
        var hasPassword = !string.IsNullOrWhiteSpace(newPassword);
        var sql = """
            UPDATE users
            SET email = @email,
                role = @role::user_role,
                password = CASE WHEN @has_password THEN @password ELSE password END
            WHERE id = @id
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("email", input.Email.Trim());
        command.Parameters.AddWithValue("role", input.Role);
        command.Parameters.AddWithValue("has_password", hasPassword);
        command.Parameters.AddWithValue("password", hasPassword ? _passwordService.HashPassword(newPassword!) : DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ChangePasswordAsync(
        int id,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        const string selectSql = "SELECT password FROM users WHERE id = @id";

        await using var selectCommand = _dataSource.CreateCommand(selectSql);
        selectCommand.Parameters.AddWithValue("id", id);

        var storedPassword = await selectCommand.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(storedPassword)
            || !_passwordService.VerifyPassword(currentPassword, storedPassword))
        {
            return false;
        }

        const string updateSql = "UPDATE users SET password = @password WHERE id = @id";

        await using var updateCommand = _dataSource.CreateCommand(updateSql);
        updateCommand.Parameters.AddWithValue("id", id);
        updateCommand.Parameters.AddWithValue("password", _passwordService.HashPassword(newPassword));

        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async Task DeleteUserAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM users WHERE id = @id";

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BorrowedBook>> GetBorrowedBooksAsync(
        bool includeReturned,
        string? search,
        CancellationToken cancellationToken)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var sql = """
            SELECT bb.id, bb.user_id, u.email, bb.book_id, b.titlu, b.autor, bb.borrow_date, bb.return_date
            FROM borrowed_books bb
            JOIN users u ON u.id = bb.user_id
            JOIN books b ON b.id = bb.book_id
            WHERE (@include_returned = TRUE OR bb.return_date IS NULL)
              AND (
                  @has_search = FALSE
                  OR u.email ILIKE @search
                  OR b.titlu ILIKE @search
                  OR b.autor ILIKE @search
              )
            ORDER BY bb.borrow_date DESC NULLS LAST, bb.id DESC
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("include_returned", includeReturned);
        command.Parameters.AddWithValue("has_search", hasSearch);
        command.Parameters.AddWithValue("search", $"%{search?.Trim()}%");

        return await ReadBorrowedBooksAsync(command, cancellationToken);
    }

    public async Task<bool> BorrowBookAsync(int userId, int bookId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO borrowed_books (user_id, book_id)
            SELECT @user_id, @book_id
            WHERE EXISTS (SELECT 1 FROM users WHERE id = @user_id)
              AND EXISTS (SELECT 1 FROM books WHERE id = @book_id)
              AND NOT EXISTS (
                  SELECT 1
                  FROM borrowed_books
                  WHERE book_id = @book_id
                    AND return_date IS NULL
              )
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("book_id", bookId);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return affectedRows > 0;
    }

    public async Task<bool> BorrowBookForUserAsync(int userId, int bookId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO borrowed_books (user_id, book_id)
            SELECT @user_id, @book_id
            WHERE EXISTS (SELECT 1 FROM users WHERE id = @user_id AND role = 'user'::user_role)
              AND EXISTS (SELECT 1 FROM books WHERE id = @book_id)
              AND NOT EXISTS (
                  SELECT 1
                  FROM borrowed_books
                  WHERE book_id = @book_id
                    AND return_date IS NULL
              )
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("book_id", bookId);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return affectedRows > 0;
    }

    public async Task MarkReturnedAsync(int borrowId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE borrowed_books
            SET return_date = COALESCE(return_date, now())
            WHERE id = @id
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", borrowId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BorrowedBook>> GetBorrowedBooksForUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT bb.id, bb.user_id, u.email, bb.book_id, b.titlu, b.autor, bb.borrow_date, bb.return_date
            FROM borrowed_books bb
            JOIN users u ON u.id = bb.user_id
            JOIN books b ON b.id = bb.book_id
            WHERE bb.user_id = @user_id
            ORDER BY bb.borrow_date DESC NULLS LAST, bb.id DESC
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("user_id", userId);

        return await ReadBorrowedBooksAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchasedBook>> GetPurchasedBooksForUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT pb.id, pb.user_id, u.email, pb.book_id, b.titlu, b.autor, b.pret, pb.purchase_date
            FROM purchased_books pb
            JOIN users u ON u.id = pb.user_id
            JOIN books b ON b.id = pb.book_id
            WHERE pb.user_id = @user_id
            ORDER BY pb.purchase_date DESC NULLS LAST, pb.id DESC
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("user_id", userId);

        var purchases = new List<PurchasedBook>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            purchases.Add(new PurchasedBook(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                GetNullableDecimal(reader, 6),
                GetNullableDateTime(reader, 7)));
        }

        return purchases;
    }

    public async Task<IReadOnlyList<PurchasedBook>> GetPurchasedBooksAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        const string sql = """
            SELECT pb.id, pb.user_id, u.email, pb.book_id, b.titlu, b.autor, b.pret, pb.purchase_date
            FROM purchased_books pb
            JOIN users u ON u.id = pb.user_id
            JOIN books b ON b.id = pb.book_id
            WHERE @has_search = FALSE
               OR u.email ILIKE @search
               OR b.titlu ILIKE @search
               OR b.autor ILIKE @search
            ORDER BY pb.purchase_date DESC NULLS LAST, pb.id DESC
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("has_search", hasSearch);
        command.Parameters.AddWithValue("search", $"%{search?.Trim()}%");

        return await ReadPurchasedBooksAsync(command, cancellationToken);
    }

    public async Task PurchaseBookAsync(int userId, int bookId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO purchased_books (user_id, book_id)
            SELECT @user_id, @book_id
            WHERE EXISTS (SELECT 1 FROM books WHERE id = @book_id)
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("book_id", bookId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<PurchasedBook>> ReadPurchasedBooksAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var purchases = new List<PurchasedBook>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            purchases.Add(new PurchasedBook(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                GetNullableDecimal(reader, 6),
                GetNullableDateTime(reader, 7)));
        }

        return purchases;
    }

    private static async Task<IReadOnlyList<Book>> ReadBooksAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var books = new List<Book>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            books.Add(new Book(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                GetNullableString(reader, 3),
                GetNullableInt32(reader, 4),
                GetNullableDecimal(reader, 5),
                GetNullableDateTime(reader, 6),
                reader.GetInt32(7)));
        }

        return books;
    }

    private static async Task<IReadOnlyList<BorrowedBook>> ReadBorrowedBooksAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var borrows = new List<BorrowedBook>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            borrows.Add(new BorrowedBook(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                GetNullableDateTime(reader, 6),
                GetNullableDateTime(reader, 7)));
        }

        return borrows;
    }

    private static async Task<IReadOnlyList<UserAccount>> ReadUsersAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var users = new List<UserAccount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new UserAccount(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                GetNullableDateTime(reader, 3)));
        }

        return users;
    }

    private static void AddBookParameters(NpgsqlCommand command, BookInput input)
    {
        command.Parameters.AddWithValue("titlu", input.Titlu.Trim());
        command.Parameters.AddWithValue("autor", input.Autor.Trim());
        command.Parameters.AddWithValue("editura", NormalizeNullable(input.Editura));
        command.Parameters.AddWithValue("anul", input.Anul.HasValue ? input.Anul.Value : DBNull.Value);
        command.Parameters.AddWithValue("pret", input.Pret.HasValue ? input.Pret.Value : DBNull.Value);
    }

    private static object NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static string? GetNullableString(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? GetNullableInt32(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static decimal? GetNullableDecimal(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static DateTime? GetNullableDateTime(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
