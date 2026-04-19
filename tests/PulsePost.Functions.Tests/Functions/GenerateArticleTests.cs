using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PulsePost.Functions.Functions;
using Xunit;
using PulsePost.Functions.Models;
using PulsePost.Functions.Services;

namespace PulsePost.Functions.Tests.Functions;

public class GenerateArticleTests
{
    private readonly Mock<IOpenAIService> _openAI = new();
    private readonly Mock<IDraftStorageService> _storage = new();
    private readonly Mock<ITelegramService> _telegram = new();
    private readonly GenerateArticle _sut;

    private const string TopicsJson = """[{"title":"GPT-5 Released","url":"https://example.com"}]""";
    private const string TopicJson = """{"title":"GPT-5 Released","sources":[{"name":"OpenAI","url":"https://openai.com"}]}""";
    private const string Article = "An article about GPT-5.";
    private const string XThreadJson = """{"tweets":["tweet1","tweet2"]}""";
    private const string ImagePrompt = "A futuristic robot";

    public GenerateArticleTests()
    {
        _openAI.Setup(o => o.SelectTopicAsync(TopicsJson, It.IsAny<CancellationToken>())).ReturnsAsync(TopicJson);
        _openAI.Setup(o => o.GenerateArticleAsync(TopicJson, It.IsAny<CancellationToken>())).ReturnsAsync(Article);
        _openAI.Setup(o => o.GenerateXThreadAsync(Article, It.IsAny<CancellationToken>())).ReturnsAsync(XThreadJson);
        _openAI.Setup(o => o.GenerateImagePromptAsync(Article, "GPT-5 Released", It.IsAny<CancellationToken>())).ReturnsAsync(ImagePrompt);
        _storage.Setup(s => s.PurgeOlderThanAsync(7, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _storage.Setup(s => s.SaveAsync(It.IsAny<ArticleDraft>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _sut = new GenerateArticle(_openAI.Object, _storage.Object, _telegram.Object, NullLogger<GenerateArticle>.Instance);
    }

    [Fact]
    public async Task Run_PurgesOldDraftsBeforeGenerating()
    {
        await _sut.Run(TopicsJson, CancellationToken.None);

        _storage.Verify(s => s.PurgeOlderThanAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_CallsAllOpenAIGenerationSteps()
    {
        await _sut.Run(TopicsJson, CancellationToken.None);

        _openAI.Verify(o => o.SelectTopicAsync(TopicsJson, It.IsAny<CancellationToken>()), Times.Once);
        _openAI.Verify(o => o.GenerateArticleAsync(TopicJson, It.IsAny<CancellationToken>()), Times.Once);
        _openAI.Verify(o => o.GenerateXThreadAsync(Article, It.IsAny<CancellationToken>()), Times.Once);
        _openAI.Verify(o => o.GenerateImagePromptAsync(Article, "GPT-5 Released", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_SavesDraftWithCorrectFields()
    {
        ArticleDraft? saved = null;
        _storage.Setup(s => s.SaveAsync(It.IsAny<ArticleDraft>(), It.IsAny<CancellationToken>()))
            .Callback<ArticleDraft, CancellationToken>((d, _) => saved = d)
            .Returns(Task.CompletedTask);

        await _sut.Run(TopicsJson, CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal("GPT-5 Released", saved.Topic);
        Assert.Equal(Article, saved.Article);
        Assert.Equal(XThreadJson, saved.XThread);
        Assert.Equal(ImagePrompt, saved.ImagePrompt);
        Assert.Equal("pending", saved.Status);
    }

    [Fact]
    public async Task Run_SendsTelegramWithTopicTitleAndApproveInstructions()
    {
        await _sut.Run(TopicsJson, CancellationToken.None);

        _telegram.Verify(t => t.SendMessageAsync(
            It.Is<string>(m => m.Contains("GPT-5 Released") && m.Contains("APPROVE")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
