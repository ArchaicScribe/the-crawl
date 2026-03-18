# System Design & Architecture

## Overview

The Crawl is a server-authoritative roguelike engine exposed as a modern .NET API. All game logic — dungeon generation, combat resolution, turn processing, AI narration — runs on the server. Clients are thin consumers that send commands and render state. This architecture supports multiple client implementations (React web, Unity, terminal) against the same backend without modification.

```
┌─────────────────────────────────────────────────────────────────┐
│                         Clients                                 │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │  React Web   │    │    Unity     │    │   Terminal   │      │
│  │  (current)   │    │  (future)    │    │  (future)    │      │
│  └──────┬───────┘    └──────┬───────┘    └──────┬───────┘      │
└─────────┼────────────────── ┼ ──────────────────┼──────────────┘
          │ REST              │ SignalR            │
          ▼                   ▼                    ▼
┌─────────────────────────────────────────────────────────────────┐
│                       TheCrawl.API                              │
│  ┌─────────────────────┐    ┌─────────────────────────────┐    │
│  │   GameController    │    │          GameHub             │    │
│  │  LeaderboardCtrl    │    │      (SignalR real-time)     │    │
│  └──────────┬──────────┘    └──────────────┬──────────────┘    │
└─────────────┼────────────────────────────── ┼ ─────────────────┘
              │                               │
              ▼                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                     TheCrawl.Application                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │  GameService │  │ CombatService│  │    IAnnouncerService  │  │
│  │  (sessions,  │  │  (combat,    │  │    (VERA — Claude     │  │
│  │   movement)  │  │   turns)     │  │     API narrator)     │  │
│  └──────┬───────┘  └──────┬───────┘  └──────────────────────┘  │
└─────────┼─────────────────┼────────────────────────────────────┘
          │                 │
          ▼                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                      TheCrawl.Domain                            │
│  Entities: Player, Enemy, Floor, Room, Item, GameSession        │
│  ValueObjects: Stats, Position                                  │
│  Enums: PlayerClass, GameStatus, TileType, ZoneType, Direction  │
│  Interfaces: IDungeonGenerator, IGameSessionRepository          │
└─────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────┐
│                   TheCrawl.Infrastructure                       │
│  ┌──────────────────┐  ┌────────────────┐  ┌────────────────┐  │
│  │  DungeonGenerator│  │ GameDbContext   │  │  RedisSession  │  │
│  │  (proc gen, A*)  │  │ (EF Core +     │  │  Store         │  │
│  │  AnnouncerService│  │  PostgreSQL)    │  │  (StackExchange│  │
│  │  (VERA/Claude)   │  │                │  │   .Redis)      │  │
│  └──────────────────┘  └────────────────┘  └────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Clean Architecture Layers

### TheCrawl.Domain
The core of the system. Contains all game entities, value objects, enums, and interfaces. Has **zero dependencies** on any other project or external package. If the game rules live here, they can be tested without a database, web framework, or network.

| Component | Purpose |
|---|---|
| `Player` | Contestant entity. Owns stats, position, HP, kill count, floor count. Calculates `BroadcastScore`. |
| `Enemy` | Dungeon inhabitant. Owns HP, damage, dodge chance, RATINGS value on kill, position. |
| `Floor` | One level of the dungeon. Owns tile grid, room list, enemy list, item list, stairs position. |
| `Room` | A carved room within a floor. Knows its bounds and center point. |
| `Item` | A pickup on the floor. Typed (consumable, weapon, armor) with an effect value. |
| `GameSession` | The running game. Ties a Player to a Floor, tracks status and event log. |
| `Stats` | Immutable record: `(Muscle, Nerve, Grit, Wit, Ratings)`. Derives `MaxHp` from Grit. |
| `Position` | Immutable record: `(X, Y)`. Provides distance calculation. |
| `ClassDefinitions` | Static map of `PlayerClass` → base `Stats` and flavor quote. |

### TheCrawl.Application
The game brain. Orchestrates domain entities using injected infrastructure interfaces. Contains all use-case logic. Depends only on Domain.

| Component | Purpose |
|---|---|
| `GameService` | Creates sessions, processes player movement, handles floor transitions, manages session lifecycle. |
| `CombatService` | Resolves player attacks, enemy counter-attacks, death, kill registration. |
| `IAnnouncerService` | Interface for VERA. Application layer calls this; infrastructure provides the Claude API implementation. |
| `Commands` | DTOs carrying player intent: `StartGameCommand`, `MoveCommand`, `AttackCommand`. |

### TheCrawl.Infrastructure
The plumbing. Implements interfaces defined in Domain and Application. Knows about databases, external APIs, and algorithms. Nothing upstream knows these implementations exist.

| Component | Purpose |
|---|---|
| `DungeonGenerator` | Room-and-corridor procedural generation. Enemy and item spawning by zone. A* pathfinding for enemy AI. |
| `AnnouncerService` | VERA implementation. Manages Claude API conversation threads scoped per session. |
| `GameDbContext` | EF Core context. Maps `Players` and `GameSessions` to PostgreSQL. |
| `GameSessionRepository` | Saves completed sessions to PostgreSQL. Queries leaderboard by `BroadcastScore`. |
| `RedisSessionStore` | Stores active `GameSession` snapshots in Redis with 24-hour TTL. Replaces in-memory dictionary. |

### TheCrawl.API
The surface layer. Maps HTTP and WebSocket requests to application services. Handles DI registration, middleware, and serialization. Contains no game logic.

| Component | Purpose |
|---|---|
| `GameController` | REST endpoints: start session, move, pick up item, get session state. |
| `LeaderboardController` | REST: top runs by BroadcastScore. |
| `GameHub` | SignalR hub: join session group, send move, send attack. Pushes `GameUpdate`, `CombatResult`, `Announcement` to clients. |
| `Program.cs` | DI wiring, middleware pipeline, CORS, Swagger. |

---

## Data Flow

### Starting a game
```
POST /api/game/start { playerName, playerClass }
  → GameController → GameService.StartGameAsync()
  → ClassDefinitions.GetBaseStats(playerClass)        // stat lookup
  → DungeonGenerator.GenerateFloor(1, SurfaceFringe)  // proc gen
  → new Player + new GameSession
  → RedisSessionStore.SaveAsync(session)               // active state
  → PostgreSQL: INSERT Players                         // persistent record
  → VERA.OnSessionStart()                              // AI narration
  ← { sessionId, announcerMessage }
