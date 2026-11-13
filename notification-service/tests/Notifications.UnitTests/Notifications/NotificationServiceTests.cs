using DevOpsStore.BuildingBlocks.Results;
using Notifications.Application.Abstractions;
using Notifications.Application.Notifications;
using Notifications.Domain.Entities;
using Notifications.UnitTests.Fakes;

namespace Notifications.UnitTests.Notifications;

public class NotificationServiceTests
{
    private readonly RecordingSender _sender = new();
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _service = new NotificationService(new InMemoryNotificationRepository(), _sender, TimeProvider.System);
    }

    [Fact]
    public async Task Send_ValidRequest_StoresAndDelivers()
    {
        var result = await _service.SendAsync(new NotificationRequest("a@example.com", "Hello", "Body", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Id);
        Assert.Equal(Notification.DefaultChannel, result.Value.Channel);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task Send_NormalizesChannel()
    {
        var result = await _service.SendAsync(new NotificationRequest("a@example.com", "Hello", "Body", " SMS "));

        Assert.Equal("sms", result.Value.Channel);
    }

    [Fact]
    public async Task Send_MissingFields_ReturnsValidationErrorAndSendsNothing()
    {
        var result = await _service.SendAsync(new NotificationRequest("", "", "", null));

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(3, result.Error.ValidationErrors!.Count);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task GetAll_ReturnsNewestFirst()
    {
        await _service.SendAsync(new NotificationRequest("a@example.com", "First", "Body", null));
        await _service.SendAsync(new NotificationRequest("a@example.com", "Second", "Body", null));

        var all = await _service.GetAllAsync();

        Assert.Equal(["Second", "First"], all.Select(n => n.Subject));
    }

    private sealed class RecordingSender : INotificationSender
    {
        public List<Notification> Sent { get; } = [];

        public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SendForEvent_SameMessageTwice_StoresOnce()
    {
        var messageId = Guid.NewGuid();
        var draft = new EventNotification(messageId, 7, NotificationTypes.OrderConfirmed, "a@example.com", "Order #7 confirmed", "Thanks");

        await _service.SendForEventAsync(draft);
        await _service.SendForEventAsync(draft);

        var stored = Assert.Single(await _service.GetAllAsync());
        Assert.Equal((7, "OrderConfirmed"), (stored.OrderId, stored.Type));
        Assert.Single(_sender.Sent);
    }
}
