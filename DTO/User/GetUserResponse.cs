using System;
using System.Text.Json.Serialization;
#nullable disable warnings

namespace LivestreamRecorderBackend.DTO.User;

internal class GetUserResponse
{
#pragma warning disable IDE1006 // 命名樣式
    [JsonPropertyName(nameof(id))]
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式

    [JsonPropertyName(nameof(UserName))]
    public string UserName { get; set; }

    [JsonPropertyName(nameof(Email))]
    public string Email { get; set; }

    [JsonPropertyName(nameof(Picture))]
    public string? Picture { get; set; }

    [JsonPropertyName(nameof(RegistrationDate))]
    public DateTime RegistrationDate { get; set; }

    [JsonPropertyName(nameof(Note))]
    public string? Note { get; set; }

    [JsonPropertyName(nameof(GoogleUID))]
    public string? GoogleUID { get; set; }

    [JsonPropertyName(nameof(GithubUID))]
    public string? GithubUID { get; set; }

    [JsonPropertyName(nameof(MicrosoftUID))]
    public string? MicrosoftUID { get; set; }

    [JsonPropertyName(nameof(IsAdmin))]
    public bool IsAdmin { get; set; }
}
