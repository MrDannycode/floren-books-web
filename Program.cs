using FlorenBooksWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.Configure<AuthDatabaseOptions>(
    builder.Configuration.GetSection(AuthDatabaseOptions.SectionName));

builder.Services.AddSingleton(static serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("FlorenBooks");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Connection string 'FlorenBooks' is missing. Configure it in appsettings.json or user secrets.");
    }

    return NpgsqlDataSource.Create(connectionString);
});

builder.Services.AddScoped<IUserAuthenticationService, PostgresUserAuthenticationService>();
builder.Services.AddScoped<ILibraryService, PostgresLibraryService>();
builder.Services.AddSingleton<IRoleRedirectService, RoleRedirectService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "FlorenBooks.Auth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("superAdmin"));
    options.AddPolicy("LibraryAdminOnly", policy => policy.RequireRole("libraryAdmin"));
    options.AddPolicy("BorrowAdminOnly", policy => policy.RequireRole("borrowAdmin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("user"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/SuperAdmin", "SuperAdminOnly");
    options.Conventions.AuthorizeFolder("/LibraryAdmin", "LibraryAdminOnly");
    options.Conventions.AuthorizeFolder("/BorrowAdmin", "BorrowAdminOnly");
    options.Conventions.AuthorizeFolder("/User", "UserOnly");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
