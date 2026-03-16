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

builder.Services.AddScoped<IGameSessionRepository, GameSessionRepository>();
builder.Services.AddSingleton<IDungeonGenerator, DungeonGenerator>();
builder.Services.AddSingleton<IAnnouncerService, AnnouncerService>();
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<CombatService>();

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
