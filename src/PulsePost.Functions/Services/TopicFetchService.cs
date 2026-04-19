using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace PulsePost.Functions.Services;

public class TopicFetchService(HttpClient httpClient, ILogger<TopicFetchService> logger) : ITopicFetchService
{
    private static readonly string[] HnKeywords =
        ["ai", "llm", "agent", "mcp", "rag", "vector", "gpt", "claude",
         "neural", "transformer", "embedding", "copilot", "openai", "anthropic",
         "gemini", "mistral", "llama", "deepmind", "diffusion"];

    private static readonly (string Name, string Url)[] CompanyFeeds =
    [
        ("OpenAI", "https://openai.com/blog/rss.xml"),
        ("Google DeepMind", "https://deepmind.google/blog/rss.xml"),
        ("Meta AI", "https://ai.meta.com/blog/rss/"),
    ];

    private static readonly (string Name, string ChannelId)[] YoutubeChannels =
    [
        ("Andrej Karpathy", "UCnUYZLuoy1rq1aVMwx4aTzw"),
        ("Yannic Kilcher", "UCZHmQk67mSJgfCCTn7xBfew"),
        ("Two Minute Papers", "UCbfYPyITQ-7l4upoX8nvctg"),
        ("AI Explained", "UCNJ1Ymd5yFuUPtn21xtRbbw"),
        ("Matt Wolfe", "UChpleBmo18P08aKCIgti38g"),
        ("Google DeepMind", "UCP7jMXSY2xbc3KCAE0MHQ-A"),
        ("Lex Fridman", "UCSHZKyawb77ixDdsGog4iWA"),
    ];

    public async Task<string> FetchAllTopicsAsync(CancellationToken ct = default)
    {
        var tasks = new[]
        {
            FetchHackerNewsAsync(ct),
            FetchCompanyBlogsAsync(ct),
            FetchYouTubeAsync(ct),
            FetchArxivAsync(ct),
        };

        var results = await Task.WhenAll(tasks);

        var combined = new
        {
            hackernews = results[0],
            company_blogs = results[1],
            youtube = results[2],
            arxiv = results[3],
        };

        return JsonSerializer.Serialize(combined);
    }

    private async Task<List<object>> FetchHackerNewsAsync(CancellationToken ct)
    {
        var results = new List<object>();
        try
        {
            var ids = await httpClient.GetFromJsonAsync<List<int>>(
                "https://hacker-news.firebaseio.com/v0/topstories.json", ct) ?? [];

            foreach (var id in ids.Take(200))
            {
                var story = await httpClient.GetFromJsonAsync<JsonElement>(
                    $"https://hacker-news.firebaseio.com/v0/item/{id}.json", ct);

                var title = story.GetProperty("title").GetString()?.ToLower() ?? "";
                if (HnKeywords.Any(kw => title.Contains(kw)))
                {
                    results.Add(new
                    {
                        source = "hackernews",
                        title = story.GetProperty("title").GetString(),
                        url = story.TryGetProperty("url", out var u) ? u.GetString() : "",
                        score = story.TryGetProperty("score", out var s) ? s.GetInt32() : 0,
                    });
                }

                if (results.Count >= 10) break;
                await Task.Delay(50, ct);
            }
        }
        catch (Exception ex) { logger.LogError(ex, "HN fetch failed"); }

        return results.OrderByDescending(r => ((dynamic)r).score).Take(5).ToList();
    }

    private async Task<List<object>> FetchCompanyBlogsAsync(CancellationToken ct)
    {
        var results = new List<object>();
        foreach (var (name, url) in CompanyFeeds)
        {
            try
            {
                var xml = SanitizeXml(await httpClient.GetStringAsync(url, ct));
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.GetDefaultNamespace();
                var items = doc.Descendants("item").Take(3);

                foreach (var item in items)
                {
                    results.Add(new
                    {
                        source = "company_blog",
                        company = name,
                        title = item.Element("title")?.Value ?? "",
                        url = item.Element("link")?.Value ?? "",
                        summary = item.Element("description")?.Value?[..Math.Min(300, item.Element("description")?.Value?.Length ?? 0)] ?? "",
                        published = item.Element("pubDate")?.Value ?? "",
                    });
                }
            }
            catch (Exception ex) { logger.LogError(ex, "Blog fetch failed for {Name}", name); }
        }
        return results;
    }

    private async Task<List<object>> FetchRedditAsync(CancellationToken ct)
    {
        var results = new List<object>();
        var subreddits = new[] { "MachineLearning", "LocalLLaMA" };

        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PulsePost/1.0");

        foreach (var sub in subreddits)
        {
            try
            {
                var data = await httpClient.GetFromJsonAsync<JsonElement>(
                    $"https://www.reddit.com/r/{sub}/hot.json?limit=5", ct);

                foreach (var post in data.GetProperty("data").GetProperty("children").EnumerateArray())
                {
                    var d = post.GetProperty("data");
                    results.Add(new
                    {
                        source = "reddit",
                        subreddit = sub,
                        title = d.GetProperty("title").GetString(),
                        url = $"https://reddit.com{d.GetProperty("permalink").GetString()}",
                        score = d.GetProperty("score").GetInt32(),
                    });
                }
            }
            catch (Exception ex) { logger.LogError(ex, "Reddit fetch failed for r/{Sub}", sub); }
        }

        return results.OrderByDescending(r => ((dynamic)r).score).Take(5).ToList();
    }

    private async Task<List<object>> FetchYouTubeAsync(CancellationToken ct)
    {
        var results = new List<object>();
        foreach (var (name, channelId) in YoutubeChannels)
        {
            try
            {
                var xml = SanitizeXml(await httpClient.GetStringAsync(
                    $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}", ct));
                var doc = XDocument.Parse(xml);
                var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
                var media = XNamespace.Get("http://search.yahoo.com/mrss/");

                foreach (var entry in doc.Descendants(ns + "entry").Take(2))
                {
                    results.Add(new
                    {
                        source = "youtube",
                        channel = name,
                        title = entry.Element(ns + "title")?.Value ?? "",
                        url = entry.Element(ns + "link")?.Attribute("href")?.Value ?? "",
                        description = entry.Descendants(media + "description").FirstOrDefault()?.Value?[..Math.Min(300, entry.Descendants(media + "description").FirstOrDefault()?.Value?.Length ?? 0)] ?? "",
                        published = entry.Element(ns + "published")?.Value ?? "",
                    });
                }
            }
            catch (Exception ex) { logger.LogError(ex, "YouTube fetch failed for {Name}", name); }
        }
        return results;
    }

    private async Task<List<object>> FetchArxivAsync(CancellationToken ct)
    {
        var results = new List<object>();
        try
        {
            var xml = SanitizeXml(await httpClient.GetStringAsync("https://rss.arxiv.org/rss/cs.AI", ct));
            var doc = XDocument.Parse(xml);

            foreach (var item in doc.Descendants("item").Take(8))
            {
                results.Add(new
                {
                    source = "arxiv",
                    title = item.Element("title")?.Value ?? "",
                    url = item.Element("link")?.Value ?? "",
                    description = item.Element("description")?.Value?[..Math.Min(300, item.Element("description")?.Value?.Length ?? 0)] ?? "",
                });
            }
        }
        catch (Exception ex) { logger.LogError(ex, "arXiv fetch failed"); }
        return results;
    }

    internal static string SanitizeXml(string xml) =>
        Regex.Replace(xml, @"&(?!(amp|lt|gt|quot|apos|#\d+|#x[0-9a-fA-F]+);)", "&amp;");
}
