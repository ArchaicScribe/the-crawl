namespace TheCrawl.Application.Commands;

public record UseAbilityCommand(Guid SessionId);

public record UseAbilityResult(bool Success, string Message, string? AnnouncerMessage = null);
