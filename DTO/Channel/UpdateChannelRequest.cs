namespace LivestreamRecorderBackend.DTO.Channel;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

public class UpdateChannelRequest
{
#pragma warning disable IDE1006 // 命名樣式
    // ReSharper disable once InconsistentNaming
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式
    public string Source { get; set; }
}
