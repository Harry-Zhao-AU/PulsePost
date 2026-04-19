using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PulsePost.Functions.Functions;
using Xunit;
using PulsePost.Functions.Models;
using PulsePost.Functions.Services;

namespace PulsePost.Functions.Tests.Functions;

public class PostArticleTests
{
    private readonly Mock<IDraftStorageService> _storage = new();
    private readonly Mock<IPublishService> _publish = new();
    private readonly Mock<ITelegramService> _telegram = new();
    private readonly PostArticle _sut;

    private static readonly ArticleDraft SampleDraft = new()
    {
        RowKey = "20240101120000",
        Topic = "AI Trends",
        Article = "Content here.",
        ImagePrompt = "An AI image",
        XThread = """{"tweets":["tweet 1","tweet 2"]}"""
    };

    public PostArticleTests()
    {
        _telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _publish.Setup(p => p.CreateBlogPrAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new PostArticle(_storage.Object, _publish.Object, _telegram.Object, NullLogger<PostArticle>.Instance);
    }

    [Fact]
    public async Task Run_NoPendingDraft_DoesNotPublishOrNotify()
    {
        _storage.Setup(s => s.GetLatestPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync((ArticleDraft?)null);

        await _sut.Run("someRowKey", CancellationToken.None);

        _publish.VerifyNoOtherCalls();
        _telegram.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_PendingDraftExists_CreatesBlogPrAndUpdatesStatusToApproved()
    {
        _storage.Setup(s => s.GetLatestPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SampleDraft);

        await _sut.Run("20240101120000", CancellationToken.None);

        _publish.Verify(p => p.CreateBlogPrAsync("AI Trends", "Content here.", "An AI image", It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.UpdateStatusAsync("20240101120000", "approved", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_PendingDraftExists_SendsTelegramWithXThreadAndImagePrompt()
    {
        _storage.Setup(s => s.GetLatestPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SampleDraft);

        await _sut.Run("20240101120000", CancellationToken.None);

        _telegram.Verify(t => t.SendMessageAsync(
            It.Is<string>(m => m.Contains("tweet 1") && m.Contains("Approved") && m.Contains("An AI image")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
