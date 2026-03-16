namespace TheCrawl.Application.Commands;

public enum Direction { North, South, East, West }

public record MoveCommand(Guid SessionId, Direction Direction);
public record MoveResult(bool Success, string Message, string? AnnouncerMessage = null);
