namespace backend.Contracts;

public class ExerciseCatalogPageResponse
{
    public IReadOnlyList<ExerciseCatalogItemResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
