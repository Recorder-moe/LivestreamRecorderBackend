using LivestreamRecorder.DB.Enums;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorder.DB.Models;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

public class Video : Entity
{
#if COUCHDB
    public override string Id
    {
        get => $"{ChannelId}:{id}";
        set
        {
            ChannelId = value?.Split(':').First() ?? "";
            id = value?.Split(':').Last() ?? "";
        }
    }
#endif

    [Required]
    [JsonProperty(nameof(ChannelId))]
    public string ChannelId { get; set; }

    [Required]
    [JsonProperty(nameof(Source))]
    public string Source { get; set; }

    [JsonProperty(nameof(Status))]
    public VideoStatus Status { get; set; }

    [JsonProperty(nameof(IsLiveStream))]
    public bool? IsLiveStream { get; set; }

    [JsonProperty(nameof(Title))]
    public string Title { get; set; }

    [JsonProperty(nameof(Description))]
    public string? Description { get; set; }

    [JsonProperty(nameof(Timestamps))]
    public Timestamps Timestamps { get; set; }

    // My system upload timestamp
    [JsonProperty(nameof(ArchivedTime))]
    public DateTime? ArchivedTime { get; set; }

    [JsonProperty(nameof(Thumbnail))]
    public string? Thumbnail { get; set; }

    [JsonProperty(nameof(Filename))]
    public string? Filename { get; set; }

    [JsonProperty(nameof(Size))]
    public long? Size { get; set; }

    [JsonProperty(nameof(SourceStatus))]
    public VideoStatus? SourceStatus { get; set; } = VideoStatus.Unknown;

    [JsonProperty(nameof(Note))]
    public string? Note { get; set; }

#if COSMOSDB
    [Obsolete("Relationship mapping is only supported in CosmosDB. Please avoid using it.")]
    public Channel? Channel { get; set; }
#endif
}

