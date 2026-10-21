namespace DragonBoyAccountManager.Core.Interop.Native;

[Flags]
public enum ModuleFilter : uint
{
    Default = 0x0,
    Modules32Bit = 0x01,
    Modules64Bit = 0x02,
    All = 0x03
}

[Flags]
public enum AllocationType : uint
{
    Commit = 0x00001000,
    Reserve = 0x00002000,
    Reset = 0x00080000,
    ResetUndo = 0x1000000,
    LargePages = 0x20000000,
    Physical = 0x00400000,
    TopDown = 0x00100000
}

[Flags]
public enum MemoryProtection : uint
{
    Execute = 0x10,
    ExecuteRead = 0x20,
    ExecuteReadWrite = 0x40,
    ExecuteWriteCopy = 0x80,
    NoAccess = 0x01,
    ReadOnly = 0x02,
    ReadWrite = 0x04,
    WriteCopy = 0x08,
    Guard = 0x100,
    NoCache = 0x200,
    WriteCombine = 0x400
}

[Flags]
public enum MemoryFreeType : uint
{
    Decommit = 0x4000,
    Release = 0x8000
}

[Flags]
public enum ThreadCreationFlags : uint
{
    None = 0,
    CreateSuspended = 0x00000004,
    StackSizeParamIsAReservation = 0x00010000
}

public enum WaitResult : uint
{
    Signaled = 0x00000000,
    Abandoned = 0x00000080,
    Timeout = 0x00000102,
    Failed = 0xFFFFFFFF
}

[Flags]
public enum ProcessAccessRights : uint
{
    Terminate = 0x0001,
    CreateThread = 0x0002,
    SetSessionId = 0x0004,
    VmOperation = 0x0008,
    VmRead = 0x0010,
    VmWrite = 0x0020,
    DupHandle = 0x0040,
    CreateProcess = 0x0080,
    SetQuota = 0x0100,
    SetInformation = 0x0200,
    QueryInformation = 0x0400,
    SuspendResume = 0x0800,
    QueryLimitedInformation = 0x1000,
    Synchronize = 0x00100000,
    StandardRightsRequired = 0x000F0000,
    AllAccess = StandardRightsRequired | Synchronize | 0xFFFF
}
