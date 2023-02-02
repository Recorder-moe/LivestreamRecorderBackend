using LivestreamRecorderBackend.DB.Enum;
using System;
using System.Collections.Generic;

#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。
namespace LivestreamRecorderBackend.DTO.Transaction;

internal class GetTransactionResponse : List<Transaction>
{
    public GetTransactionResponse() { }

    public GetTransactionResponse(IEnumerable<Transaction> transactions) : base(transactions) { }
}

internal class Transaction
{
#pragma warning disable IDE1006 // 命名樣式
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式

    public TokenType TokenType { get; set; }

    public string UserId { get; set; }

    public TransactionType TransactionType { get; set; }

    public decimal Amount { get; set; }

    public DateTime Timestamp { get; set; }

    public TransactionState TransactionState { get; set; }

    public string? ChannelId { get; set; }

    public string? Note { get; set; }

}

