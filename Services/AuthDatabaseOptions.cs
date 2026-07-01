namespace FlorenBooksWeb.Services;

public sealed class AuthDatabaseOptions
{
    public const string SectionName = "Authentication";

    public string LoginQuery { get; set; } = """
        SELECT id::text AS id,
               email AS username,
               role AS role,
               password AS password_hash
        FROM users
        WHERE email = @username
        LIMIT 1
        """;

    public Dictionary<string, string> RoleRedirects { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["superAdmin"] = "/SuperAdmin/Dashboard",
        ["libraryAdmin"] = "/LibraryAdmin/Dashboard",
        ["borrowAdmin"] = "/BorrowAdmin/Dashboard",
        ["user"] = "/User/Dashboard"
    };
}
