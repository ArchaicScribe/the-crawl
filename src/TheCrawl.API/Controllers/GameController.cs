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
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("pickup")]
    public async Task<IActionResult> Pickup([FromBody] PickupCommand command, CancellationToken ct)
    {
        var result = await gameService.PickupAsync(command, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("equip")]
    public async Task<IActionResult> Equip([FromBody] EquipCommand command, CancellationToken ct)
    {
        var result = await gameService.EquipAsync(command, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("drop")]
    public async Task<IActionResult> Drop([FromBody] DropCommand command, CancellationToken ct)
    {
        var result = await gameService.DropAsync(command, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("use-item")]
    public async Task<IActionResult> UseItem([FromBody] UseItemCommand command, CancellationToken ct)
    {
        var result = await gameService.UseItemAsync(command, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("session/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken ct)
    {
        var session = await gameService.GetSessionAsync(sessionId, ct);
        if (session is null) return NotFound();

        var player = session.Player;
        var floor  = session.CurrentFloor;

        return Ok(new
        {
            session.Id,
            session.Status,
            Player = new
            {
                player.Name,
                player.Class,
                player.CurrentHp,
                player.MaxHp,
                player.Level,
                player.Xp,
                player.XpToNextLevel,
                player.KillCount,
                player.FloorsCleared,
                player.TotalRatings,
                player.BroadcastScore,
                Stats    = player.BaseStats,
                Position = player.Position,
                Equipment = new
                {
                    Weapon  = player.EquippedWeapon  is null ? null : new { player.EquippedWeapon.DisplayName,  player.EquippedWeapon.Rarity,  player.EquippedWeapon.Weight,  player.EquippedWeapon.CurrentDurability, player.EquippedWeapon.MaxDurability },
                    Offhand = player.EquippedOffhand is null ? null : new { player.EquippedOffhand.DisplayName, player.EquippedOffhand.Rarity, player.EquippedOffhand.Weight, player.EquippedOffhand.CurrentDurability, player.EquippedOffhand.MaxDurability },
                },
                Backpack = new
                {
                    Used     = player.BackpackUsed,
                    Capacity = player.BackpackCapacity,
                    Items = player.BackpackItems.Select(i => new
                    {
                        i.Id, i.Name, i.Description, i.Type, i.EffectValue
                    }),
                    Weapons = player.BackpackWeapons.Select(w => new
                    {
                        w.Id,
                        Name = w.DisplayName,
                        w.Rarity, w.Weight,
                        w.CurrentDurability, w.MaxDurability,
                        w.IsCursed
                    }),
                },
            },
            Floor = new
            {
                floor.FloorNumber,
                floor.Zone,
                floor.Width,
                floor.Height,
                floor.StairsPosition,
                EnemiesAlive = floor.Enemies.Count(e => e.IsAlive),
                VisibleEnemies = floor.Enemies
                    .Where(e => e.IsAlive && floor.IsVisible(e.Position))
                    .Select(e => new { e.Name, e.FlavorTitle, e.CurrentHp, e.MaxHp, Position = e.Position }),
                // Only show weapons the player can see — no map hacking
                VisibleWeapons = floor.Weapons
                    .Where(w => w.Position is not null && floor.IsVisible(w.Position!))
                    .Select(w => new { w.Id, Name = w.DisplayName, w.Rarity, Position = w.Position }),
                VisibleItems = floor.Items
                    .Where(i => floor.IsVisible(i.Position))
                    .Select(i => new { i.Id, i.Name, i.Type, Position = i.Position }),
                VisibleTiles  = floor.VisibleTiles,
                ExploredTiles = floor.ExploredTiles,
            },
            EventLog = session.EventLog.TakeLast(20),
        });
    }
}
