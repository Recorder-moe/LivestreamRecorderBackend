namespace LivestreamRecorderBackend.DTO.Transaction;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

public class SupportChannelRequest
{
    public string UserId { get; set; }
    public string ChannelId { get; set; }
    public decimal Amount { get; set; }
}

