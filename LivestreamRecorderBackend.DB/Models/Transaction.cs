using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LivestreamRecorderBackend.DB.Enum;

namespace LivestreamRecorderBackend.DB.Models;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

[Table("Transactions")]
public class Transaction : Entity
{
    [Required]
    public override string id { get; set; }

    [Required]
    public TokenType TokenType { get; set; }

    [Required]
    public string UserId { get; set; }

    [Required]
    public TransactionType TransactionType { get; set; }

    [Required]
    public decimal Amount { get; set; }

    [Required]
    public DateTime Timestamp { get; set; }

    public string? ChannelId { get; set; }

    public string? Note { get; set; }

}

