using System;

namespace ChangeSkin.Progress;

/// <summary>
/// Default <see cref="ILoadProgress"/> implementation backed by events.
/// Thread-safe: all callbacks are invoked synchronously but consumers can marshal to main thread.
/// </summary>
internal sealed class LoadProgress : ILoadProgress
{
    /// <summary>Fired on every report. Args: (phase, percent).</summary>
    public event Action<string, float> OnReport;

    /// <summary>Fired when the operation completes successfully.</summary>
    public event Action OnComplete;

    /// <summary>Fired when the operation fails. Arg: error message.</summary>
    public event Action<string> OnFail;

    private string _phase;
    private float _percent;

    public string Phase => _phase;
    public float Percent => _percent;
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }
    public string Error { get; private set; }

    public void ReportPhase(string phase)
    {
        _phase = phase;
        OnReport?.Invoke(_phase, _percent);
    }

    public void ReportPercent(float percent)
    {
        _percent = Clamp(percent, 0f, 1f);
        OnReport?.Invoke(_phase, _percent);
    }

    public void Report(string phase, float percent)
    {
        _phase = phase;
        _percent = Clamp(percent, 0f, 1f);
        OnReport?.Invoke(_phase, _percent);
    }

    public void Complete()
    {
        _percent = 1f;
        IsCompleted = true;
        OnReport?.Invoke(_phase, _percent);
        OnComplete?.Invoke();
    }

    public void Fail(string error)
    {
        Error = error;
        IsFailed = true;
        OnReport?.Invoke(_phase, _percent);
        OnFail?.Invoke(error);
    }

    public static float Clamp(float value, float min, float max) // Copied from Mathf.Clamp to avoid Unity dependency in this class
    {
        if (min > max)
        {
            Exception ex = new ArgumentException("min should be less than or equal to max");
            throw ex;
        }

        if (value < min)
        {
            return min;
        }
        else if (value > max)
        {
            return max;
        }

        return value;
    }
}
