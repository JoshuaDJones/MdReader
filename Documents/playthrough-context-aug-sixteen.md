# Playthrough Context - Updated August 23, 2026

## Purpose

This file is a handoff for continuing the playthrough implementation across `Eldoria.Core`, `Eldoria.Application`, `Eldoria.Infrastructure`, `Eldoria.Api`, and `Lunoria.Web`.

Snapshot creation, the authenticated playthrough and scene pages, the turn/action flow, and an anonymous SignalR guest view are implemented. Attack, Open Chest, Use Potion, Trade Item, Forfeit Action, live chest creation, and scene completion are implemented end to end.

## Important user decisions

- A playthrough is an immutable snapshot of a journey at the time play starts.
- Later changes to live journeys, scenes, characters, spells, spell types, or items must not alter an active playthrough.
- Starting a playthrough marks every unfinished playthrough for the same user and journey as completed.
- Completing old playthroughs and inserting the replacement snapshot must happen atomically.
- The client does not supply snapshot data, timestamps, completion state, or generated playthrough metadata. The server derives these values.
- Source IDs on snapshot entities are for traceability and relationship mapping only. Runtime gameplay must never load mutable live entities through a source ID.
- Images are shared by URL and filename. Images must never be deleted from Azure storage; `AzureStorageBlob.DeletePhotoFromUrl` is intentionally a successful no-op.
- Scene character snapshots can have multiple runtime instances of the same `PlaythroughCharacter`.
- A journey playthrough character has a one-to-one relationship with its `PlaythroughCharacter`.
- A scene begins at round 1 with the first active journey character as `CurrentParticipant`.
- Turn order is active journey characters, then NPCs, then enemies.
- Every current participant, including NPCs and enemies, uses the Begin Turn, movement, and action flow.
- A participant remains current until an action completes or the action is forfeited.
- Players can Attack, Open Chest, Use Potion, Trade Item, or Forfeit Action when each action is eligible.
- NPCs and enemies cannot Open Chest or Trade Item. Open Chest is hidden when the scene has no unopened chest, and Use Potion is hidden when the current participant has no unused consumable.
- Equipment is applied automatically while carried. There is no manual equipment-slot UI. Multiple copies can be stored and traded, but each distinct equippable item definition contributes its effects at most once.
- Players and NPCs can attack enemies. Enemies can attack players and NPCs.
- Defeated scene NPCs and enemies are marked dead and removed from the participant list.
- A player reduced to zero HP is downed but remains a participant. They recover with 1 HP after five of their own scheduled turns.
- A player who defeats an enemy always receives a reward: an equal random chance of up to 4 HP or up to 4 MP, capped by effective equipment-adjusted limits.
- A chest can be opened only once. If the awarded quantity will not fit, the chest remains unopened and the player forfeits the action.
- Anonymous phone guests are read-only.
- Do not run builds, tests, linters, migrations, or browser automation unless the user asks or the requested implementation requires verification.

## Snapshot creation API

Routes in `Eldoria.Api/Controllers/PlaythroughController.cs`:

```http
GET  /api/v1/journeys/{journeyId}/playthroughs
POST /api/v1/journeys/{journeyId}/playthroughs
GET  /api/v1/playthroughs/{playthroughId}
```

- Journey playthrough listing returns `PlaythroughSummaryDto` records.
- Starting a playthrough returns `201 Created` with `PlaythroughCreatedDto` containing only `id`.
- The client navigates with that ID and loads the detail endpoint separately.
- Invalid snapshot relationships return `400 Bad Request` with `Playthrough.InvalidSourceGraph`.

`IPlaythroughService` currently exposes:

```csharp
Task<Result<PlaythroughCreatedDto>> StartAsync(
    int userId,
    int journeyId,
    CancellationToken ct);

Task<Result<PlaythroughDetailsDto>> GetAsync(
    int userId,
    int playthroughId,
    CancellationToken ct);

Task<Result<List<PlaythroughSummaryDto>>> GetForJourneyAsync(
    int userId,
    int journeyId,
    CancellationToken ct);
```

