namespace OptimizelySite.Lab;

/// <summary>
/// Configuration for the fault-injection lab. Every fault endpoint is disabled
/// unless <see cref="Enabled"/> is true and <see cref="Key"/> matches the caller's
/// key. A deployment that forgets the key fails closed (404), never open.
/// </summary>
public sealed class LabOptions
{
    public const string SectionName = "Lab";

    /// <summary>Master switch. Off by default so a deployed site cannot be stressed accidentally.</summary>
    public bool Enabled { get; set; }

    /// <summary>Shared key the operator passes as <c>?key=</c>. Never rendered into a page.</summary>
    public string? Key { get; set; }

    /// <summary>Upper bound for a single memory allocation, in megabytes.</summary>
    public int MaxMemoryMb { get; set; } = 512;

    /// <summary>Upper bound for a CPU burn, in seconds.</summary>
    public int MaxCpuSeconds { get; set; } = 30;

    /// <summary>Upper bound for how long a fault may hold resources, in seconds.</summary>
    public int MaxHoldSeconds { get; set; } = 60;

    /// <summary>Upper bound for a slow SQL statement, in milliseconds.</summary>
    public int MaxSqlDelayMs { get; set; } = 30_000;

    /// <summary>Upper bound for blocked thread-pool workers.</summary>
    public int MaxThreads { get; set; } = 64;

    /// <summary>Upper bound for the number of content reads in a content storm.</summary>
    public int MaxContentReads { get; set; } = 20_000;

    /// <summary>Upper bound for messages pushed in a Service Bus storm.</summary>
    public int MaxServiceBusMessages { get; set; } = 500;

    /// <summary>Azure Service Bus connection string (server-side only). Empty disables the storm.</summary>
    public string? ServiceBusConnection { get; set; }

    /// <summary>Queue or topic that receives the storm.</summary>
    public string? ServiceBusQueue { get; set; }

    /// <summary>True only when the lab is switched on AND has a key to compare against.</summary>
    public bool IsUsable => Enabled && !string.IsNullOrWhiteSpace(Key);
}
