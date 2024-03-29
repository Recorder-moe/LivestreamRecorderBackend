﻿namespace LivestreamRecorderBackend.DTO.Channel;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

public class AddChannelRequest
{
    public string Url { get; set; }
    public string? Name { get; set; }
    public string? Avatar { get; set; }
    public string? Banner { get; set; }
}

