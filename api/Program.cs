using Microsoft.EntityFrameworkCore;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Resources;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

// Add services to the container
var builder = WebApplication.CreateBuilder(args);

// Configure authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Configure authorization policies
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    
    options.AddPolicy("BooksReadAccess", policy =>
        policy.RequireAssertion(context => 
            context.User.HasClaim("http://schemas.microsoft.com/identity/claims/scope", "Books.Read") ||
            context.User.IsInRole("Books.Write")));

    options.AddPolicy("BooksWriteAccess", policy =>
        policy.RequireRole("Books.Write"));
});

// Add CORS services
builder.Services.AddCors();

// Add OpenTelemetry and configure it to use Azure Monitor if APPLICATIONINSIGHTS_CONNECTION_STRING is not null or empty
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING"))) {
    builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource.AddService(
            serviceName: "Globalmantics Books API"
        );
    })
    .UseAzureMonitor();
}
//Set Environment Variable APPLICATIONINSIGHTS_CONNECTION_STRING

// Use SQL Server if the connection string is present, otherwise use in-memory database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("No connection string found, using in-memory database.");
    builder.Services.AddDbContext<BookDb>(opt => opt.UseInMemoryDatabase("BookList"));
}
else
{
    Console.WriteLine($"Using SQL Server with connection string: {connectionString}");
    builder.Services.AddDbContext<BookDb>(opt => opt.UseSqlServer(connectionString));
}

var app = builder.Build();

// Configure middleware
app.UseAuthentication();
app.UseAuthorization();

// Enable CORS
app.UseCors(policy =>
{
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader();
});

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<BookDb>();
    DbInitializer.Initialize(context);
}

// Define the routes, PREFIX_URL_PATH should start with a / if defined
var prefixUrlPath = Environment.GetEnvironmentVariable("PREFIX_URL_PATH") ?? string.Empty;

var pathRoute = app.MapGet($"{prefixUrlPath}/", () =>
{
    return Results.Ok("API is operational.");
});

app.MapGet($"{prefixUrlPath}/books", async (BookDb db) =>
    await db.Books.ToListAsync())
    .RequireAuthorization("BooksReadAccess");

app.MapGet($"{prefixUrlPath}/books/{{id}}", async (int id, BookDb db) =>
    await db.Books.FindAsync(id)
        is Book book
            ? Results.Ok(book)
            : Results.NotFound())
    .RequireAuthorization("BooksReadAccess");


app.MapPost($"{prefixUrlPath}/book/add", async (Book book, BookDb db) =>
{
    db.Books.Add(book);
    await db.SaveChangesAsync();

    return Results.Created($"/books/{book.Id}", book);
})
.RequireAuthorization("BooksWriteAccess");

app.MapPut($"{prefixUrlPath}/books/update/{{id}}", async (int id, Book inputBook, BookDb db) =>
{
    var book = await db.Books.FindAsync(id);

    if (book is null) return Results.NotFound();

    book.Title = inputBook.Title;
    book.Image = inputBook.Image;

    await db.SaveChangesAsync();

    return Results.NoContent();
})
.RequireAuthorization("BooksWriteAccess");

app.MapDelete($"{prefixUrlPath}/books/delete/{{id}}", async (int id, BookDb db) =>
{
    if (await db.Books.FindAsync(id) is Book book)
    {
        db.Books.Remove(book);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    return Results.NotFound();
})
.RequireAuthorization("BooksWriteAccess");

app.Run();
