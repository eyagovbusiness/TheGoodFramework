using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TGF.CA.Application.Contracts.Routing;
using TGF.CA.Infrastructure.Licensing.Slascone;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

namespace TGF.CA.Infrastructure.Licensing.Tests.Unit.Infrastructure;

/// <summary>
/// The framework default exemption is /health and /health-ui only. Route constants such as /health/live and /health/ready exist, but a
/// consuming service must opt in to exempting them by passing them as allowed paths, exactly and without any prefix matching.
/// </summary>
public class LicensingGateMiddlewareTests {
    [Theory]
    [InlineData(TGFEndpointRoutes.health)]
    [InlineData(TGFEndpointRoutes.healthUi)]
    public async Task Invoke_ShouldExemptFrameworkDefaultPathsWithoutResolvingLicensing(string path) {
        var (middleware, next) = CreateMiddleware([]);
        var context = CreateContext(path, resolveLicensing: false);

        await middleware.Invoke(context);

        Assert.True(next.Called);
    }

    [Theory]
    [InlineData(TGFEndpointRoutes.healthLive)]
    [InlineData(TGFEndpointRoutes.healthReady)]
    [InlineData("/license/status")]
    public async Task Invoke_ShouldBlockNonCompliantRequestsToNonDefaultPathsWithFixedProblemBody(string path) {
        var (middleware, next) = CreateMiddleware([]);
        var context = CreateContext(path);

        await middleware.Invoke(context);

        Assert.False(next.Called);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Contains("license compliance requirements are not met", ReadBody(context));
    }

    [Fact]
    public async Task Invoke_ShouldExemptHealthLiveOnlyWhenTheConsumerPassesItExplicitly() {
        var (middleware, next) = CreateMiddleware([TGFEndpointRoutes.healthLive]);

        await middleware.Invoke(CreateContext(TGFEndpointRoutes.healthLive, resolveLicensing: false));
        Assert.True(next.Called);

        next.Called = false;
        var ready = CreateContext(TGFEndpointRoutes.healthReady);
        await middleware.Invoke(ready);
        Assert.False(next.Called);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ready.Response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live/extra")]
    [InlineData("/health/liveness")]
    [InlineData("/health/live/")]
    public async Task Invoke_ShouldMatchExplicitAllowedPathsExactlyWithoutPrefixes(string path) {
        var (middleware, next) = CreateMiddleware([TGFEndpointRoutes.healthLive]);
        var context = CreateContext(path);

        await middleware.Invoke(context);

        Assert.False(next.Called);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_ShouldAdmitCompliantRequests() {
        var (middleware, next) = CreateMiddleware([]);
        var context = CreateContext("/api/anything", LicenseComplianceStatus.Compliant);

        await middleware.Invoke(context);

        Assert.True(next.Called);
    }

    private static (LicensingGateMiddleware Middleware, NextProbe Next) CreateMiddleware(string[] allowedPaths) {
        var next = new NextProbe();
        return (new LicensingGateMiddleware(next.Invoke, allowedPaths), next);
    }

    private static DefaultHttpContext CreateContext(string path, LicenseComplianceStatus status = LicenseComplianceStatus.NonCompliant, bool resolveLicensing = true) {
        var services = new ServiceCollection();
        if (resolveLicensing) {
            var licensing = new Mock<ILicensingService>();
            licensing.SetupGet(service => service.ComplianceStatus).Returns(status);
            services.AddSingleton(licensing.Object);
        }
        // Without a registered ILicensingService, resolving it throws: exempt paths must therefore never touch licensing.

        return new DefaultHttpContext {
            RequestServices = services.BuildServiceProvider(),
            Request = { Path = path },
            Response = { Body = new MemoryStream() }
        };
    }

    private static string ReadBody(HttpContext context) {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    private sealed class NextProbe {
        public bool Called { get; set; }

        public Task Invoke(HttpContext _) {
            Called = true;
            return Task.CompletedTask;
        }
    }
}
