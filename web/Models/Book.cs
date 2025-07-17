using System.Text.Json.Serialization;

namespace web.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    [JsonPropertyName("published")]
    public int Published { get; set; }
    public decimal Price { get; set; }
}
