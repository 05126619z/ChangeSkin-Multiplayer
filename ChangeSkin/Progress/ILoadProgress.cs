namespace ChangeSkin.Progress;

/// <summary>
/// Abstraction for reporting load progress to any consumer (UI, log, network).
/// Implementations must be safe to call from any thread.
/// </summary>
internal interface ILoadProgress
{
    /// <summary>Human-readable phase name (e.g. "Downloading…", "Extracting…").</summary>
    void ReportPhase(string phase);

    /// <summary>Normalized progress [0..1].</summary>
    void ReportPercent(float percent);

    /// <summary>Combine phase + percent in one call.</summary>
    void Report(string phase, float percent);

    /// <summary>Mark the operation as successfully completed.</summary>
    void Complete();

    /// <summary>Mark the operation as failed.</summary>
    void Fail(string error);
}
