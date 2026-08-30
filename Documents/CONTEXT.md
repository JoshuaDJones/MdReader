# Lunoria Application Context

## Purpose and scope

This file is the fast orientation guide for work in this repository. Lunoria is a companion application for a handcrafted, grid-based tabletop RPG. A game master/operator uses it to author campaigns, manage character and item catalogs, prepare journeys and scenes, present narrative content, and track live playthrough state.

There are three client projects in the repository, but only one is active:

- **Active client:** `Lunoria.Web` — the new React/TypeScript client and the only frontend that should receive new work.
- **Inactive/legacy:** `Eldoria.Web` — old React client. Do not use it as the source of truth and do not document or extend it.
- **Inactive/legacy:** `Eldoria.BlazorClient` — unused Blazor client. Do not document or extend it.

Everything outside those two legacy client folders is considered active unless the code or current task says otherwise.

## User verification preference

The repository owner will perform verification manually. **Do not run builds, tests, linters, browser/UI automation, local development servers, or database updates unless the user explicitly asks for that specific verification action.** This includes `dotnet build`, `dotnet test`, `npm run build`, `npm run lint`, browser testing, starting the API/Vite processes, and applying EF migrations. Implement requested changes and report what should be verified, but leave execution to the user to conserve their token usage.

## Product model

The main authoring hierarchy is:

```text
User
├── Catalogs: Characters, Spell Types/Spells, Consumables, Equipment
└── Series
    └── Journeys
        ├── Journey Characters
        ├── Journey Intro Pages
        ├── Journey Playthroughs
        └── Scenes
            ├── Scene Characters
            ├── Dialogs → Pages → Page Sections
            ├── Events → Actions
            ├── Chests → Roll-based Loot Entries
            └── Scene Playthroughs
```

- A `Series` groups journeys.
- A `Journey` is a campaign/mission containing ordered scenes, selected journey characters, intro pages, and multiple runs.
- A `Scene` is an encounter or narrative unit with a display image, optional internal grid or external grid URL, characters, dialogue, events, chests, and playthrough records.
- `SceneGrid` is an optional one-to-one scene-owned grid definition containing rows, columns, grid color, and an optional Azure Blob background. It is intentionally not copied into playthrough records yet.
- `Character`, `Spell`, `SpellType`, `ConsumableItem`, and `EquippableItem` are user-owned reusable catalog templates.
- `JourneyCharacter` is the journey-level mutable version of a character template. It preserves journey configuration/state and journey-specific spells.
- `JourneyPlaythrough` represents one run of a journey. Only one active playthrough per journey is intended.
- `JourneyPlaythroughCharacter` snapshots mutable character state for a run.
- `ScenePlaythrough` tracks a scene within a journey playthrough: status, timing, round/current participant, scene-specific character snapshots, participants, events, and chests.
- Dialogue is `SceneDialog` → ordered `DialogPage` → ordered `DialogPageSection`. Sections are narrator text or associated with a character.
- Scene events contain ordered actions. The currently modeled action adjusts a character stat using a target type, stat type, and operation.
- A scene chest has die sides and loot entries with roll ranges. Each loot entry points to exactly one kind of loot conceptually: consumable or equippable item.

Characters support Player, NPC, and Enemy types, base HP/MP and combat/movement values, separate consumable/equipment capacities, optional alternate forms, portraits, spell assignments, and dialog colors. Equipment contributes effective-stat modifiers, damage reduction, optional spell-type modifiers, and granted spells. Consumables provide HP/MP effects.

## Active architecture

The backend is a layered .NET 8 application:

```text
Eldoria.Api → Eldoria.Application → Eldoria.Core
     │                                  ↑
     └──────── Eldoria.Infrastructure ──┘
```

- `Eldoria.Core`: domain entities, enums, and repository interfaces. It has no project dependencies.
- `Eldoria.Application`: DTOs, entity-to-DTO mappings, service interfaces/implementations, result/error handling, authentication, and Azure Blob abstractions. It references Core.
- `Eldoria.Infrastructure`: EF Core SQL Server `ApplicationDbContext`, entity configurations, migrations, and repositories. It references Core.
- `Eldoria.Api`: controllers, HTTP request models, JWT/CORS/Swagger setup, and dependency composition. It references Application and Infrastructure.
- `Eldoria.Application.Tests`: xUnit/NSubstitute service tests.
- `Lunoria.Web`: active Vite/React client.

