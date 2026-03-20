using Microsoft.EntityFrameworkCore;
using TheCrawl.Application.Interfaces;
using TheCrawl.Application.Services;
using TheCrawl.Domain.Interfaces;
using TheCrawl.Infrastructure.Generators;
using TheCrawl.Infrastructure.Persistence;
using TheCrawl.Infrastructure.Repositories;
using TheCrawl.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

builder.Services.AddScoped<IGameSessionRepository, GameSessionRepository>();
builder.Services.AddScoped<ISessionStore, RedisSessionStore>();
builder.Services.AddSingleton<IDungeonGenerator, DungeonGenerator>();
builder.Services.AddSingleton<IWeaponGenerator, WeaponGenerator>();
builder.Services.AddSingleton<IFovCalculator, FovCalculator>();
builder.Services.AddSingleton<IPathfinder, AStarPathfinder>();
builder.Services.AddHttpClient("Anthropic", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});

// VERA is used when Anthropic:ApiKey is configured; falls back to static lines if not.
builder.Services.AddSingleton<IAnnouncerService, VeraService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<CombatService>();
builder.Services.AddScoped<EnemyTurnService>();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.Run();
