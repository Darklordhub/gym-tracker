using System.Text.Json;
using System.Text.Json.Serialization;

namespace backend.Contracts;

public class WgerExerciseInfoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("translations")]
    public List<JsonElement> Translations { get; set; } = [];

    [JsonPropertyName("muscles")]
    public List<JsonElement> Muscles { get; set; } = [];

    [JsonPropertyName("muscles_secondary")]
    public List<JsonElement> MusclesSecondary { get; set; } = [];

    [JsonPropertyName("equipment")]
    public List<JsonElement> Equipment { get; set; } = [];

    [JsonPropertyName("images")]
    public List<JsonElement> Images { get; set; } = [];

    [JsonPropertyName("videos")]
    public List<JsonElement> Videos { get; set; } = [];
}
