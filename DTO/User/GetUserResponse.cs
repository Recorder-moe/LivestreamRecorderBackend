using Newtonsoft.Json;
using System;
#nullable disable warnings

namespace LivestreamRecorderBackend.DTO.User;

internal class GetUserResponse
{
#pragma warning disable IDE1006 // 命名樣式
    [JsonProperty(nameof(id))]
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式

    [JsonProperty(nameof(UserName))]
    public string UserName { get; set; }

    [JsonProperty(nameof(Email))]
    public string Email { get; set; }

    [JsonProperty(nameof(Picture))]
    public string? Picture { get; set; }

    [JsonProperty(nameof(RegistrationDate))]
    public DateTime RegistrationDate { get; set; }

    [JsonProperty(nameof(Note))]
    public string? Note { get; set; }

    [JsonProperty(nameof(GoogleUID), NullValueHandling = NullValueHandling.Ignore)]
    public string? GoogleUID { get; set; }

    [JsonProperty(nameof(GithubUID), NullValueHandling = NullValueHandling.Ignore)]
    public string? GithubUID { get; set; }

    [JsonProperty(nameof(MicrosoftUID), NullValueHandling = NullValueHandling.Ignore)]
    public string? MicrosoftUID { get; set; }

    [JsonProperty(nameof(IsAdmin))]
    public bool IsAdmin { get; set; }
}
