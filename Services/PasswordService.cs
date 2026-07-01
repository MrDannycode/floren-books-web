using System.Security.Cryptography;
using System.Text;

namespace FlorenBooksWeb.Services;

public sealed class PasswordService : IPasswordService
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string storedPassword)
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
