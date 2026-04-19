using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PulsePost.Functions.Functions;
using Xunit;
using PulsePost.Functions.Services;

namespace PulsePost.Functions.Tests.Functions;

public class FetchTopicsTests
{
    private readonly Mock<ITopicFetchService> _topicFetchService = new();
    private readonly FetchTopics _sut;

    public FetchTopicsTests()
    {
        _sut = new FetchTopics(_topicFetchService.Object, NullLogger<FetchTopics>.Instance);
    }

    [Fact]
    public async Task Run_ReturnsFetchedTopicsJson()
    {
        const string expected = """[{"title":"AI news"}]""";
        _topicFetchService.Setup(s => s.FetchAllTopicsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.Run("start", CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Run_DelegatesEntirelyToFetchService()
    {
        _topicFetchService.Setup(s => s.FetchAllTopicsAsync(It.IsAny<CancellationToken>())).ReturnsAsync("[]");

        await _sut.Run("any-message", CancellationToken.None);

        _topicFetchService.Verify(s => s.FetchAllTopicsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
