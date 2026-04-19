using Xunit;

namespace PulsePost.Functions.Tests.Services;

/// <summary>
/// Integration tests — verify external feed URLs are reachable.
/// Run manually or in CI with [Trait("Category","Integration")].
/// </summary>
public class FeedUrlTests
{
    private static readonly HttpClient Http = new();

    public static IEnumerable<object[]> CompanyFeedUrls =>
    [
        ["OpenAI", "https://openai.com/blog/rss.xml"],
        ["Google DeepMind", "https://deepmind.google/blog/rss.xml"],
        ["Meta AI", "https://ai.meta.com/blog/rss/"],
        ["Microsoft AI", "https://blogs.microsoft.com/ai/feed/"],
    ];

    public static IEnumerable<object[]> YouTubeChannelIds =>
    [
        ["Andrej Karpathy", "UCnUYZLuoy1rq1aVMwx4aTzw"],
        ["Yannic Kilcher",  "UCZHmQk67mSJgfCCTn7xBfew"],
        ["Two Minute Papers","UCbfYPyITQ-7l4upoX8nvctg"],
        ["AI Explained",    "UCNJ1Ymd5yFuUPtn21xtRbbw"],
        ["Matt Wolfe",      "UChpleBmo18P08aKCIgti38g"],
        ["Google DeepMind", "UCP7jMXSY2xbc3KCAE0MHQ-A"],
        ["Lex Fridman",     "UCSHZKyawb77ixDdsGog4iWA"],
    ];

    [Theory(Skip = "Integration — run manually"), MemberData(nameof(CompanyFeedUrls))]
    public async Task CompanyFeed_Returns200(string name, string url)
    {
        var response = await Http.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode,
            $"{name} RSS feed returned {(int)response.StatusCode}: {url}");
    }

    [Theory(Skip = "Integration — run manually"), MemberData(nameof(YouTubeChannelIds))]
    public async Task YouTubeChannel_Returns200(string name, string channelId)
    {
        var url = $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";
        var response = await Http.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode,
            $"{name} YouTube feed returned {(int)response.StatusCode}: {url}");
    }
}