## StartAsync behavior

`PlaythroughService.StartAsync`:

1. Begins a serializable database transaction.
2. Loads the source journey and verifies ownership.
3. Loads every tracked unfinished playthrough for the same user and journey.
4. Uses one server-generated UTC timestamp to complete old playthroughs and start the replacement.
5. Loads the complete source journey graph with ownership, `IgnoreQueryFilters()`, identity resolution, and split-query behavior.
6. Loads base snapshot assets with `PlaythroughRepository.GetStartAssetsAsync`.
7. Includes active base characters and soft-deleted characters still referenced by the journey, including alternate-form closure.
8. Validates source relationships and maps the snapshot in dependency order.
9. Adds the new root, saves once, commits, and returns its ID.

Creation adds this initial log inside the same transaction:

```text
Message: Playthrough Generated
EventTime: the same UTC timestamp used for StartedAt
```

## Snapshot coverage

### Base playthrough entities

- Spell types.
- Spells linked to snapshot spell types.
- Characters, images, dialog colors, base stats, and alternate forms.
- Character spells linked to snapshot spells.
- Consumable items.
- Equippable items.
- Equippable affected spell types linked to snapshot spell types.
- Equippable added spells linked to snapshot spells.

### Journey playthrough entities

- Journey intro pages.
- Journey characters with initial and current state.
- Journey-character alternate forms.
- Journey-character spells linked to snapshot spells.
- Runtime consumable and equippable inventories.

### Scene playthrough entities

- Scenes, grids, and intro pages.
- Scene characters and their spells.
- Chests and loot entries linked to snapshot items.
- Dialogs, pages, and sections linked to snapshot characters.
- Events and event actions.
- Stat-adjustment and add-spell actions linked to snapshot entities.
- Runtime scene participants, character instances, inventories, and event state.

Runtime collections such as inventories, participants, and event logs start empty unless their initialization is part of playthrough or scene startup. They represent gameplay state rather than mutable source content.

## Snapshot isolation safeguards

- Gameplay entities do not have navigations or foreign keys back to live journey content.
- `Source...Id` values are scalar traceability data only.
- Snapshot relationships target other snapshot entities.
- Soft-deleted referenced source content can still be copied.
- The serializable transaction covers completion of previous playthroughs and insertion of the replacement.
- Runtime services query the playthrough graph itself.
- Blob URLs are safe to share because uploads use generated names and deletion is disabled.

## Authenticated Playthrough page

Route:

```text
/series/:seriesId/journeys/:journeyId/playthroughs/:playthroughId
```

Relevant file: `Lunoria.Web/src/pages/authenticated/PlaythroughPage.tsx`

Current behavior:

- Loads `GET /api/v1/playthroughs/{playthroughId}`.
- Shows scene cards and the playthrough event log.
- Not-started scenes have Start buttons.
- In-progress scenes have Resume buttons.
- The header has Play Intro when intro pages exist.
- A newly created playthrough automatically opens the journey intro-page viewer through navigation state.
- The header also has a Join button for anonymous phone guests.

The Play Hub starts or resumes a playthrough and navigates here using the playthrough ID.

## Scene playthrough API

Base route:

```text
/api/v1/playthroughs/{playthroughId}/scenes/{sceneId}
```

Current endpoints:

```http
GET  /
POST /start
POST /participants/scene-characters/{scenePlaythroughCharacterId}
POST /participants/journey-characters/{journeyPlaythroughCharacterId}/activate
POST /participants/playthrough-characters/{playthroughCharacterId}
POST /chests
PUT  /participants/{participantId}/stats
POST /participants/{participantId}/movement
POST /participants/{participantId}/forfeit-action
POST /participants/{participantId}/attack
POST /participants/{participantId}/chests/{chestId}/open
POST /participants/{participantId}/consumables/{inventoryItemId}/use
POST /participants/{participantId}/trade
POST /end
```

The detail response contains scene identity and state, participants, activation/add-character options, chests, dialogs, and playthrough event logs.

