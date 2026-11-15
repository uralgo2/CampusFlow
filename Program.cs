using System.Reflection;
using System.Text.Json;
using CampusFlow.Api.Authentication;
using CampusFlow.Api.Http;
using CampusFlow.Api.Middleware;
using CampusFlow.Api.Serialization;
using CampusFlow.Application.Abstractions;
using CampusFlow.Application.Errors;
using CampusFlow.Application.Services;
using CampusFlow.Contracts.Common;
using CampusFlow.Infrastructure.Persistence;
using CampusFlow.Infrastructure.Persistence.Repositories;
using CampusFlow.Infrastructure.Security;
using CampusFlow.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var configuredConnection = builder.Configuration.GetConnectionString("CampusFlow") ?? "Data Source=campusflow.db";
var connectionBuilder = new SqliteConnectionStringBuilder(configuredConnection);
if (!string.IsNullOrWhiteSpace(connectionBuilder.DataSource)
    && !string.Equals(connectionBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
    && !Path.IsPathRooted(connectionBuilder.DataSource))
{
    connectionBuilder.DataSource = Path.Combine(builder.Environment.ContentRootPath, connectionBuilder.DataSource);
}

builder.Services.AddDbContext<CampusFlowDbContext>(options => options.UseSqlite(connectionBuilder.ToString()));

builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddSingleton<IClock, LocalClock>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IStatsService, StatsService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => JsonDefaults.Configure(options.JsonSerializerOptions));

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = new Dictionary<string, string>();

        foreach (var entry in context.ModelState)
        {
            var message = entry.Value.Errors
                              .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                                  ? error.Exception?.Message
                                  : error.ErrorMessage)
                              .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                          ?? "Некорректное значение";

            var field = string.IsNullOrWhiteSpace(entry.Key) || entry.Key == "$"
                ? "body"
                : JsonNamingPolicy.CamelCase.ConvertName(entry.Key);

            details[field] = message;
        }

        return new BadRequestObjectResult(
            new ErrorResponse(ErrorCodes.Validation, "Некорректные данные запроса", details));
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CampusFlow API",
        Version = "v1",
        Description =
            "MVP сервиса бронирования аудиторий: справочник аудиторий, свободные слоты, брони, админ-управление и статистика загрузки."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "JWT-токен, полученный в POST /api/auth/login"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);

var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < JwtOptions.MinimumKeyLength)
{
    throw new InvalidOperationException(
        $"Jwt:Key должен быть задан и содержать минимум {JwtOptions.MinimumKeyLength} символов. " +
        "Измените appsettings.json или задайте переменную окружения Jwt__Key.");
}
builder.Services
    .AddAuthentication(JwtAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, JwtAuthenticationHandler>(
        JwtAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseStatusCodePages(async statusCodeContext =>
{
    var context = statusCodeContext.HttpContext;
    var error = context.Response.StatusCode switch
    {
        StatusCodes.Status404NotFound => new ErrorResponse(ErrorCodes.NotFound, "Ресурс не найден"),
        StatusCodes.Status405MethodNotAllowed => new ErrorResponse(
            ErrorCodes.MethodNotAllowed,
            "Метод не поддерживается для этого ресурса"),
        _ => null
    };

    if (error is not null)
    {
        await HttpErrorWriter.WriteAsync(context, context.Response.StatusCode, error);
    }
});

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CampusFlow API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "CampusFlow API";
});


app.MapGet("/", (req) =>
{
    req.Response.Redirect("/index.html");
    return Task.CompletedTask;
});
app.MapControllers();
await DbInitializer.InitializeAsync(app.Services, app.Logger);

app.Run();

