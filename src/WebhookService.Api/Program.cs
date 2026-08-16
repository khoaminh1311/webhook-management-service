using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Instrumentation;
using WebhookService.Core.Services;
using WebhookService.Core.Settings;
using WebhookService.Infrastructure.Data;
using WebhookService.Infrastructure.Repositories;
using WebhookService.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Webhook Management Service",
        Version = "v1",
        Description = "A webhook management and delivery service with retry handling, HMAC security, and structured observability."
    });
});

// Configuration
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<QueueSettings>(builder.Configuration.GetSection("QueueSettings"));
builder.Services.Configure<RetrySettings>(builder.Configuration.GetSection("RetrySettings"));
builder.Services.Configure<WebhookSecuritySettings>(builder.Configuration.GetSection("WebhookSecurity"));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
var key = Encoding.ASCII.GetBytes(jwtSettings.SecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// EF Core — SQL Server
builder.Services.AddDbContext<WebhookServiceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IWebhookRepository, WebhookRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IDeliveryAttemptRepository, DeliveryAttemptRepository>();

// Http Client
builder.Services.AddHttpClient("WebhookClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Queues & Background Workers
builder.Services.AddSingleton<IWebhookDeliveryQueue, WebhookService.Infrastructure.Queues.WebhookDeliveryQueue>();
builder.Services.AddHostedService<WebhookService.Infrastructure.Workers.WebhookDeliveryWorker>();

// Services
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddSingleton<IDelayService, DelayService>();
builder.Services.AddSingleton<IRetryPolicyService, WebhookRetryPolicyService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IWebhookSignatureService, WebhookSignatureService>();
builder.Services.AddSingleton<WebhookMetrics>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWebhookService, WebhookService.Core.Services.WebhookService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IWebhookDeliveryService, WebhookDeliveryService>();

// Health checks (includes database connectivity check)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WebhookServiceDbContext>("database");

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health endpoint
app.MapHealthChecks("/health");

app.Run();

// Make Program accessible for integration tests via WebApplicationFactory
public partial class Program { }
