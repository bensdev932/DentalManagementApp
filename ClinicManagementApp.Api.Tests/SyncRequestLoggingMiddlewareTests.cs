using System.Text;
using ClinicManagementApp.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClinicManagementApp.Api.Tests;

public class SyncRequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NonSyncPath_SkipsLoggingAndPassesThrough()
    {
        var loggerMock = new Mock<ILogger<SyncRequestLoggingMiddleware>>();
        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SyncRequestLoggingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/patients";

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.Headers.ContainsKey("X-Correlation-Id").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_SyncPath_EchoesCorrelationId_AndPassesBodiesIntact()
    {
        var loggerMock = new Mock<ILogger<SyncRequestLoggingMiddleware>>();
        loggerMock.Setup(l => l.BeginScope(It.IsAny<Dictionary<string, object>>()))
            .Returns(Mock.Of<IDisposable>());

        string receivedReqBody = "";
        RequestDelegate next = async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            receivedReqBody = await reader.ReadToEndAsync();
            var responseBytes = Encoding.UTF8.GetBytes("{\"patientName\":\"Jane Doe\",\"status\":\"ok\"}");
            await ctx.Response.Body.WriteAsync(responseBytes);
        };

        var middleware = new SyncRequestLoggingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/sync/push";
        context.Request.Headers["X-Correlation-Id"] = "test-cid-12345";

        var reqBytes = Encoding.UTF8.GetBytes("{\"patientName\":\"John Doe\",\"phone\":\"09171234567\"}");
        context.Request.Body = new MemoryStream(reqBytes);
        context.Request.ContentLength = reqBytes.Length;

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("test-cid-12345");
        receivedReqBody.Should().Be("{\"patientName\":\"John Doe\",\"phone\":\"09171234567\"}");

        responseStream.Position = 0;
        using var resReader = new StreamReader(responseStream);
        var finalResponse = await resReader.ReadToEndAsync();
        finalResponse.Should().Be("{\"patientName\":\"Jane Doe\",\"status\":\"ok\"}");
    }

    [Fact]
    public async Task InvokeAsync_ClientLogsPath_DoesNotReadBodies_AndLogsMetadataOnly()
    {
        var loggerMock = new Mock<ILogger<SyncRequestLoggingMiddleware>>();
        loggerMock.Setup(l => l.BeginScope(It.IsAny<Dictionary<string, object>>()))
            .Returns(Mock.Of<IDisposable>());

        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new SyncRequestLoggingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/sync/client-logs";
        context.Request.ContentLength = 1024;

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.Headers.ContainsKey("X-Correlation-Id").Should().BeTrue();
    }
}

