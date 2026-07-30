using backend.Services;
using backend.Tests.TestInfrastructure;

namespace backend.Tests;

public class ExerciseMediaStorageServiceTests
{
    [Fact]
    public async Task DraftVideo_UsesPrivateStorageAndAdminPreviewRoute()
    {
        using var factory = new ExerciseMediaTestFactory();
        var storage = factory.CreateStorageService();
        await using var content = new MemoryStream(TestMediaFiles.ValidMp4());

        var stored = await storage.SaveGeneratedVideoAsync(4, 9, content, "video/mp4", "video.mp4");

        Assert.Equal("/api/admin/exercise-catalog/media-studio/9/video", stored.PreviewUrl);
        Assert.DoesNotContain("/media/", stored.PreviewUrl, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(factory.RootPath, "private", "exercises", "4", "drafts", "9", "video.mp4")));
        Assert.False(File.Exists(Path.Combine(factory.RootPath, "public", "exercises", "4", "drafts", "9", "video.mp4")));
    }

    [Fact]
    public async Task PublishPreparation_ProducesPublicUrlOnlyAfterPublication()
    {
        using var factory = new ExerciseMediaTestFactory();
        var storage = factory.CreateStorageService();
        await using var content = new MemoryStream(TestMediaFiles.ValidMp4());
        var stored = await storage.SaveGeneratedVideoAsync(4, 9, content, "video/mp4", "video.mp4");

        using var publication = await storage.PrepareDraftPublicationAsync(4, 9, stored.PreviewUrl, null);

        Assert.StartsWith("https://media.test/media/exercises/4/published/9/", publication.VideoPublicUrl);
        publication.CommitFiles();
        publication.Complete();

        var relativePath = new Uri(publication.VideoPublicUrl!).AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        Assert.True(File.Exists(Path.Combine(factory.RootPath, "public", relativePath["media/".Length..])));
    }

    [Fact]
    public async Task SaveGeneratedMedia_RejectsPathTraversal()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var content = new MemoryStream(TestMediaFiles.ValidMp4());

        await Assert.ThrowsAsync<ExerciseMediaStorageException>(() =>
            factory.CreateStorageService().SaveGeneratedVideoAsync(4, 9, content, "video/mp4", "../video.mp4"));
    }

    [Fact]
    public async Task SaveGeneratedMedia_RejectsUnsupportedContentType()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var content = new MemoryStream(TestMediaFiles.ValidMp4());

        await Assert.ThrowsAsync<ExerciseMediaStorageException>(() =>
            factory.CreateStorageService().SaveGeneratedVideoAsync(4, 9, content, "text/plain", "video.txt"));
    }

    [Fact]
    public async Task SaveGeneratedMedia_RejectsInvalidSignature()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var content = new MemoryStream("not an mp4"u8.ToArray());

        await Assert.ThrowsAsync<ExerciseMediaStorageException>(() =>
            factory.CreateStorageService().SaveGeneratedVideoAsync(4, 9, content, "video/mp4", "video.mp4"));
    }
}
