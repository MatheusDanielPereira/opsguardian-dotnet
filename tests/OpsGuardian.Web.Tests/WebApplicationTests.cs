using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsGuardian.Infrastructure.Persistence;

namespace OpsGuardian.Web.Tests;

public sealed class WebApplicationTests(OpsGuardianWebApplicationFactory factory)
    : IClassFixture<OpsGuardianWebApplicationFactory>
{
    [Fact]
    public async Task DashboardReturnsSuccessAndSecurityHeaders()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Incident command center", content, StringComparison.Ordinal);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadinessHealthCheckReportsHealthyDatabase()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateIncidentPersistsIncidentAndOutboxMessage()
    {
        using var client = CreateClient(allowAutoRedirect: false);
        using var formPage = await client.GetAsync("/Incidents/Create");
        var html = await formPage.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(html);

        Dictionary<string, string> form = new()
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Title"] = "Payments API is degraded",
            ["Input.Description"] = "Latency has exceeded the service objective for ten minutes.",
            ["Input.Severity"] = "4",
            ["Input.AssignedEngineer"] = "SRE Team",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Incidents/Create")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Text.Html));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Incidents/Details/", response.Headers.Location?.OriginalString, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OpsGuardianDbContext>();
        Assert.Equal(1, await dbContext.Incidents.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    private HttpClient CreateClient(bool allowAutoRedirect = true) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            BaseAddress = new Uri("https://localhost"),
        });

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        Assert.True(match.Success, "The antiforgery token was not rendered.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }
}
