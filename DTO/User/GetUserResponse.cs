using System;
using System.Text.Json.Serialization;

// ReSharper disable NotNullOrRequiredMemberIsNotInitialized
#nullable disable warnings

namespace LivestreamRecorderBackend.DTO.User;

internal class GetUserResponse
{
#pragma warning disable IDE1006 // 命名樣式
    [JsonPropertyName(nameof(id))]
    // ReSharper disable once InconsistentNaming
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式

    [JsonPropertyName(nameof(UserName))] public string UserName { get; set; }

    [JsonPropertyName(nameof(Email))] public string Email { get; set; }

    [JsonPropertyName(nameof(Picture))] public string? Picture { get; set; }

    [JsonPropertyName(nameof(RegistrationDate))]
    public DateTime RegistrationDate { get; set; }

    [JsonPropertyName(nameof(Note))] public string? Note { get; set; }

    [JsonPropertyName(nameof(IsAdmin))] public bool IsAdmin { get; set; }

    [JsonPropertyName(nameof(GoogleUID))]
    // ReSharper disable InconsistentNaming
    public string? GoogleUID { get; set; }

    [JsonPropertyName(nameof(GitHubUID))] public string? GitHubUID { get; set; }

    [JsonPropertyName(nameof(MicrosoftUID))]
    public string? MicrosoftUID { get; set; }
    // ReSharper restore InconsistentNaming
}
