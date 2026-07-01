using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FlorenBooksWeb.Services;

public sealed class PostgresUserAuthenticationService : IUserAuthenticationService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly AuthDatabaseOptions _options;

    public PostgresUserAuthenticationService(
        NpgsqlDataSource dataSource,
        IOptions<AuthDatabaseOptions> options)
    {
        _dataSource = dataSource;
        _options = options.Value;
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
            && !PasswordMatches(password, storedPassword))
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

    private static bool PasswordMatches(string password, string storedPassword)
    {
        var normalizedStoredPassword = storedPassword.Trim();

        if (FixedTimeEquals(password, normalizedStoredPassword))
        {
            return true;
        }

        if (PasswordMatchesHash(password, normalizedStoredPassword, MD5.HashData)
            || PasswordMatchesHash(password, normalizedStoredPassword, SHA1.HashData)
            || PasswordMatchesHash(password, normalizedStoredPassword, SHA256.HashData)
            || PasswordMatchesHash(password, normalizedStoredPassword, SHA384.HashData)
            || PasswordMatchesHash(password, normalizedStoredPassword, SHA512.HashData))
        {
            return true;
        }

        if (VerifyBCryptPassword(password, normalizedStoredPassword))
        {
            return true;
        }

        return VerifyAspNetIdentityPassword(password, normalizedStoredPassword);
    }

    private static bool VerifyBCryptPassword(string password, string storedPassword)
    {
        if (!storedPassword.StartsWith("$2a$", StringComparison.Ordinal)
            && !storedPassword.StartsWith("$2b$", StringComparison.Ordinal)
            && !storedPassword.StartsWith("$2x$", StringComparison.Ordinal)
            && !storedPassword.StartsWith("$2y$", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedPassword);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    private static bool PasswordMatchesHash(
        string password,
        string storedPassword,
        Func<byte[], byte[]> hashAlgorithm)
    {
        var hashBytes = hashAlgorithm(Encoding.UTF8.GetBytes(password));
        var hexHash = Convert.ToHexString(hashBytes);
        var base64Hash = Convert.ToBase64String(hashBytes);

        return FixedTimeEquals(hexHash, storedPassword)
            || FixedTimeEquals(hexHash.ToLowerInvariant(), storedPassword)
            || FixedTimeEquals(base64Hash, storedPassword);
    }

    private static bool VerifyAspNetIdentityPassword(string password, string storedPassword)
    {
        byte[] decodedHash;

        try
        {
            decodedHash = Convert.FromBase64String(storedPassword);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decodedHash.Length < 13 || decodedHash[0] != 0x01)
        {
            return false;
        }

        var prf = ReadNetworkByteOrder(decodedHash, 1);
        var iterationCount = ReadNetworkByteOrder(decodedHash, 5);
        var saltLength = ReadNetworkByteOrder(decodedHash, 9);

        if (saltLength < 16 || decodedHash.Length < 13 + saltLength)
        {
            return false;
        }

        var salt = new byte[saltLength];
        Buffer.BlockCopy(decodedHash, 13, salt, 0, salt.Length);

        var storedSubkeyLength = decodedHash.Length - 13 - salt.Length;
        if (storedSubkeyLength < 16)
        {
            return false;
        }

        var storedSubkey = new byte[storedSubkeyLength];
        Buffer.BlockCopy(decodedHash, 13 + salt.Length, storedSubkey, 0, storedSubkey.Length);

        var hashAlgorithmName = prf switch
        {
            0 => HashAlgorithmName.SHA1,
            1 => HashAlgorithmName.SHA256,
            2 => HashAlgorithmName.SHA512,
            _ => default
        };

        if (hashAlgorithmName == default)
        {
            return false;
        }

        var generatedSubkey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterationCount,
            hashAlgorithmName,
            storedSubkeyLength);

        return CryptographicOperations.FixedTimeEquals(storedSubkey, generatedSubkey);
    }

    private static int ReadNetworkByteOrder(byte[] buffer, int offset)
    {
        return (buffer[offset] << 24)
            | (buffer[offset + 1] << 16)
            | (buffer[offset + 2] << 8)
            | buffer[offset + 3];
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