The usual backend request path is controller → application service → repository/EF Core → mapping to an application DTO. Put business rules and ownership checks in services/repositories, not controllers. Core entities should not depend on API or UI types.

## Backend conventions

- Target framework and SDK: .NET 8; `global.json` requests SDK `8.0.204` with latest-feature roll-forward.
- Persistence: Entity Framework Core 8 with SQL Server. `ApplicationDbContext` applies configurations from the Infrastructure assembly. Migrations live in `Eldoria.Infrastructure/Migrations`.
- Authentication: custom email/password authentication issues JWTs. The API fallback authorization policy requires an authenticated user; only auth endpoints and Swagger are anonymous.
- Ownership: catalog and journey operations are scoped to the authenticated user ID. Follow parent relationships when authorizing nested resources; a client-supplied ID is not proof of ownership.
- HTTP API: controller routes are under `/api/v1`; the frontend base URL is expected to include that prefix. Most resource create/update requests involving images are multipart form data.
- Results: application services commonly return `Result`/`Result<T>`. Controllers translate failures into HTTP responses. A few older endpoints still return the result envelope directly, so check the actual controller and frontend API function before changing a contract.
- Images: uploaded through `ImagesController`/`ImagesService` and stored in Azure Blob Storage. Do not manually set multipart boundaries.
- Configuration keys: `ConnectionStrings:DefaultConnection`, `Jwt:{Issuer,Audience,Key}`, and `AzureStorage:{AccountName,ContainerName,AccessKey}`. Treat values as secrets; prefer user secrets/environment variables and never copy them into documentation or commits.
- Local API profile: `http://localhost:5243`; Swagger is enabled. Development deliberately avoids HTTPS redirection so Vite CORS calls work without a machine-specific certificate.
- Allowed frontend development origins currently include ports 5173 and 5174.

Primary API resource areas include auth, series, journeys, journey intro pages/characters/spells/playthroughs, scenes, scene characters/dialogs/events, dialog pages/sections, characters, spells/types, consumables, equipment, and images.

## Active frontend: `Lunoria.Web`

The active client uses React 19, TypeScript, Vite, React Router, Axios, Formik/Yup, Tailwind CSS 4, Tiptap, and small focused UI libraries. Use the `@/` alias for `src/` imports.

The client is feature-oriented:

- `src/app`: router, layouts, global providers, and route guards.
- `src/pages`: route-level composition; keep pages thin.
- `src/features/<domain>`: domain types, API functions, and owned components.
- `src/components/ui`: reusable domain-neutral primitives.
- `src/components/layout`: shared page structure.
- `src/lib`: API client, form-data helpers, and cross-cutting utilities.
- `src/styles/index.css`: Tailwind setup and the semantic design-token source of truth.

Global provider order in `main.tsx` is Auth → Toast → Confirm Dialog → Modal Stack → Router. Use:

- page-owned `Drawer` for ordinary create/edit flows;
- `useConfirmDialog` for destructive confirmation;
- `useToast` for operation feedback;
- `useModalStack` for nested overlays or focused picker/viewer workflows.

Authentication stores the JWT in local storage under `auth_token`. `apiClient` adds it as a bearer token and requires `VITE_API_BASE_URL` (normally `http://localhost:5243/api/v1` for local development).

Current authenticated routes cover home, series/journeys and journey editing, characters, spells, consumables, equipment, dialogs, intro pages, play hub, and the component showcase. The visual system uses semantic tokens such as `brand`, `surface`, `content`, `danger`, `health`, and `mana`; choose tokens by meaning rather than literal color.

The public `/grid-prototype` route is an isolated, temporary SignalR board prototype. A host creates an in-memory 20×36 session and receives an eight-character code plus a host token stored in browser session storage. Anyone with the code can join and move snapped character tokens; only the host can add/remove tokens or change the background and grid color. The anonymous character feed intentionally exposes all non-deleted character names/images for this prototype. Sessions expire after eight hours, disappear on API restart, and are not part of the journey/playthrough domain. See `docs/grid-prototype.md`.

Scenes can now own one persisted `SceneGrid`. The scene create/edit drawer offers internal grid, external grid URL, or no grid. Internal grid setup appears as a second drawer page where dimensions, color, and background are configured. Scene cards show a **Show grid** button. External grids open their URL; internal grids open the authenticated `/scene-grids/:sceneId` route. That internal viewer is intentionally display-only: it renders only the saved background and grid across the full browser viewport with no toolbar, controls, session information, or character UI.