```

### Player move (via SignalR)
```
Client → GameHub.SendMove(sessionId, direction)
  → RedisSessionStore.GetAsync(sessionId)     // load live session
  → GameService.MoveAsync()
      → walkability check
      → enemy blocking check
      → player.MoveTo()
      → enemy turns (A* pathfinding, chase if in FOV)
      → floor transition if on stairs
  → RedisSessionStore.SaveAsync(session)      // update live state
  → VERA contextual commentary if notable event
  ← Clients.Group(sessionId).SendAsync("GameUpdate", state)
  ← Clients.Group(sessionId).SendAsync("Announcement", vera)
```

### Player death
```
CombatService.ResolvePlayerAttack()
  → player HP reaches 0
  → session.EndSession(Dead)
  → RedisSessionStore.RemoveAsync(sessionId)     // evict active state
  → PostgreSQL: UPDATE GameSessions (status, endedAt)  // permanent record
  → VERA.OnDeath()                               // final broadcast
  ← CombatResult { playerDied: true, finalScore, ... }
```

---

## Database Design

### PostgreSQL — Persistent Data

Stores completed and in-progress session metadata. Source of truth for the leaderboard.

```
Players
  id              uuid PK
  name            text
  class           text          -- "Lawyer", "Exterminator", etc.
  stats_muscle    int
  stats_nerve     int
  stats_grit      int
  stats_wit       int
  stats_ratings   int
  current_hp      int
  level           int
  kill_count      int
  floors_cleared  int
  position_x      int
  position_y      int

GameSessions
  id              uuid PK
  player_id       uuid FK → Players.id
  status          text          -- "Active", "Dead", "Escaped"
  started_at      timestamptz
  ended_at        timestamptz NULL
```

**Why not persist Floor state to PostgreSQL?**
Floors are procedurally generated and fully reconstructable. Persisting a 60×40 tile grid plus enemy and item positions to a relational database adds schema complexity with no benefit — the session is either active (in Redis) or over (only metadata matters for the leaderboard).

### Redis — Active Session State

Stores the full in-flight game session as a JSON snapshot. Keyed by session ID.

```
Key:   session:{sessionId}
Value: JSON snapshot of GameSession (player, floor, enemies, items, event log)
TTL:   24 hours — abandoned runs expire automatically (permadeath by inactivity)
```

**Why Redis over an in-memory dictionary?**

| Concern | Static Dictionary | Redis |
|---|---|---|
| Server restart | All sessions lost | Sessions survive |
| Multiple API instances | Sessions don't share | Shared session store |
| Abandoned runs | Accumulate forever | TTL evicts automatically |
| Portfolio signal | Junior pattern | Production-grade caching |

---

## AI Systems

### VERA — Narrative AI

VERA (Verified Entertainment Relay Automaton) is the dungeon's broadcast AI. She is implemented as a Claude API integration with a session-scoped conversation thread — she remembers your run.

```
Session start
  → Create Claude conversation with system prompt:
      - VERA's personality (corporate, polished, entertained by suffering)
      - Player name, class, flavor quote
      - Broadcast framing (this is live television)

