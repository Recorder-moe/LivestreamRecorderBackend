using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorder.DB.Models;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

public class User : Entity
{
    [Required]
    [JsonProperty(nameof(UserName))]
    public string UserName { get; set; }

    [Required]
    [JsonProperty(nameof(Email))]
    public string Email { get; set; }

    [JsonProperty(nameof(Picture))]
    public string? Picture { get; set; }

    [JsonProperty(nameof(RegistrationDate))]
    public DateTime RegistrationDate { get; set; }

    [JsonProperty(nameof(Note))]
    public string? Note { get; set; }

    [JsonProperty(nameof(GoogleUID))]
    public string? GoogleUID { get; set; }

    [JsonProperty(nameof(GithubUID))]
    public string? GithubUID { get; set; }

    [JsonProperty(nameof(MicrosoftUID))]
    public string? MicrosoftUID { get; set; }

    [JsonProperty(nameof(DiscordUID))]
    public string? DiscordUID { get; set; }

    [JsonProperty(nameof(IsAdmin))]
    public bool IsAdmin { get; set; } = false;
}

