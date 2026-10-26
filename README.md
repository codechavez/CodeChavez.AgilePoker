# CodeChavez.AgilePoker

A slim, real-time **Planning Poker** app for Scrum teams, built with **Blazor Server**, **SignalR** and **MudBlazor** on **.NET 9**.

Team members join a shared session by URL, pick a card in secret, and reveal everyone's votes at once. The app shows the average of the numeric votes. Nothing is stored in a database: all session state lives in server memory and is pushed to clients in real time.

## Features

- **No sign-up.** Open the site and you get a new session. Share the URL to invite others.
- **Fibonacci deck up to 21:** `1, 2, 3, 5, 8, 13, 21`, plus `?` (unsure) and `☕` (I need a break). Anything bigger than 21 should probably be split into smaller stories.
- **Hidden votes.** Until someone reveals, a card shows 💭 (not voted yet) or 🙈 (voted, still hidden).
- **Reveal / Hide / Reset** for the whole table, plus the **average** of numeric votes once revealed.
- **Remembers you.** Your name and a player ID are kept in the browser's `localStorage`, so a page refresh puts you back in the session.
- **Rename** yourself at any time, and **End Session** to close the room for everyone.

## Tech stack

| Concern            | Technology                                              |
|--------------------|---------------------------------------------------------|
| Runtime            | .NET 9 / ASP.NET Core                                   |
| UI                 | Blazor Server (Interactive Server render mode)          |
| Component library  | [MudBlazor](https://mudblazor.com/) 8.x                 |
| Real-time sync     | ASP.NET Core SignalR (`PokerHub`)                       |
| Container          | Docker (`mcr.microsoft.com/dotnet/aspnet:9.0`)          |
| CI/CD              | GitHub Actions → Docker Hub                             |

## Project structure

```
CodeChavez.AgilePoker/
├── .docker/
│   └── agile.poker.Dockerfile        # Multi-stage build: SDK image to build, ASP.NET image to run
├── .github/workflows/
│   ├── agile.poker-cd.yml            # On push to main: build, publish, build and push Docker image
│   └── agile.poker-ci.yml            # CI workflow (currently empty)
├── CodeChavez.AgilePoker.sln
├── LICENSE                           # Apache 2.0
└── src/CodeChavez.AgilePoker/
    ├── Program.cs                    # App startup: Razor components, MudBlazor, SignalR, /pokerhub
    ├── Components/
    │   ├── App.razor                 # Root HTML document
    │   ├── Routes.razor              # Router
    │   ├── _Imports.razor
    │   ├── Layout/
    │   │   ├── MainLayout.razor      # MudBlazor theme and providers (snackbar, dialogs, popovers)
    │   │   └── NavMenu.razor         # Template leftover, not used by the layout
    │   └── Pages/
    │       ├── AgilePoker.razor      # The whole app UI and client-side logic (route "/")
    │       └── Error.razor
    ├── Services/
    │   └── PokerHub.cs               # SignalR hub: in-memory sessions, players and votes
    ├── DTOs/
    │   └── PlayerVote.cs             # A player's state in a session
    ├── Properties/launchSettings.json
    ├── appsettings*.json
    └── wwwroot/                      # app.css, favicon, logos
```

### Key pieces

**`Services/PokerHub.cs`** is the server side of the game. It keeps a static, in-memory dictionary of `sessionId → List<PlayerVote>` and exposes these hub methods:

| Method                                   | What it does                                                         |
|------------------------------------------|----------------------------------------------------------------------|
| `Join(sessionId, playerName, playerId)`  | Adds the player to the session, or reattaches them by `playerId` (this clears their vote). Joins the SignalR group. |
| `Vote(sessionId, vote)`                  | Records the caller's card and marks them as voted.                    |
| `Reveal(sessionId)` / `Hide(sessionId)`  | Shows or hides every player's vote.                                   |
| `Reset(sessionId)`                       | Clears all votes for the next round.                                  |
| `UpdatePlayerName(sessionId, newName)`   | Renames the caller.                                                   |
| `EndSession(sessionId)`                  | Sends `SessionEnded` to every player and deletes the session.         |
| `OnDisconnectedAsync`                    | Removes a player when their connection drops.                         |

After every change the hub broadcasts **`PlayersUpdated`** with the full player list to the session's group.

**`DTOs/PlayerVote.cs`** holds `Name`, `Vote`, `HasVoted`, `Revealed`, `ConnectionId`, `IpAddress` and `PlayerId`.

**`Components/Pages/AgilePoker.razor`** is the only page. It reads or creates the `?session=` query parameter, loads the player ID and name from `localStorage`, opens a `HubConnection` to `/pokerhub`, and re-renders whenever `PlayersUpdated` arrives. It also calculates the average from the revealed numeric votes.

## Workflow

### How a round works

1. **Open the site.** If the URL has no `?session=` parameter, a new GUID is generated and you are redirected to `/?session=<guid>`.
2. **Enter your name and click Join.** The page connects to the hub and calls `Join`. Your name and a persistent player ID are saved in `localStorage`.
3. **Invite the team** by copying the session URL from the "Invite players" field.
4. **Everyone picks a card.** Each pick calls `Vote`. Other players see 🙈 on your card but not the value.
5. **One person clicks Reveal.** Every vote is shown, along with the average of the numeric votes (`?` and `☕` are left out).
6. **Discuss, then click Reset** to clear the votes and start the next round.
7. **End Session** (top bar) when you are done. Everyone is notified and the session is removed from memory.

### Message flow

```
 Browser A                 PokerHub (server memory)               Browser B
    |  Join(session, name, id) ->|                                    |
    |                            |-- PlayersUpdated ----------------->|
    |<----------- PlayersUpdated-|                                    |
    |  Vote(session, "5") ------>|                                    |
    |<----------- PlayersUpdated-|-- PlayersUpdated (shows 🙈) ------>|
    |  Reveal(session) --------->|                                    |
    |<----------- PlayersUpdated-|-- PlayersUpdated (shows "5") ----->|
    |  Reset(session) ---------->|                                    |
    |<----------- PlayersUpdated-|-- PlayersUpdated (cleared) ------->|
```

## Running locally

**Prerequisites:** [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

```bash
dotnet restore
dotnet run --project src/CodeChavez.AgilePoker
```

The app listens on `http://localhost:7080`. Use the `AgilePoker.HTTPS` launch profile for `https://localhost:7081`:

```bash
dotnet run --project src/CodeChavez.AgilePoker --launch-profile AgilePoker.HTTPS
```

To try multiplayer, open the session URL in a second browser or a private window. Two tabs in the same browser share `localStorage`, so they count as the same player.

## Running with Docker

```bash
docker build -f .docker/agile.poker.Dockerfile -t codechavez.agile.poker .
docker run -p 7080:7080 codechavez.agile.poker
```

Then browse to `http://localhost:7080`. The image is published as a self-contained `linux-x64` build and serves HTTP on ports `7080` and `7081` (`ASPNETCORE_URLS`). Put TLS termination in front of it, such as a reverse proxy or your cloud platform. `X-Forwarded-For` and `X-Forwarded-Proto` headers are honored.

## CI/CD

`.github/workflows/agile.poker-cd.yml` runs on every push to `main`, or manually through `workflow_dispatch`:

1. Check out the repo and set up .NET 9
2. `dotnet restore`, `dotnet build` and `dotnet publish` in Release
3. Log in to Docker Hub with the `DOCKER_USERNAME` and `DOCKER_PASSWORD` repository secrets
4. Build the image from `.docker/agile.poker.Dockerfile`
5. Push `teslacode/codechavez.agile.poker:latest`

`agile.poker-ci.yml` exists but is empty for now.

## Limitations

- **State is in memory only.** Restarting the app wipes every session, and the app cannot be scaled out to more than one instance without a shared backplane such as Redis for SignalR plus a shared session store.
- **No authentication or roles.** Anyone with the session URL can join, vote, reveal, reset or end the session.
- **The deck is fixed.** To change it, edit the `cards` array in `AgilePoker.razor`.

## Terms of Use

By using this application, its hosted website, its source code or its Docker image (together, "the Software"), you agree to these terms. If you do not agree, do not use the Software.

1. **Free and as-is.** The Software is offered free of charge and provided **"AS IS" and "AS AVAILABLE"**, without warranties or conditions of any kind, express or implied. This includes, without limitation, warranties of merchantability, fitness for a particular purpose, accuracy, availability and non-infringement.
2. **Release of responsibility and liability.** To the maximum extent permitted by applicable law, you release the author and contributors from **any and all responsibility and liability** arising from or related to your use of the Software, or your inability to use it. In no event shall the author or contributors be liable for any claim, damages or loss of any kind, whether direct, indirect, incidental, special, consequential or punitive. This includes loss of data, profits, business or goodwill, and work stoppage, even if the author was advised that such damages were possible.
3. **No guarantee of service.** The hosted website may change, be interrupted or be taken down at any time without notice. Sessions are held in memory only and can be lost at any time. Do not rely on the Software for anything critical.
4. **Your content and conduct.** You are solely responsible for the names and any other information you enter, and for how you and your team use the Software. Be respectful and be kind. Do not enter sensitive, confidential or personal information.
5. **Data.** The Software does not store data in a database. While a session is active, the server keeps player names, votes and connection details, including IP address, in memory. Your browser stores your player name and a player ID in `localStorage`.
6. **Self-hosting and modifications.** If you deploy, modify or redistribute the Software, you do so at your own risk and are fully responsible for that deployment and its users.
7. **License.** These terms are in addition to the [Apache License 2.0](LICENSE). See in particular Section 7 (Disclaimer of Warranty) and Section 8 (Limitation of Liability).

## License

[Apache License 2.0](LICENSE)
