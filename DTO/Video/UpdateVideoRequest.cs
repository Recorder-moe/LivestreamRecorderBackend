using LivestreamRecorder.DB.Enums;

namespace LivestreamRecorderBackend.DTO.Video;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

internal class UpdateVideoRequest
{
#pragma warning disable IDE1006 // 命名樣式
    // ReSharper disable once InconsistentNaming
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式

    public string ChannelId { get; set; }

    public VideoStatus? Status { get; set; }

    public VideoStatus? SourceStatus { get; set; }

    public string? Note { get; set; }
}
