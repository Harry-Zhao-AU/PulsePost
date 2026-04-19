using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PulsePost.Functions.Functions;
using PulsePost.Functions.Models;
using PulsePost.Functions.Services;

namespace PulsePost.Functions.Tests.Functions;

public class TelegramWebhookTests : IDisposable
{
    private const string AllowedChatId = "12345";
    private const long AllowedChatIdLong = 12345L;

    private readonly Mock<IDraftStorageService> _storage = new();
    private readonly Mock<IOpenAIService> _openAI = new();
    private readonly Mock<ITelegramService> _telegram = new();
    private readonly Mock<ServiceBusClient> _sbClient = new();
    private readonly Mock<ServiceBusSender> _sbSender = new();
    private readonly TelegramWebhook _sut;

    public TelegramWebhookTests()
    {
        Environment.SetEnvironmentVariable("TELEGRAM_CHAT_ID", AllowedChatId);
        _sbClient.Setup(c => c.CreateSender(It.IsAny<string>())).Returns(_sbSender.Object);
        _sbSender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _telegram.Setup(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new TelegramWebhook(_storage.Object, _openAI.Object, _telegram.Object, _sbClient.Object, NullLogger<TelegramWebhook>.Instance);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("TELEGRAM_CHAT_ID", null);

    private static HttpRequest MakeRequest(TelegramUpdate update)
    {
        var json = JsonSerializer.Serialize(update);
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.ContentType = "application/json";
        return context.Request;
    }

    private static HttpRequest MakeRequest(string text, long chatId = AllowedChatIdLong) =>
        MakeRequest(new TelegramUpdate
        {
            Message = new TelegramMessage { Text = text, Chat = new TelegramChat { Id = chatId } }
        });

    [Fact]
    public async Task Run_NullMessage_ReturnsOkImmediately()
    {
        var req = MakeRequest(new TelegramUpdate { Message = null });

        var result = await _sut.Run(req, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        _storage.VerifyNoOtherCalls();
        _telegram.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_UnknownChatId_ReturnsOkWithoutProcessing()
    {
        var req = MakeRequest("/generate", chatId: 99999L);

        var result = await _sut.Run(req, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        _telegram.VerifyNoOtherCalls();
        _sbClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_GenerateCommand_SendsToFetchTopicsQueueAndNotifies()
    {
        var req = MakeRequest("/generate");

        await _sut.Run(req, CancellationToken.None);

        _sbClient.Verify(c => c.CreateSender("fetch-topics"), Times.Once);
        _sbSender.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _telegram.Verify(t => t.SendMessageAsync(It.Is<string>(m => m.Contains("Pipeline started")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_ApproveWithNoPendingDraft_SendsNoPendingWarning()
    {
        _storage.Setup(s => s.GetLatestPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync((ArticleDraft?)null);
        var req = MakeRequest("APPROVE");

        await _sut.Run(req, CancellationToken.None);

        _telegram.Verify(t => t.SendMessageAsync(It.Is<string>(m => m.Contains("No pending draft")), It.IsAny<CancellationToken>()), Times.Once);
        _sbClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_ApproveWithDraft_SendsToPostArticleQueueAndNotifies()
    {
        var draft = new ArticleDraft { RowKey = "20240101120000" };
        _storage.Setup(s => s.GetLatestPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        var req = MakeRequest("APPROVE");

        await _sut.Run(req, CancellationToken.None);

        _sbClient.Verify(c => c.CreateSender("post-article"), Times.Once);
        _telegram.Verify(t => t.SendMessageAsync(It.Is<string>(m => m.Contains("Publishing")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_RejectWithDraft_UpdatesStatusToRejectedAndNotifies()
    {
        var draft = new ArticleDraft { RowKey = "20240101120000" };
        _storage.Setup(s => s.GetLatestPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        _storage.Setup(s => s.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var req = MakeRequest("REJECT");

        await _sut.Run(req, CancellationToken.None);

        _storage.Verify(s => s.UpdateStatusAsync("20240101120000", "rejected", It.IsAny<CancellationToken>()), Times.Once);
        _telegram.Verify(t => t.SendMessageAsync(It.Is<string>(m => m.Contains("rejected")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_EditWithFeedback_RewritesArticleAndKeepsPendingStatus()
    {
        var draft = new ArticleDraft { RowKey = "20240101120000", Article = "old article" };
        _storage.Setup(s => s.GetLatestPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        _storage.Setup(s => s.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _openAI.Setup(o => o.RewriteArticleAsync("old article", "more technical", It.IsAny<CancellationToken>()))
            .ReturnsAsync("new article");
        _openAI.Setup(o => o.GenerateXThreadAsync("new article", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"tweets":["tweet1","tweet2"]}""");
        var req = MakeRequest("EDIT more technical");

        await _sut.Run(req, CancellationToken.None);

        Assert.Equal("new article", draft.Article);
        _openAI.Verify(o => o.RewriteArticleAsync("old article", "more technical", It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.UpdateStatusAsync("20240101120000", "pending", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_EditWithEmptyFeedback_SendsWarningAndSkipsRewrite()
    {
        var draft = new ArticleDraft { RowKey = "20240101120000" };
        _storage.Setup(s => s.GetLatestPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        var req = MakeRequest("EDIT");

        await _sut.Run(req, CancellationToken.None);

        _openAI.Verify(o => o.RewriteArticleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _telegram.Verify(t => t.SendMessageAsync(It.Is<string>(m => m.Contains("Please provide feedback")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_UnknownCommand_SendsUnknownCommandMessage()
    {
        var draft = new ArticleDraft { RowKey = "20240101120000" };
        _storage.Setup(s => s.GetLatestPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        var req = MakeRequest("HELLO");

        await _sut.Run(req, CancellationToken.None);

        _telegram.Verify(t => t.SendMessageAsync(It.Is<string>(m => m.Contains("Unknown command")), It.IsAny<CancellationToken>()), Times.Once);
    }
}