## Scene startup and participants

Starting a scene:

1. Adds active journey characters as Player participants.
2. Adds initially active scene NPC instances.
3. Adds initially active scene enemy instances.
4. Sets round 1.
5. Sets the first active journey participant as current.
6. Adds `Scene Started: {scene name}` to the event log.

Participant ordering is `ParticipantType`, then `SortOrderWithinType`, then ID. This yields Players, NPCs, and Enemies.

Scene character instances are intentionally duplicate-capable. Migration `20260822170000_AllowDuplicateScenePlaythroughCharacterInstances` made `SourceSceneCharacterId` nullable and removed uniqueness from the scene/source-character index.

## Scene Playthrough page

Route:

```text
/series/:seriesId/journeys/:journeyId/playthroughs/:playthroughId/scenes/:sceneId
```

Relevant file: `Lunoria.Web/src/pages/authenticated/ScenePlaythroughPage.tsx`

Layout and behavior:

- Scene name appears in the header.
- Round number and the options hamburger are on the right.
- Participants render in cards, up to four across at the largest breakpoint.
- The event log is in the right-side column.
- Participant images use `object-contain` and render on the left.
- Name, description, HP, MP, and movement render to the right.
- The current participant has the orange utility border.
- The current participant card is slightly blurred behind the animated orange turn button.
- NPCs and enemies receive the same Begin Turn prompt as journey characters.
- `Ctrl+O` or `Cmd+O` opens the options drawer.

Options drawer functionality:

- Activate an inactive journey character.
- Add a new scene character instance from a playthrough NPC or enemy.
- Adjust participant stats.
- Add a runtime chest from the scene's snapshotted chest definitions.
- Open the playthrough dialog-page viewer.
- End the scene and navigate back to the journey playthrough page.

The earlier per-participant `Add another` button was removed. New instances are added through the options drawer.

## Current turn flow

The current participant turn works as follows:

1. Click Begin Turn.
2. The Movement Roll dialog opens.
3. Choose one face of a six-sided die.
4. Total movement is the participant's current movement plus the chosen face.
5. Continue records `{character name} moved {number} spaces` and displays a toast.
6. The Turn Action dialog opens.
7. The participant remains current while choosing or completing an action.

The movement endpoint accepts any active current participant. It is no longer restricted to `ParticipantType.Player`.

Turn Action uses square image buttons with the action name overlaid in the top-left. It currently offers eligible participants:

- Attack.
- Open Chest.
- Use Potion.
- Trade Item.
- Forfeit Action.

Attack opens an image-button attack-type dialog for Melee Attack, Range Attack, or Spell Attack. The resolution dialog requires an eligible target and die face, shows the calculated damage at the top, and requires a spell for spell attacks. Equipment modifiers and target damage reduction are included. Spell attacks spend MP. A successful attack advances the turn.

After a successful attack, the target image is displayed in a full-page overlay while two red diagonal slashes animate from the top corners to form an X. `public/sounds/sword_slash.wav` plays with the approximately two-second presentation. The overlay sits above the attack dialogs.

Open Chest is available only to players and only when an unopened chest exists. The chest dialog displays `Opening_Chest.png` and loops `sounds/Treasure_Chest_Magical_Glittering_Gold.wav` until the user selects a die face and the result view is ready. The die maps to the chest's snapshotted loot entry. The awarded item fades in centered above the dice, then remains visible with its image on the left and statistics on the right until Continue is clicked. Duplicate inventory items are allowed. If the complete quantity will not fit, no item is awarded, the chest stays unopened, and the action is forfeited.

Use Potion is available whenever the current participant has an unused consumable. The picker groups duplicate potions, displays item effects and statistics, consumes exactly one inventory copy, applies HP and MP effects without exceeding equipment-adjusted maxima, logs the use, and advances the turn. Journey and scene participants are supported.

Trade Item is available only between active player participants. The user chooses another player and sees both players' consumable and equippable inventories. Selecting an item and its destination moves it if capacity permits. A successful trade advances the turn.

