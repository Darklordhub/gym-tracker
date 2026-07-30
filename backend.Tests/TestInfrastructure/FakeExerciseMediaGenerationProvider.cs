using backend.Models;
using backend.Services;

namespace backend.Tests.TestInfrastructure;

internal class FakeExerciseMediaGenerationProvider : IExerciseMediaGenerationProvider
{
    public string ProviderName => "Fake";
    public int StartCalls { get; private set; }

    public virtual void ValidateConfiguration()
    {
    }

    public virtual Task<ExerciseMediaGenerationStartResult> StartGenerationAsync(
        ExerciseMediaDraft draft,
        CancellationToken cancellationToken = default)
    {
        StartCalls++;
        return Task.FromResult(new ExerciseMediaGenerationStartResult
        {
            Provider = ProviderName,
            Model = "test-model",
            ProviderJobId = "test-job",
        });
    }

    public virtual Task<ExerciseMediaGenerationRefreshResult> RefreshGenerationStatusAsync(
        ExerciseMediaDraft draft,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ExerciseMediaGenerationRefreshResult
        {
            State = ExerciseMediaGenerationState.Pending,
        });
    }
}

internal sealed class BlockingExerciseMediaGenerationProvider : FakeExerciseMediaGenerationProvider
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task StartEntered => _startEntered.Task;
    private readonly TaskCompletionSource _startEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async Task<ExerciseMediaGenerationStartResult> StartGenerationAsync(
        ExerciseMediaDraft draft,
        CancellationToken cancellationToken = default)
    {
        var result = await base.StartGenerationAsync(draft, cancellationToken);
        _startEntered.TrySetResult();
        await _release.Task.WaitAsync(cancellationToken);
        return result;
    }

    public void Release()
    {
        _release.TrySetResult();
    }
}
