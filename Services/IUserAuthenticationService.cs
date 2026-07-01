namespace FlorenBooksWeb.Services;

public interface IUserAuthenticationService
{
    Task<AuthenticatedUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken);
}
