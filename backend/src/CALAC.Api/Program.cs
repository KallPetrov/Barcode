using System.IdentityModel.Tokens.Jwt;
using System.Text;
using CALAC.Infrastructure.Data;
using CALAC.Infrastructure.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();

        metrics.ConfigureResource(resource => resource.AddService("CALAC.Api"));
    });

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddDataProtection();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CALAC API",
        Version = "v1",
        Description = "REST API за баркод PDA платформа"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (connectionString?.Contains("Host=localhost") == true || string.IsNullOrEmpty(connectionString))
    {
        options.UseSqlite("Data Source=calac.db");
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<InventorySessionService>();
builder.Services.AddScoped<PickingService>();
builder.Services.AddScoped<GoodsReceiptService>();
builder.Services.AddScoped<TransferService>();
builder.Services.AddScoped<ErpConfigurationService>();
builder.Services.AddScoped<WorkTaskService>();
builder.Services.AddScoped<OperatorPerformanceService>();
builder.Services.AddScoped<NotificationAlertService>();
builder.Services.AddScoped<KpiReportingService>();
builder.Services.AddScoped<ReminderService>();
builder.Services.AddScoped<OperatorActionHistoryService>();
builder.Services.AddScoped<SlaService>();
builder.Services.AddScoped<TenantOnboardingService>();
builder.Services.AddScoped<PartnerApiKeyService>();
builder.Services.AddScoped<ForecastingService>();
builder.Services.AddScoped<BatchPickingService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<TenantBrandingService>();
builder.Services.AddHttpClient<WebhookSubscriptionService>();
builder.Services.AddScoped<IZplService, ZplService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (!app.Environment.IsProduction())
    {
        await DbSeeder.SeedAsync(db);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapHub<CALAC.Api.Hubs.WarehouseHub>("/hub/warehouse");

app.Run();
public partial class Program { }
