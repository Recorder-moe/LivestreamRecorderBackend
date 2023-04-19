#nullable disable warnings
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderBackend.DTO.User;

public class UpdateUserRequest
{
#pragma warning disable IDE1006 // 命名樣式
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string? Note { get; set; }
    public Referral? Referral { get; set; }
}