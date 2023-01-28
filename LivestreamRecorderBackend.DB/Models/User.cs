﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LivestreamRecorderBackend.DB.Models;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

[Table("Users")]
public class User : Entity
{
    [Required]
    public override string id { get; set; }
    [Required]
    public string UserName { get; set; }
    [Required]
    public string Email { get; set; }

    public string? Picture { get; set; }

    public DateTime RegistrationDate { get; set; }

    public string? Note { get; set; }

    public string? GoogleUID { get; set; }

    public Tokens Tokens { get; set; }

}


public class Tokens
{
    public int SupportToken { get; set; } = 0;
    public int DownloadToken { get; set; } = 0;
}
