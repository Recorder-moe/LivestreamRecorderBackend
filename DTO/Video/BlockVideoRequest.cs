#nullable disable warnings

using LivestreamRecorder.DB.Enum;

namespace LivestreamRecorderBackend.DTO.Video;

public class BlockVideoRequest
{
#pragma warning disable IDE1006 // 命名樣式
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式
    public VideoStatus Status { get; set; }
    public string? Note { get; set; }
}