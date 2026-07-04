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

public enum CourierType
{
    Econt = 0,
    Speedy = 1,
    Dhl = 2,
    Other = 99
}

public enum ShipmentStatus
{
    Draft = 0,
    LabelGenerated = 1,
    HandedOver = 2,
    InTransit = 3,
    Delivered = 4,
    Cancelled = 5,
    Returned = 6
}
