using System.Text.Json.Serialization;

namespace backend.Contracts;

public class WgerPagedResponse<T>
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    [JsonPropertyName("results")]
    public List<T> Results { get; set; } = [];
}
