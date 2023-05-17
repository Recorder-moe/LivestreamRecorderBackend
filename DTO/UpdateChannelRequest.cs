namespace LivestreamRecorderBackend.DTO;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

public class UpdateChannelRequest
{
    public string UserId { get; set; }
    public string ChannelId { get; set; }
    public string? Name { get; set; }
    public string? Avatar { get; set; }
    public string? Banner { get; set; }
}

