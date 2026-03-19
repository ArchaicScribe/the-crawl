using Microsoft.AspNetCore.Mvc;
using TheCrawl.Application.Commands;
using TheCrawl.Application.Services;

namespace TheCrawl.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController(GameService gameService) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartGameCommand command, CancellationToken ct)
    {
        var result = await gameService.StartGameAsync(command, ct);
        return Ok(result);
    }

    [HttpPost("move")]
    public async Task<IActionResult> Move([FromBody] MoveCommand command, CancellationToken ct)
    {
        var result = await gameService.MoveAsync(command, ct);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("session/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken ct)
    {
        var session = await gameService.GetSessionAsync(sessionId, ct);
        if (session is null) return NotFound();

        return Ok(new
        {
            session.Id,
            session.Status,
            Player = new
            {
                session.Player.Name,
                session.Player.Class,
                session.Player.CurrentHp,
                session.Player.MaxHp,
                session.Player.Level,
                session.Player.KillCount,
                session.Player.FloorsCleared,
                Stats = session.Player.BaseStats,
                Position = session.Player.Position,
            },
            Floor = new
            {
                session.CurrentFloor.FloorNumber,
                session.CurrentFloor.Zone,
                session.CurrentFloor.Width,
                session.CurrentFloor.Height,
                session.CurrentFloor.StairsPosition,
                EnemiesAlive = session.CurrentFloor.Enemies.Count(e => e.IsAlive),
                // Only expose enemies the player can currently see
                VisibleEnemies = session.CurrentFloor.Enemies
                    .Where(e => e.IsAlive && session.CurrentFloor.IsVisible(e.Position))
                    .Select(e => new { e.Name, e.FlavorTitle, e.CurrentHp, e.MaxHp, Position = e.Position }),
                VisibleTiles   = session.CurrentFloor.VisibleTiles,
                ExploredTiles  = session.CurrentFloor.ExploredTiles,
            },
            EventLog = session.EventLog.TakeLast(20),
        });
    }
}