Equippable effects are automatic while the item is in the equipment inventory. Effects include attack, defense, movement, maximum HP/MP, inventory capacity, spell damage, and added spells. Multiple copies of the same equippable item can exist, but effects are calculated once per distinct item ID.

Forfeiting adds `{character name} forfeited their action`, advances to the next active participant, increments the round on wrap, refreshes the scene, and dismisses the dialogs.

If the Turn Action dialog is dismissed without executing or forfeiting, the current card displays Select Action. Reopening it does not repeat movement.

Important limitation: movement-complete/action-pending is still React page state (`begunTurnKey` and `awaitingActionTurnKey`). It is not persisted. Refreshing during a turn can return the card to Begin Turn and allow movement to be recorded again. A persisted turn-phase field remains important before public launch.

## Anonymous guest playthrough

The Playthrough page Join button creates a secure anonymous viewing session.

Authenticated endpoints:

```http
POST   /api/v1/playthroughs/{playthroughId}/join-session
DELETE /api/v1/playthroughs/{playthroughId}/join-session
```

Anonymous endpoint and frontend route:

```http
GET /api/v1/playthrough-sessions/{token}
```

```text
/join/:token
```

Implementation files include:

- `Eldoria.Api/Controllers/PlaythroughJoinSessionController.cs`
- `Eldoria.Api/PlaythroughRealtime/PlaythroughHub.cs`
- `Eldoria.Application/Services/PlaythroughJoinSessionService.cs`
- `Eldoria.Infrastructure/Db/Repositories/PlaythroughJoinSessionRepository.cs`
- `Lunoria.Web/src/features/playthroughSession/`
- `Lunoria.Web/src/pages/public/PlaythroughGuestPage.tsx`

Security and lifecycle:

- Tokens use 32 cryptographically random bytes encoded as base64url.
- Only the SHA-256 token hash is stored.
- One join-session row exists per playthrough.
- Invitations expire after 24 hours.
- Opening Join again rotates the token and closes connections using the old invitation.
- The game master can revoke the session from the QR dialog.
- Completed playthroughs cannot create or use guest invitations.
- Anonymous snapshot and hub access validate the token.
- Anonymous routes are read-only and rate-limited.

The QR dialog uses `qrcode.react`, supports copying the link, shows expiration, and provides Close Guest Session. For local phone testing, generate the QR from a LAN-accessible frontend URL, not `localhost`; `VITE_API_BASE_URL` and API CORS must also be reachable from the phone.

## Public phone UI

The full-screen guest deck contains:

1. All journey-character views.
2. Active scene-character instance views.
3. Event Log as the final view.

Each character view shows the image, identity, current stats, spells, equipment and modifiers, consumables, and runtime statuses. Duplicate scene instances remain separate cards.

If there are no in-progress scenes, no scene-character cards appear. The current model can have multiple in-progress scenes, so the public snapshot includes active characters from every in-progress scene.

The character cards use the full available horizontal width. Journey, scene, and live-reconnecting text is intentionally compact so more character information fits on smaller screens.

The previous side arrows were replaced by navigation controls docked to the bottom of the viewport. The card content sits above the dock without extra page space passing beneath it. Selection is preserved across live refreshes when possible, and navigating scrolls the next view to the top.

`BreakpointIndicator` remains mounted for future diagnostics but is disabled through its flag and does not render.

## SignalR behavior

Hub route:

```text
/hubs/playthrough
```

The guest calls `JoinPlaythrough(token)`. After token validation, the connection joins `playthrough:{playthroughId}`. Automatic reconnect rejoins the group.

Server events:

- `PlaythroughUpdated` — refetch the public snapshot.
- `SessionClosed` — stop displaying the session.

Existing notifications cover scene start/end, participant activation/addition, live chest creation, stat updates, movement, action forfeiture, attack resolution, chest resolution, potion use, and item trading.

Future gameplay mutations must call `IPlaythroughRealtimeNotifier` after their transaction succeeds.

## Public snapshot contents

