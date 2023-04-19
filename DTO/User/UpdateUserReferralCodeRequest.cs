#nullable disable warnings

namespace LivestreamRecorderBackend.DTO.User;

public class AddUserReferralCodeRequest
{
#pragma warning disable IDE1006 // 命名樣式
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式
    public string ReferralCode { get; set; }
}