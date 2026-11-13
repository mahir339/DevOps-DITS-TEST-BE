using DevOpsStore.BuildingBlocks.Http;
using DevOpsStore.BuildingBlocks.Web.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevOpsStore.BuildingBlocks.UnitTests;

public class CorrelationIdTests
{
    private static async Task<string?> RunMiddlewareAsync(string? incomingHeader)
    {
        var context = new DefaultHttpContext();
        if (incomingHeader is not null)
            context.Request.Headers[CorrelationId.HeaderName] = incomingHeader;

        string? seenByNext = null;
        var middleware = new CorrelationIdMiddleware(
            ctx => { seenByNext = CorrelationId.Get(ctx); return Task.CompletedTask; },
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        return seenByNext;
    }

    [Fact]
    public async Task Middleware_ValidIncomingHeader_IsReused()
    {
        Assert.Equal("abc-123", await RunMiddlewareAsync("abc-123"));
    }

    [Fact]
    public async Task Middleware_NoHeader_GeneratesNewId()
    {
        var id = await RunMiddlewareAsync(null);

        Assert.True(CorrelationId.IsValid(id));
        Assert.Equal(32, id!.Length);
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("line\nbreak")]
    [InlineData("<script>")]
    public async Task Middleware_UnsafeHeader_IsReplaced(string unsafeValue)
    {
        var id = await RunMiddlewareAsync(unsafeValue);

        Assert.NotEqual(unsafeValue, id);
        Assert.True(CorrelationId.IsValid(id));
    }

    [Fact]
    public void IsValid_RejectsTooLongValues()
    {
        Assert.False(CorrelationId.IsValid(new string('a', 65)));
        Assert.True(CorrelationId.IsValid(new string('a', 64)));
    }

    [Fact]
    public async Task PropagationHandler_AddsCurrentCorrelationIdToOutgoingRequest()
    {
        // An incoming request carrying X-Correlation-Id: req-42 makes an outgoing HttpClient call.
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationId.HeaderName] = "req-42";
        HttpRequestMessage? captured = null;

        await new CorrelationIdMiddleware(async ctx =>
        {
            var handler = new CorrelationIdPropagationHandler(new HttpContextAccessor { HttpContext = ctx })
            {
                InnerHandler = new CapturingHandler(r => captured = r),
            };
            using var invoker = new HttpMessageInvoker(handler);
            await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://downstream/health"), CancellationToken.None);
        }, NullLogger<CorrelationIdMiddleware>.Instance).InvokeAsync(context);

        Assert.Equal("req-42", Assert.Single(captured!.Headers.GetValues(CorrelationId.HeaderName)));
    }

    [Fact]
    public async Task PropagationHandler_OutsideRequest_AddsNothing()
    {
        HttpRequestMessage? captured = null;
        var handler = new CorrelationIdPropagationHandler(new HttpContextAccessor())
        {
            InnerHandler = new CapturingHandler(r => captured = r),
        };
        using var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://downstream/health"), CancellationToken.None);

        Assert.False(captured!.Headers.Contains(CorrelationId.HeaderName));
    }

    private sealed class CapturingHandler(Action<HttpRequestMessage> capture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            capture(request);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
