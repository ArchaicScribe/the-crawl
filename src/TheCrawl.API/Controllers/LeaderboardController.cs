using Microsoft.AspNetCore.Mvc;
using TheCrawl.Domain.Interfaces;

namespace TheCrawl.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController(IGameSessionRepository sessionRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int top = 10, CancellationToken ct = default)
    {
        var sessions = await sessionRepository.GetLeaderboardAsync(top, ct);
        var entries = sessions.Select(s => new
        {
            PlayerName = s.Player.Name,
            PlayerClass = s.Player.Class.ToString(),
            FloorsCleared = s.Player.FloorsCleared,
            Kills = s.Player.KillCount,
            BroadcastScore = s.Player.BroadcastScore,
            Outcome = s.Status.ToString(),
            EndedAt = s.EndedAt,
        });
        return Ok(entries);
    }
}
