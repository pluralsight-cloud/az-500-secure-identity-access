using Microsoft.Identity.Web;
using System.Net.Http.Headers;
using System.Text.Json;

namespace web.Services;

public class Book
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Image { get; set; }
}

public class BookService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BookService> _logger;

    public BookService(
        IHttpClientFactory httpClientFactory,
        ITokenAcquisition tokenAcquisition,
        IConfiguration configuration,
        ILogger<BookService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenAcquisition = tokenAcquisition;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<Book>> GetBooksAsync()
    {
        try
        {
            // Get the token for the Books API
            string[] scopes = { _configuration["BooksApi:Scopes"]?.Split(' ')[0] };
            string accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            // Create the HTTP client
            var client = _httpClientFactory.CreateClient("BooksApi");
            
            // Add the token to the request
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            // Make the request
            var response = await client.GetAsync("/books");
            response.EnsureSuccessStatusCode();
            
            // Read the response
            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var books = JsonSerializer.Deserialize<List<Book>>(content, options);
            
            return books ?? new List<Book>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting books from API");
            return new List<Book>();
        }
    }

    public async Task<Book?> GetBookAsync(int id)
    {
        try
        {
            // Get the token for the Books API
            string[] scopes = { _configuration["BooksApi:Scopes"]?.Split(' ')[0] };
            string accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            // Create the HTTP client
            var client = _httpClientFactory.CreateClient("BooksApi");
            
            // Add the token to the request
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            // Make the request
            var response = await client.GetAsync($"/books/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            
            response.EnsureSuccessStatusCode();
            
            // Read the response
            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Book>(content, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting book {id} from API");
            return null;
        }
    }
}
