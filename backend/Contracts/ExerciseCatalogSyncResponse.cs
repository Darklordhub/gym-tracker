namespace backend.Contracts;

public class ExerciseCatalogSyncResponse
{
    public string Provider { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Processed { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string Message { get; set; } = string.Empty;
}