Game event occurs (kill, damage, floor descent, near-death, item pickup)
  → Structured event context sent to Claude:
      { event: "kill", enemy: "Middle Manager - Fully Autonomous",
        killCount: 7, floor: 9, playerHp: 45, maxHp: 80 }
  → Claude responds in-character as VERA
  → Response streamed via SignalR to client
  → Appended to session conversation (VERA remembers)

Session ends (death or escape)
  → VERA delivers final broadcast message
  → Conversation thread closed and archived
```

**Why a conversation thread and not one-shot calls?**
One-shot calls produce generic commentary. A conversation thread lets VERA reference earlier events — "You've been this low before. Floor 4, remember? You didn't make it then either." That's what makes it feel like a character rather than a text generator.

### Game AI — Enemy Behavior

Enemies are not static. After every player action, all alive enemies on the current floor take a turn.

```
Behavior types:
  Chase    — moves toward player via A* if player is in FOV
  Patrol   — moves between fixed patrol points, switches to Chase if player enters FOV
  Wander   — moves to a random adjacent walkable tile each turn

FOV check (shadowcasting algorithm):
  → Compute all tiles visible from player position
  → Enemy is "aware" if its position is in the visible set
  → Aware enemies switch to Chase behavior

A* pathfinding:
  → Grid-based, uses tile walkability
  → Enemy takes one step per turn toward next node on path
  → Recalculates path each turn (player moves)
```

---

## API Contract

### REST Endpoints

```
POST   /api/game/start              Start a new session
POST   /api/game/move               Move player (cardinal direction)
POST   /api/game/attack             Attack nearest enemy
POST   /api/game/pickup             Pick up item at current position
GET    /api/game/session/{id}       Full session state snapshot
GET    /api/leaderboard             Top 20 runs by BroadcastScore
```

### SignalR Hub — /hubs/game

**Client → Server:**
```
JoinSession(sessionId)              Subscribe to session event group
SendMove(sessionId, direction)      Move: "North" | "South" | "East" | "West"
SendAttack(sessionId)               Attack nearest enemy
```

**Server → Client (pushed events):**
```
Joined(sessionId)                   Confirms group subscription
GameUpdate(MoveResult)              State after movement
CombatResult(AttackResult)          State after combat
Announcement(string)                VERA narration line
```

**Why split REST and SignalR?**
REST handles stateless commands where a synchronous response is expected (start, state query). SignalR handles events that need to push updates to all session participants simultaneously — enemy turns, VERA commentary, and future spectator support all benefit from the group broadcast model.

---

## Technology Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 | Current release; signals active platform awareness |
| Architecture | Clean Architecture | Enforces dependency direction; each layer independently testable |
| Real-time | SignalR | .NET-native; negotiates WebSocket/SSE/polling automatically |
| ORM | EF Core + Npgsql | Standard .NET data access; owned entities model Stats/Position cleanly |
| Cache | Redis + StackExchange.Redis | Industry-standard; TTL fits permadeath model naturally |
| AI | Claude API (Anthropic SDK) | Session-scoped conversation threading for persistent character memory |
| Testing | xUnit + FluentAssertions | Standard .NET test stack; domain logic is pure and highly testable |
| Frontend | React 19 + TypeScript + Vite 8 | Current toolchain; consistent with portfolio |
| Containerization | Docker + docker-compose | Single command local setup; parity with deployment environment |

---

## Branch & Release Strategy

```
main        ← production. PR from staging only.
staging     ← pre-production. Manual promotion from qa.
qa          ← QA environment. Auto-promoted from develop on every merge.
develop     ← integration. All feature PRs land here.
feature/*   ← short-lived feature work.
```

**Branch prefix conventions:**

| Prefix | Purpose |
|---|---|
| `feature/` | New functionality |
| `fix/` | Bug fixes on develop |
| `hotfix/` | Urgent fix from staging or main, back-merged to develop |
| `chore/` | Dependencies, config, tooling |
| `docs/` | Documentation only |
| `test/` | Test additions |
| `refactor/` | Restructuring, no behavior change |
| `ci/` | Pipeline changes |
