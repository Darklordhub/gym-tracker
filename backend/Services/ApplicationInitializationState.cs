namespace backend.Services;

public enum ApplicationInitializationStatus
{
    Pending,
    Succeeded,
    Failed,
}

public sealed class ApplicationInitializationState
{
    private readonly object _sync = new();
    private ApplicationInitializationStatus _status = ApplicationInitializationStatus.Pending;
    private string? _failureMessage;

    public ApplicationInitializationStatus Status
    {
        get
        {
            lock (_sync)
            {
                return _status;
            }
        }
    }

    public string? FailureMessage
    {
        get
        {
            lock (_sync)
            {
                return _failureMessage;
            }
        }
    }

    public bool IsReady => Status == ApplicationInitializationStatus.Succeeded;

    public void MarkSucceeded()
    {
        lock (_sync)
        {
            _status = ApplicationInitializationStatus.Succeeded;
            _failureMessage = null;
        }
    }

    public void MarkFailed(string failureMessage)
    {
        lock (_sync)
        {
            _status = ApplicationInitializationStatus.Failed;
            _failureMessage = failureMessage;
        }
    }
}
