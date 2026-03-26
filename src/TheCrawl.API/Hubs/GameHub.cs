using Microsoft.AspNetCore.SignalR;
using TheCrawl.Application.Commands;
using TheCrawl.Application.Services;
using TheCrawl.Domain.Interfaces;

namespace TheCrawl.API.Hubs;

public class GameHub(GameService gameService, CombatService combatService, EnemyTurnService enemyTurns, ISessionStore sessionStore) : Hub
{
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Caller.SendAsync("Joined", sessionId);
    }

    public async Task SendMove(string sessionId, string direction)
    {
        if (!Guid.TryParse(sessionId, out var id)) return;
        if (!Enum.TryParse<Direction>(direction, true, out var dir)) return;

        var result = await gameService.MoveAsync(new MoveCommand(id, dir));
        await Clients.Group(sessionId).SendAsync("GameUpdate", result);

        if (result.AnnouncerMessage is not null)
            await Clients.Group(sessionId).SendAsync("Announcement", result.AnnouncerMessage);
    }

    public async Task SendAttack(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var id)) return;

        var session = await gameService.GetSessionAsync(id);
        if (session is null) return;

        var result = await combatService.ResolvePlayerAttackAsync(session);

        // Run enemy turns after every attack — same as after movement
        var enemyResult = await enemyTurns.ProcessTurnsAsync(session);
        foreach (var ev in enemyResult.Events)
            session.LogEvent(ev);

        await sessionStore.SaveAsync(session);

        await Clients.Group(sessionId).SendAsync("CombatResult", result);

        var announcerMsg = enemyResult.AnnouncerMessage ?? result.AnnouncerMessage;
        if (announcerMsg is not null)
            await Clients.Group(sessionId).SendAsync("Announcement", announcerMsg);
    }
}
