using TheCrawl.Domain.Enums;

namespace TheCrawl.Application.Commands;

public record StartGameCommand(string PlayerName, PlayerClass PlayerClass);
public record StartGameResult(Guid SessionId, string AnnouncerMessage);
