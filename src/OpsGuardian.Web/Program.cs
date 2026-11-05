using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpsGuardian.Infrastructure;
using OpsGuardian.Infrastructure.Health;
using OpsGuardian.Web.Security;
using OpsGuardian.Web.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizeFolder("/");
        options.Conventions.AllowAnonymousToPage("/Error");
        options.Conventions.AuthorizePage("/Incidents/Create", AuthorizationPolicies.WriteIncidents);
    });

ConfigureAuthentication(builder);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.WriteIncidents, policy =>
        policy.RequireRole(AuthorizationRoles.IncidentManager))
    .AddPolicy(AuthorizationPolicies.ResolveIncidents, policy =>
        policy.RequireRole(AuthorizationRoles.IncidentManager));

builder.Services.AddOpsGuardianInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
});

var useOtlp = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("OpsGuardian.Web"))
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
        if (useOtlp)
        {
            metrics.AddOtlpExporter();
        }
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
        if (useOtlp)
        {
            tracing.AddOtlpExporter();
        }
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
}).AllowAnonymous();

app.MapRazorPages();

await DevelopmentDatabaseInitializer.InitializeAsync(app);
await app.RunAsync();

static void ConfigureAuthentication(WebApplicationBuilder builder)
{
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services
            .AddAuthentication(DemoAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(
                DemoAuthenticationHandler.SchemeName,
                _ => { });
        return;
    }

    var authority = builder.Configuration["Authentication:OpenIdConnect:Authority"];
    var clientId = builder.Configuration["Authentication:OpenIdConnect:ClientId"];
    if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(clientId))
    {
        throw new InvalidOperationException(
            "OpenID Connect Authority and ClientId are required outside development.");
    }

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        })
        .AddOpenIdConnect(options =>
        {
            builder.Configuration.GetSection("Authentication:OpenIdConnect").Bind(options);
            options.ResponseType = "code";
            options.SaveTokens = false;
        });
}

public partial class Program;
