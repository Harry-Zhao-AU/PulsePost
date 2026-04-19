using PulsePost.Functions.Services;
using Xunit;

namespace PulsePost.Functions.Tests.Services;

public class CodeFenceTests
{
    [Theory]
    [InlineData("{\"title\":\"foo\"}", "{\"title\":\"foo\"}")]
    [InlineData("  {\"title\":\"foo\"}  ", "{\"title\":\"foo\"}")]
    [InlineData("```json\n{\"title\":\"foo\"}\n```", "{\"title\":\"foo\"}")]
    [InlineData("```\n{\"title\":\"foo\"}\n```", "{\"title\":\"foo\"}")]
    [InlineData("Here is the JSON:\n```json\n{\"title\":\"foo\"}\n```", "{\"title\":\"foo\"}")]
    [InlineData("Sure!\n{\"title\":\"foo\"}", "{\"title\":\"foo\"}")]
    [InlineData("{\"a\":{\"b\":1}}", "{\"a\":{\"b\":1}}")]
    public void StripCodeFences_ReturnsCleanJson(string input, string expected)
    {
        Assert.Equal(expected, OpenAIService.StripCodeFences(input));
    }

    [Fact]
    public void StripCodeFences_StrippedJsonIsValidAndParseable()
    {
        var input = "Here is your JSON:\n```json\n{\"title\": \"AI Agents\", \"angles\": [\"a\", \"b\"]}\n```\nHope that helps!";
        var result = OpenAIService.StripCodeFences(input);
        var doc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result);
        Assert.Equal("AI Agents", doc.GetProperty("title").GetString());
    }

    [Fact]
    public void StripCodeFences_NoJson_ReturnsTrimmed()
    {
        var input = "  plain text response  ";
        Assert.Equal("plain text response", OpenAIService.StripCodeFences(input));
    }
}
