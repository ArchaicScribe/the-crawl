namespace TheCrawl.Application.Commands;

public enum Direction { North, South, East, West, NorthEast, NorthWest, SouthEast, SouthWest }

public record MoveCommand(Guid SessionId, Direction Direction);
public record MoveResult(bool Success, string Message, string? AnnouncerMessage = null);
