#nullable disable warnings
namespace LivestreamRecorderBackend.DTO.User;

public class UpdateUserRequest
{
#pragma warning disable IDE1006 // 命名樣式
    public string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式
    public string? UserName { get; set; }
    public string? Picture { get; set; }
    public string? Note { get; set; }
}