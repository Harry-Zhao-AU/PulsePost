using PulsePost.Functions.Services;
using Xunit;

namespace PulsePost.Functions.Tests.Services;

public class XmlSanitizeTests
{
    [Theory]
    [InlineData("no entities here", "no entities here")]
    [InlineData("a &amp; b", "a &amp; b")]
    [InlineData("a &lt; b &gt; c", "a &lt; b &gt; c")]
    [InlineData("a &quot;b&quot;", "a &quot;b&quot;")]
    [InlineData("AT&T rocks", "AT&amp;T rocks")]
    [InlineData("foo &bar; baz", "foo &amp;bar; baz")]
    [InlineData("a &#160; b", "a &#160; b")]
    [InlineData("a &#xA0; b", "a &#xA0; b")]
    [InlineData("A&B &amp; C&D", "A&amp;B &amp; C&amp;D")]
    public void SanitizeXml_HandlesEntitiesCorrectly(string input, string expected)
    {
        Assert.Equal(expected, TopicFetchService.SanitizeXml(input));
    }

    [Fact]
    public void SanitizeXml_AllowsValidXmlToParse()
    {
        var raw = "<rss><channel><item><title>AT&amp;T Blog</title><link>https://example.com?a=1&amp;b=2</link></item></channel></rss>";
        var sanitized = TopicFetchService.SanitizeXml(raw);
        var doc = System.Xml.Linq.XDocument.Parse(sanitized);
        Assert.Equal("AT&T Blog", doc.Descendants("title").First().Value);
    }

    [Fact]
    public void SanitizeXml_FixesBareAmpersand_AllowsXmlToParse()
    {
        var raw = "<rss><channel><item><title>A&B Conference</title></item></channel></rss>";
        var sanitized = TopicFetchService.SanitizeXml(raw);
        var doc = System.Xml.Linq.XDocument.Parse(sanitized);
        Assert.Equal("A&B Conference", doc.Descendants("title").First().Value);
    }
}
