using Microsoft.Extensions.Options;
using Npgsql;

namespace FlorenBooksWeb.Services;

public sealed class PostgresUserAuthenticationService : IUserAuthenticationService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly AuthDatabaseOptions _options;
    private readonly IPasswordService _passwordService;

    public PostgresUserAuthenticationService(
        NpgsqlDataSource dataSource,
        IOptions<AuthDatabaseOptions> options,
        IPasswordService passwordService)
    {
        _dataSource = dataSource;
        _options = options.Value;
        _passwordService = passwordService;
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(_options.LoginQuery);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("password", password);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var storedPassword = GetOptionalString(reader, "password_hash")
            ?? GetOptionalString(reader, "password");
        if (!string.IsNullOrWhiteSpace(storedPassword)
            && !_passwordService.VerifyPassword(password, storedPassword))
        {
            return null;
        }

        var id = GetRequiredString(reader, "id");
        var resolvedUsername = GetOptionalString(reader, "username")
            ?? GetRequiredString(reader, "email");
        var role = GetRequiredString(reader, "role");

        return new AuthenticatedUser(id, resolvedUsername, role);
    }

    private static string GetRequiredString(NpgsqlDataReader reader, string columnName)
    {
        var value = GetOptionalString(reader, columnName);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Login query must return a non-empty '{columnName}' column.");
        }

        return value;
    }

    private static string? GetOptionalString(NpgsqlDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal).ToString();
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

}
