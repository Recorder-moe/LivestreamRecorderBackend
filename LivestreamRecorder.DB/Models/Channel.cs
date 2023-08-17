using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorder.DB.Models;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

public class Channel : Entity
{
#if COSMOSDB
    public Channel() : base()
    {
#pragma warning disable CS0618 // 類型或成員已經過時
        Videos = new HashSet<Video>();
#pragma warning restore CS0618 // 類型或成員已經過時
    }
#endif

#if COUCHDB
    public override string Id
    {
        get => $"{Source}:{id}";
        set
        {
            Source = value?.Split(':').First() ?? "";
            id = value?.Split(':').Last() ?? "";
        }
    }
#endif

    [JsonProperty(nameof(ChannelName))]
    public string ChannelName { get; set; }

    [Required]
    [JsonProperty(nameof(Source))]
    public string Source { get; set; }

    [JsonProperty(nameof(Monitoring))]
    public bool Monitoring { get; set; } = false;

    [JsonProperty(nameof(Avatar))]
    public string? Avatar { get; set; }

    [JsonProperty(nameof(Banner))]
    public string? Banner { get; set; }

    [JsonProperty(nameof(LatestVideoId))]
    public string? LatestVideoId { get; set; }

    [JsonProperty(nameof(Hide))]
    public bool? Hide { get; set; } = false;

    [JsonProperty(nameof(UseCookiesFile))]
    public bool? UseCookiesFile { get; set; } = false;

    [JsonProperty(nameof(SkipNotLiveStream))]
    public bool? SkipNotLiveStream { get; set; } = true;

    [JsonProperty(nameof(AutoUpdateInfo))]
    public bool? AutoUpdateInfo { get; set; } = true;

    [JsonProperty(nameof(Note))]
    public string? Note { get; set; }

#if COSMOSDB
    [Obsolete("Relationship mapping is only supported in CosmosDB. Please avoid using it.")]
    public ICollection<Video> Videos { get; set; }
#endif
}

