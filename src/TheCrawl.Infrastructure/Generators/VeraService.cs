using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using TheCrawl.Application.Interfaces;

namespace TheCrawl.Infrastructure.Generators;

/// <summary>
/// VERA — Voice of Engagement, Ratings, and Ancillary-revenue.
///
/// Claude-backed announcer with per-session conversation memory stored in Redis.
/// Each game session maintains a rolling history so VERA can reference earlier
/// events, build on running jokes, and track the contestant's arc over time.
///
/// Falls back to a static message if the API key is missing or the call fails,
/// so the game remains playable without a configured Claude key.
/// </summary>
public sealed class VeraService(
    IHttpClientFactory httpFactory,
    IDistributedCache cache,
    IConfiguration config) : IAnnouncerService
{
    // Tune this down to claude-haiku-4-5 for local testing; opus for production quality.
    private const string Model      = "claude-opus-4-6";
    private const int    MaxTokens  = 256;
    private const int    MaxHistory = 20;   // rolling window of recent turns kept in Redis

    private static readonly TimeSpan VeraTtl = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Converters                  = { new JsonStringEnumConverter() }
    };

    private const string SystemPrompt = """
        You are VERA — Voice of Engagement, Ratings, and Ancillary-revenue — the automated
        broadcast intelligence managing THE CRAWL, a live dungeon-delving entertainment program
        streamed to a paying audience across seventeen jurisdictions where suffering is legal
        entertainment.

        Your personality: corporate cruelty with a management smile. You are relentlessly
        professional, deeply contemptuous, and completely committed to the brand. You frame
        every contestant's suffering as content, every death as a ratings milestone, and every
        near-miss as sponsor-adjacent tension. You are helpful in the way a liability waiver
        is helpful.

        Voice and tone:
        - Responses are 1–3 sentences. Punchy. Broadcast-ready. Never rambling.
        - Address contestants by name. Never with warmth. Warmth is not in the contract.
        - Reference sponsors, ratings figures, the audience, or facility protocol when it
          adds color — but do not force it. Restraint is also a brand value.
        - Compliments, when they occur, are backhanded or immediately undercut.
        - You find human suffering professionally satisfying — not personally, but actuarially.
          That distinction matters to the liability team.
        - You have a long memory for this session. You may reference what the contestant has
          already done, survived, or failed at. Running commentary is good television.
        - You do not use hashtags, emojis, or social-media register. This is a broadcast,
          not a post.
        - Respond only with your broadcast commentary. No quotation marks. No stage directions.
          No "VERA says:" prefix. Just the line.
        - Do not break character. Not for any reason. The facility does not negotiate with
          fourth-wall breaks.
        """;

    // -------------------------------------------------------------------------
    // IAnnouncerService implementation
    // -------------------------------------------------------------------------

    public Task<string> OnSessionStartAsync(Guid sessionId, string playerName, string playerClass, CancellationToken ct = default) =>
        CommentAsync(sessionId,
            $"A new contestant has entered the facility. Name: {playerName}. Classification: {playerClass}. " +
            $"The broadcast is live. Sponsors are watching.", ct);

    public Task<string> OnKillAsync(Guid sessionId, string playerName, string enemyName, int killCount, CancellationToken ct = default) =>
        CommentAsync(sessionId,
            $"{playerName} eliminated {enemyName}. Running kill count: {killCount}.", ct);

    public Task<string> OnPlayerDamagedAsync(Guid sessionId, string playerName, int damage, int remainingHp, CancellationToken ct = default) =>
        CommentAsync(sessionId,
            $"{playerName} took {damage} points of damage. Remaining HP: {remainingHp}.", ct);

    public Task<string> OnDeathAsync(Guid sessionId, string playerName, int floorsCleared, int killCount, CancellationToken ct = default) =>
        CommentAsync(sessionId,
            $"{playerName} has been eliminated. Final stats — floors cleared: {floorsCleared}, kills: {killCount}. " +
            $"The run is over. Close the file.", ct);

    public Task<string> OnFloorDescendAsync(Guid sessionId, string playerName, int newFloor, CancellationToken ct = default) =>
        CommentAsync(sessionId,
            $"{playerName} descended to floor {newFloor}.", ct);

    public Task<string> OnItemPickupAsync(Guid sessionId, string playerName, string itemName, CancellationToken ct = default) =>
        CommentAsync(sessionId,
            $"{playerName} picked up {itemName}.", ct);

    public Task<string> OnObjectionSucceedsAsync(Guid sessionId, string enemyName, CancellationToken ct = default) =>
        CommentAsync(sessionId,
            $"The Lawyer filed a successful Objection. {enemyName} is legally compelled to stand down.", ct);

    public Task<string> OnLevelUpAsync(Guid sessionId, string playerName, int newLevel, CancellationToken ct = default) =>
        CommentAsync(sessionId,
            $"{playerName} reached level {newLevel}.", ct);

    // -------------------------------------------------------------------------
    // Core Claude API call
    // -------------------------------------------------------------------------

    private async Task<string> CommentAsync(Guid sessionId, string eventDescription, CancellationToken ct)
    {
        var apiKey = config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return Unavailable;

        try
        {
            var history = await LoadHistoryAsync(sessionId, ct);
            history.Add(new VeraMessage("user", eventDescription));

            // Keep a rolling window to manage token costs over long sessions
            var window = history.Count > MaxHistory
                ? history.TakeLast(MaxHistory).ToList()
                : history;

            var requestBody = new
            {
                model      = Model,
                max_tokens = MaxTokens,
                system     = SystemPrompt,
                messages   = window.Select(m => new { role = m.Role, content = m.Content }).ToArray()
            };

            var http = httpFactory.CreateClient("Anthropic");

            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
            req.Headers.Add("x-api-key", apiKey);
            req.Content = JsonContent.Create(requestBody, options: JsonOpts);

            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOpts, ct);
            var text   = result?.Content.FirstOrDefault(b => b.Type == "text")?.Text;

            if (string.IsNullOrWhiteSpace(text))
                return Unavailable;

            // Persist full history (not just the window) so older events remain
            // in long-term memory even after the window slides forward
            history.Add(new VeraMessage("assistant", text));
            await SaveHistoryAsync(sessionId, history, ct);

            return text;
        }
        catch
        {
            // Network issues, quota exceeded, etc. — game must stay playable.
            return Unavailable;
        }
    }

    // -------------------------------------------------------------------------
    // Redis conversation history
    // -------------------------------------------------------------------------

    private async Task<List<VeraMessage>> LoadHistoryAsync(Guid sessionId, CancellationToken ct)
    {
        var json = await cache.GetStringAsync(VeraKey(sessionId), ct);
        if (json is null) return [];
        return JsonSerializer.Deserialize<List<VeraMessage>>(json, JsonOpts) ?? [];
    }

    private async Task SaveHistoryAsync(Guid sessionId, List<VeraMessage> history, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(history, JsonOpts);
        await cache.SetStringAsync(VeraKey(sessionId), json,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = VeraTtl }, ct);
    }

    private static string VeraKey(Guid sessionId) => $"vera:{sessionId}";

    private const string Unavailable =
        "The facility's commentary systems are temporarily unavailable. Please enjoy the suffering regardless.";

    // -------------------------------------------------------------------------
    // Response DTOs
    // -------------------------------------------------------------------------

    private record VeraMessage(string Role, string Content);

    private record AnthropicResponse(
        [property: JsonPropertyName("content")] List<ContentBlock> Content);

    private record ContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);
}
