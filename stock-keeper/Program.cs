using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using CsvHelper;
using Azure.Identity;
using Azure.Core;
using StockKeeper;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

string apiBaseUrl = config["ApiBaseUrl"] ?? "http://localhost:8001";

// NOTE: You must set the following environment variables before running:
// AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, API_CLIENT_ID
string apiClientId = Environment.GetEnvironmentVariable("API_CLIENT_ID") ?? string.Empty;
string tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty;
string clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty;
string clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty;
if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(apiClientId))
{
    Console.WriteLine("ERROR: Missing required environment variables. Please set AZURE_TENANT_ID, AZURE_CLIENT_ID, and AZURE_CLIENT_SECRET.");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

// The .default scope is a special value that tells the token endpoint to issue a token containing all the app roles and delegated permissions that have been granted to the client application for the target API.
string scope = $"api://{apiClientId}/.default";

// Authentication using Azure Identity
async Task<string> GetTokenAsync()
{
    var options = new ClientSecretCredentialOptions
    {
        AuthorityHost = new Uri($"https://{tenantId}.ciamlogin.com")
    };
    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
    var tokenRequestContext = new TokenRequestContext(
        new[] { scope }
    );
    var token = await credential.GetTokenAsync(tokenRequestContext);
    return token.Token;
}

// Menu for managing stock
Console.WriteLine("Stock Keeper Console");
Console.WriteLine("---------------------");
Console.WriteLine("Enter the relative path to the CSV file with book titles to delete:");
Console.Write("CSV Path [Default: stock.csv]: ");
string? csvPath = Console.ReadLine();
if (string.IsNullOrWhiteSpace(csvPath))
    csvPath = "stock.csv";

if (!File.Exists(csvPath))
{
    Console.WriteLine($"File not found: {csvPath}");
    return;
}

List<BookToRemove> bookActions;
using (var reader = new StreamReader(csvPath))
using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
{
    bookActions = csv.GetRecords<BookToRemove>().ToList();
}

var token = await GetTokenAsync();
Console.WriteLine($"\nToken audience: {apiClientId}");
Console.WriteLine($"API Base URL: {apiBaseUrl}");
Console.WriteLine("\nTest with PowerShell:");
Console.WriteLine("------------------------");
Console.WriteLine($"$token = '{token}'");
Console.WriteLine($"Invoke-RestMethod -Uri '{apiBaseUrl}/books' -Headers @{{Authorization = \"Bearer $token\"}} -Method Get");
Console.WriteLine($"Invoke-RestMethod -Uri '{apiBaseUrl}/book/add' -Headers @{{Authorization = \"Bearer $token\"}} -Method Post -ContentType 'application/json' -Body '{{\"title\": \"Test Book\"}}'\n");

var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(apiBaseUrl);
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// Try the root endpoint first to verify basic connectivity
try {
    var testResp = await httpClient.GetAsync("/");
    Console.WriteLine($"\nAPI status: {testResp.StatusCode}");
    if (!testResp.IsSuccessStatusCode) {
        var errorContent = await testResp.Content.ReadAsStringAsync();
        Console.WriteLine($"Error content: {errorContent}");
    }
} catch (Exception ex) {
    Console.WriteLine($"\nError connecting to API: {ex.Message}");
}

// Get all books
var booksResp = await httpClient.GetAsync("/books");
booksResp.EnsureSuccessStatusCode();
var booksJson = await booksResp.Content.ReadAsStringAsync();
var books = JsonSerializer.Deserialize<List<Book>>(booksJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

// Process actions from CSV
foreach (var bookAction in bookActions)
{
    if (bookAction.Action.Equals("Add", StringComparison.OrdinalIgnoreCase))
    {
        var newBook = new Book { Title = bookAction.Title };
        var newBookJson = JsonSerializer.Serialize(newBook);
        var addContent = new StringContent(newBookJson, System.Text.Encoding.UTF8, "application/json");
        var addResp = await httpClient.PostAsync("/book/add", addContent);
        if (addResp.IsSuccessStatusCode)
            Console.WriteLine($"Added: {bookAction.Title}");
        else
        {
            var errorContent = await addResp.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to add: {bookAction.Title}. Status: {addResp.StatusCode}. Error: {errorContent}");
        }
    }
    else if (bookAction.Action.Equals("Remove", StringComparison.OrdinalIgnoreCase))
    {
        if (books != null)
        {
            var match = books.FirstOrDefault(b => b.Title.Equals(bookAction.Title, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                var delResp = await httpClient.DeleteAsync($"/books/delete/{match.Id}");
                if (delResp.IsSuccessStatusCode)
                    Console.WriteLine($"Deleted: {match.Title}");
                else
                {
                    var errorContent = await delResp.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to delete: {match.Title}. Status: {delResp.StatusCode}. Error: {errorContent}");
                }
            }
            else
            {
                Console.WriteLine($"Not found: {bookAction.Title}");
            }
        }
        else
        {
            Console.WriteLine($"Book list is null, cannot remove: {bookAction.Title}");
        }
    }
}

// --- Book model ---
public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
}
