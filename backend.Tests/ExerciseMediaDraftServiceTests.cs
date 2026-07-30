using System.Text.Json;
using backend.Contracts;
using backend.Models;
using backend.Services;
using backend.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public class ExerciseMediaDraftServiceTests
{
    [Fact]
    public async Task CreateDraft_StoresPromptAndCurrentSourceSnapshot()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);

        var draft = await factory.CreateDraftService(context).CreateDraftAsync(
            exercise.Id,
            new CreateExerciseMediaDraftRequest(),
            createdByUserId: 11);

        Assert.NotNull(draft);
        Assert.Equal(ExerciseMediaDraftStatuses.NeedsReview, draft.Status);
        Assert.Equal(ExerciseMediaDraftMediaTypes.Video, draft.MediaType);
        Assert.Equal("v1", draft.PromptVersion);
        Assert.Contains("Barbell back squat", draft.PromptText);
        Assert.Contains("controlled tempo", draft.PromptText, StringComparison.OrdinalIgnoreCase);

        var snapshotJson = Assert.IsType<string>(draft.SourceSnapshotJson);
        using var snapshot = JsonDocument.Parse(snapshotJson);
        Assert.Equal(exercise.Id, snapshot.RootElement.GetProperty("exerciseId").GetInt32());
        Assert.Equal(exercise.Name, snapshot.RootElement.GetProperty("name").GetString());
        Assert.Equal(exercise.Source, snapshot.RootElement.GetProperty("source").GetString());
        Assert.Equal(exercise.ExternalId, snapshot.RootElement.GetProperty("externalId").GetString());
    }

    [Fact]
    public async Task ApproveDraft_AllowsNeedsReviewButRejectsInvalidStatus()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        var service = factory.CreateDraftService(context);

        var approved = await service.ApproveDraftAsync(
            draft.Id,
            new ReviewExerciseMediaDraftRequest { ReviewNotes = "Technique is correct." },
            reviewedByUserId: 12);

        Assert.NotNull(approved);
        Assert.Equal(ExerciseMediaDraftStatuses.Approved, approved.Status);
        Assert.Equal("Technique is correct.", approved.ReviewNotes);
        Assert.Equal(12, approved.ReviewedByUserId);

        var exception = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.ApproveDraftAsync(draft.Id, new ReviewExerciseMediaDraftRequest(), reviewedByUserId: 12));
        Assert.Contains("Only drafts", exception.Message);
    }

    [Fact]
    public async Task RejectDraft_AllowsUnpublishedDraftButRejectsPublishedDraft()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        var service = factory.CreateDraftService(context);

        var rejected = await service.RejectDraftAsync(
            draft.Id,
            new RejectExerciseMediaDraftRequest { RejectionReason = "Incorrect setup." },
            reviewedByUserId: 12);

        Assert.NotNull(rejected);
        Assert.Equal(ExerciseMediaDraftStatuses.Rejected, rejected.Status);
        Assert.Equal("Incorrect setup.", rejected.RejectionReason);

        var entity = await context.ExerciseMediaDrafts.SingleAsync(item => item.Id == draft.Id);
        entity.Status = ExerciseMediaDraftStatuses.Published;
        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.RejectDraftAsync(draft.Id, new RejectExerciseMediaDraftRequest(), reviewedByUserId: 12));
        Assert.Contains("Published drafts cannot", exception.Message);
    }

    [Fact]
    public async Task PublishDraft_RequiresApprovalAndGeneratedMedia()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        var service = factory.CreateDraftService(context);

        var notApproved = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.PublishDraftAsync(draft.Id, publishedByUserId: 13));
        Assert.Equal("Only approved drafts can be published.", notApproved.Message);

        await service.ApproveDraftAsync(draft.Id, new ReviewExerciseMediaDraftRequest(), reviewedByUserId: 12);
        var noMedia = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.PublishDraftAsync(draft.Id, publishedByUserId: 13));
        Assert.Equal("Draft has no generated media to publish.", noMedia.Message);
    }

    [Fact]
    public async Task PublishDraft_UsesLocalOverridesAndPreservesProviderMedia()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var originalVideoUrl = exercise.VideoUrl;
        var originalThumbnailUrl = exercise.ThumbnailUrl;
        var draft = await factory.CreateDraftAsync(context, exercise);
        await factory.AttachPrivateVideoAsync(context, draft);
        var service = factory.CreateDraftService(context);

        await service.ApproveDraftAsync(draft.Id, new ReviewExerciseMediaDraftRequest(), reviewedByUserId: 12);
        var published = await service.PublishDraftAsync(draft.Id, publishedByUserId: 13);

        Assert.NotNull(published);
        Assert.Equal(ExerciseMediaDraftStatuses.Published, published.Status);

        var updatedExercise = await context.ExerciseCatalogItems.SingleAsync(item => item.Id == exercise.Id);
        Assert.StartsWith("https://media.test/media/exercises/", updatedExercise.LocalVideoUrlOverride);
        Assert.Null(updatedExercise.LocalThumbnailUrlOverride);
        Assert.True(updatedExercise.IsManuallyEdited);
        Assert.Equal(originalVideoUrl, updatedExercise.VideoUrl);
        Assert.Equal(originalThumbnailUrl, updatedExercise.ThumbnailUrl);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("Source")]
    [InlineData("ExternalId")]
    public async Task PublishDraft_BlocksStaleSourceSnapshots(string changedProperty)
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        await factory.AttachPrivateVideoAsync(context, draft);
        var service = factory.CreateDraftService(context);
        await service.ApproveDraftAsync(draft.Id, new ReviewExerciseMediaDraftRequest(), reviewedByUserId: 12);

        switch (changedProperty)
        {
            case "Name":
                exercise.Name = "Changed name";
                break;
            case "Source":
                exercise.Source = "changed-source";
                break;
            case "ExternalId":
                exercise.ExternalId = "changed-external-id";
                break;
        }

        exercise.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.PublishDraftAsync(draft.Id, publishedByUserId: 13));
        Assert.Contains("source snapshot is stale", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
