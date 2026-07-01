namespace FlorenBooksWeb.Services;

public sealed record AuthenticatedUser(
    string Id,
    string Username,
    string Role);