Dedicated public DTOs expose only the playthrough name, active scene names, sanitized journey and scene character state, spells, inventories, equipment, stats, and event logs.

They do not expose user IDs, source IDs, filenames, dialogs, chests, or game-master controls.

## Schema and migration status

- `Playthrough.SortOrder` was removed by `20260822152400_RemovePlaythroughSortOrder`.
- Duplicate scene character instances are supported by `20260822170000_AllowDuplicateScenePlaythroughCharacterInstances`.
- Persistent join sessions were added by `20260822222241_AddPlaythroughJoinSessions`.
- Combat and downed-player state were added by `20260823114801_AddScenePlaythroughCombat`.
- Runtime scene chests were added by `20260823162247_AddRuntimeSceneChests`.
- `PlaythroughJoinSessions` has unique indexes on `PlaythroughId` and `TokenHash`.
- The runtime-chest migration and all earlier migrations were applied successfully to the configured database as of August 23, 2026.
- The earlier snapshot rebuild remains `20260816142304_RebuildPlaythroughSchema`.

## Verification status

- The API and frontend production builds succeed.
- Focused chest, combat, potion, equipment-effect, trade, lifecycle, and chest-creation tests pass. The most recent focused run covering potion, chest, and combat passed 12 of 12 tests.
- The full backend test project currently has stale compile failures in legacy `JourneyWorkflowTests.cs`, `SceneWorkflowTests.cs`, and `InventoryAndEquipmentTests.cs`; these reference types removed by the newer playthrough model.
- The frontend has no automated test suite.
- Vite reports an existing SignalR annotation warning and a large JavaScript bundle warning. The main production bundle is approximately 1.24 MB minified and 371 KB gzip.
- Browser automation was unavailable during the latest UI work, so the newest playthrough interactions still need an end-to-end real-browser and phone test.
- `20260823162247_AddRuntimeSceneChests` was generated and applied successfully.

## Immediate next work

The core playthrough action loop is implemented. The next priorities are:

1. Persist turn phase so refresh cannot repeat movement.
2. Repair the stale full backend test suite.
3. Add frontend and end-to-end multiplayer playthrough tests.
4. Perform full real-device testing of guest joining, reconnecting, every action, and scene ending.
5. Decide whether only one scene may be in progress.
6. Complete Azure production hardening and deployment work described below.

## Azure deployment, cost, and performance

The detailed plan is in `azure-deployment-cost-and-performance-plan.md` at the repository root.

Recommended initial fast closed-beta architecture:

- Azure Static Web Apps Standard for `Lunoria.Web`.
- Linux Azure App Service B2 for `Eldoria.Api` and its SignalR hubs.
- Provisioned Azure SQL Standard S0.
- Existing Azure Blob Storage using the Hot tier and locally redundant storage.
- Application Insights with cost-conscious telemetry.
- API, SQL, and Blob Storage in the same nearby region, initially East US 2.

The expected starting cost is about $50-$60 per month. A lower-cost B1 plus Azure SQL Basic configuration is about $30-$40 per month but is more likely to feel slower than local development. Do not add Azure SignalR Service, Premium App Service, Azure Front Door, or private endpoints until monitoring shows a real need.

Before deployment, add configurable production CORS, disable or protect Swagger in Production, add global production error handling and a health endpoint, add GitHub Actions workflows, add a Static Web Apps navigation fallback, and create a controlled EF Core migration release step. Configure a $60 Azure budget with 50%, 80%, and 100% alerts.

## Earlier related fixes

### Scene creation retry behavior

`Lunoria.Web/src/features/scenes/components/SceneEditorForm.tsx` preserves a created scene when custom-grid creation fails, uses distinct React keys for scene and grid state, normalizes grid color to `#ffffff`, and omits update-only `removeBackground` during grid creation.

Existing duplicate scene records were not removed.

### Image deletion policy

`AzureStorageBlob.DeletePhotoFromUrl(string?)` intentionally returns success without deleting a blob. Preserve this unless the user explicitly changes the policy.
