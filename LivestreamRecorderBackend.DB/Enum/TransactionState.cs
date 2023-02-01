namespace LivestreamRecorderBackend.DB.Enum;

public enum TransactionState
{
    Unknown = -1,
    Pending = 0,
    Success = 1,
    Cancel = 2,
    Failed = 3
}

