namespace CALAC.Domain.Enums;

public enum UserRole
{
    Operator = 0,
    Supervisor = 1,
    Admin = 2
}

public enum DeviceStatus
{
    Offline = 0,
    Online = 1,
    Maintenance = 2
}

public enum SyncOperationStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
