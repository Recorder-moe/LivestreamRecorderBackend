﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#nullable disable warnings

namespace LivestreamRecorderBackend.DTO.User;

internal class GetUserResponse
{
#pragma warning disable IDE1006 // 命名樣式
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式
    public string UserName { get; set; }
    public string Email { get; set; }
    public string? Picture { get; set; }
    public DateTime RegistrationDate { get; set; }
    public string? Note { get; set; }
    public string? GoogleUID { get; set; }
}
