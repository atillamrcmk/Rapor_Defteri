namespace RaporDefteri.Domain;

public enum ReportEntryType
{
    HospitalDeparture = 1,
    HospitalReturn = 2,
    CourtDeparture = 3,
    CourtReturn = 4,
    OtherDeparture = 5,
    OtherReturn = 6,
    Admission = 7,
    Release = 8,
    Transfer = 9,
    VehiclePassage = 10,
    Visitor = 11,
    MaterialDelivery = 12,
    GeneralEvent = 13,
    HandoverNote = 14
}

public enum ReportEntryStatus
{
    Valid = 1,
    Corrected = 2,
    Cancelled = 3
}

public enum ShiftStatus
{
    Open = 1,
    ReadyForHandover = 2,
    Closed = 3
}

public enum CustodyStatus
{
    Active = 1,
    Released = 2,
    Transferred = 3,
    Cancelled = 4
}

public enum TemporaryMovementKind
{
    Hospital = 1,
    Court = 2,
    Other = 3
}

public enum TemporaryAbsenceStatus
{
    Open = 1,
    Returned = 2,
    Cancelled = 3
}

public enum VehicleBaseLocation
{
    Unspecified = 0,
    External = 1,
    Institution = 2
}

public enum PassageDirection
{
    Entry = 1,
    Exit = 2
}

public enum VehicleVisitStatus
{
    Open = 1,
    Completed = 2,
    Standalone = 3
}

public enum LocationInference
{
    None = 0,
    AwaitingExit = 1,
    AwaitingReturn = 2
}

public enum MaterialDirection
{
    Received = 1,
    Delivered = 2
}

public enum SnapshotKind
{
    ShiftClose = 1,
    DailyLock = 2
}

public enum ReturnWatch
{
    NotScheduled = 1,
    Waiting = 2,
    ExpectedTimePassed = 3
}