The journey editor is for authoring only and must not list, start, resume, or display playthroughs. Its **Play** button navigates to the journey Play Hub. The Play Hub owns the **Start** action and the previous-playthrough list/resume/log controls.

## Important current-state caveats

The domain redesign is ahead of parts of the API/client integration. Verify real implementations before assuming a route or workflow exists.

- Current Core entities use `ScenePlaythrough`, `ScenePlaythroughParticipant`, and related snapshot entities. Some tests and `Lunoria.Web/src/features/scenes/api/scenesApi.ts` still expose older `SceneProgress`, participant-turn, and progress routes that have no matching active controller/service/repository in the current tree.
- The frontend API layer also contains calls for journey-character consumable/equipment assignment and scene-character item/state mutation whose corresponding controllers are not present in the current API controller set.
- `getLegacySceneDashboard` is explicitly legacy-shaped even though it remains in the new client API layer.
- `docs/entity-redesign-application-gaps.md` is useful historical context, but parts of its checklist refer to `Eldoria.Web` or pre-redesign names and are not authoritative for `Lunoria.Web`.
- `docs/realtime-player-session-implementation.md` describes a broader proposed player-session architecture. A narrower grid-prototype SignalR hub is active at `/hubs/grid-prototype`, but it should not be mistaken for the full proposed playthrough/player-session design.
- Some repository tests refer to pre-rename types/members such as `ISceneProgressRepository`, `SceneProgressService`, `SceneParticipantTurn`, and `SceneProgressStatus`; check build status before relying on those tests.
- Scene-chest/loot-entry DTO and repository work is currently in progress in the working tree. Always inspect `git status` and preserve pre-existing edits.
- The solution file still includes the unused Blazor client and does not include `Lunoria.Web`; this is historical solution structure, not frontend ownership.

When resolving drift, prefer the current Core entity model and confirmed API behavior, then update Application, API, tests, and `Lunoria.Web` contracts together.

## Development commands

These commands are reference-only. Do not run them unless the user explicitly requests verification, as described in **User verification preference** above.

From the repository root:

```powershell
dotnet restore Eldoria.sln
dotnet build Eldoria.sln
dotnet test Eldoria.Application.Tests/Eldoria.Application.Tests.csproj
dotnet run --project Eldoria.Api/Eldoria.Api.csproj --launch-profile http
```

For the active client:

```powershell
cd Lunoria.Web
npm install
npm run dev
npm run build
npm run lint
```

Common EF commands are documented in `docs/entity-framework-commands.md`. Use `Eldoria.Api` as the startup project and `Eldoria.Infrastructure` as the migrations project. Inspect generated migrations for accidental drop/add operations before applying them.

## Where to start for common changes

- Domain or relationship change: Core entity/enums → Infrastructure configuration/migration/repository → Application DTO/mapping/service → API request/controller → `Lunoria.Web` type/API/UI → tests.
- New catalog workflow: confirm user ownership at every read and mutation; validate referenced catalog entities belong to the same user.
- Image-backed form: use multipart request models, `toFormData`, and the existing image service; retain/delete old blobs deliberately during updates.
- New frontend feature: keep API/types/components under its owning `src/features` directory, keep route pages compositional, and promote only genuinely shared primitives.
- Playthrough work: distinguish templates (`Character`, `SceneCharacter`), journey state (`JourneyCharacter`), journey-run snapshots (`JourneyPlaythroughCharacter`), and scene-run snapshots (`ScenePlaythroughCharacter`). Avoid mutating the wrong lifecycle layer.
- Inventory/equipment work: effective stats derive from stored character state plus currently equipped modifiers; capacity rules and HP clamping belong in backend business logic.

## Supporting documentation

- `README.md`: product overview and tabletop gameplay concepts.
- `docs/entity-redesign-summary.md`: historical reasoning behind the expanded domain model.
- `docs/entity-redesign-application-gaps.md`: migration/integration history; validate against current code.
- `docs/entity-framework-relationships.md`: EF relationship notes.
- `docs/entity-framework-commands.md`: migration commands.
- `docs/realtime-player-session-implementation.md`: proposed future real-time player-session architecture.
- `Lunoria.Web/docs/project-structure.md`: frontend placement and naming rules.
- `Lunoria.Web/docs/api-layer.md`: active client API conventions.
- `Lunoria.Web/docs/dialogs-and-modals.md`: overlay/provider usage.
- `Lunoria.Web/docs/design-tokens.md`: semantic color system.

Before editing, run `git status`, read the files in the affected vertical slice, and do not modify either legacy client unless the user explicitly requests it.
