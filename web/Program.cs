using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.Graph;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using web.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.HttpOverrides;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(new[] { 
        "User.Read",          // Base profile read permission
        "User.ReadWrite",     // Permission to update user profile
        "profile",            // OpenID Connect profile scope
        "email",              // OpenID Connect email scope
        builder.Configuration["BooksApi:Scopes"]?.Split(' ')[0] // Books API scope
    })
    .AddMicrosoftGraph(builder.Configuration.GetSection("GraphApi"))
    .AddInMemoryTokenCaches();

// Configure named HttpClient for BooksAPI with auth token
builder.Services.AddHttpClient("BooksApi", client => 
{
    client.BaseAddress = new Uri(builder.Configuration["BooksApi:BaseUrl"] ?? "http://localhost:8001");
});

// Register the BookService
builder.Services.AddScoped<web.Services.BookService>();

builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddHttpClient();

builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddMicrosoftIdentityConsentHandler();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler("/Error", createScopeForErrors: true);
// For compatibility with reverse proxies and load balancers like Azure Container Apps
app.UseForwardedHeaders();
app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();