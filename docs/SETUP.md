# Feirb — Developer Setup Guide

## Prerequisites

### Required

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.100+ (`global.json`, rolls forward to newer 10.0.x feature bands) | Build and run the solution |
| Container runtime: [Docker](https://www.docker.com/) **or** [Podman](https://podman.io/) | Latest | Aspire containers (PostgreSQL, pgAdmin, Ollama, GreenMail) |
| [Git](https://git-scm.com/) | Latest | Version control |

> The **Aspire workload is obsolete and must not be installed.** The AppHost uses the `Aspire.AppHost.Sdk` NuGet SDK, which `dotnet restore` fetches automatically. The Aspire *CLI* (below) is a separate, optional tool.

### Required for the dev-harness scripts (`.claude/skills/dev-harness/`)

| Tool | Purpose |
|------|---------|
| `bash` | The scripts are bash (Linux, macOS, WSL2 or Git Bash on Windows) |
| `curl` | Health checks and API calls |
| `python3` | JSON parsing and SMTP test mails |
| `psql` (PostgreSQL client) | `query.sh` connects to the Aspire-managed database on `localhost:15432` |

### Recommended / optional

| Tool | Purpose |
|------|---------|
| [Aspire CLI](https://aspire.dev/get-started/install-cli/) | `aspire start`, `aspire describe`, `aspire logs`; also the `aspire` MCP server in `.mcp.json` (`aspire agent mcp`) |
| [Node.js](https://nodejs.org/) LTS (22+) incl. `npm`/`npx` | Playwright MCP, Postgres MCP, Playwright E2E tests (`tests/playwright`), Bruno API tests (`npx bru`) |
| [GitHub CLI (`gh`)](https://cli.github.com/) | Issue and PR management |
| [Claude Code](https://claude.com/claude-code) | AI-assisted development; project skills and MCP servers live in `.claude/` and `.mcp.json` |

**Recommended IDE (pick one):**
- [Visual Studio 2022](https://visualstudio.microsoft.com/) 17.12+ with ASP.NET workload
- [JetBrains Rider](https://www.jetbrains.com/rider/) 2024.3+
- [VS Code](https://code.visualstudio.com/) with [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

### Linux system libraries

The API uses SkiaSharp (avatar processing). On minimal Debian/Ubuntu installs the native library needs fontconfig:

```bash
sudo apt install libfontconfig1
```

## Installing the Prerequisites (fresh machine)

The commands below target Debian/Ubuntu. Equivalents exist for every tool on macOS (Homebrew) and Windows (WinGet, run the dev-harness scripts from WSL2 or Git Bash).

### 1. Base tools

```bash
sudo apt update
sudo apt install -y git curl python3 postgresql-client libfontconfig1
```

### 2. .NET 10 SDK

Follow the [official install instructions](https://learn.microsoft.com/dotnet/core/install/linux) for your distribution, or use the distribution-neutral install script:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0

# Make it available in every shell (add to ~/.bashrc)
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools"

dotnet --version   # must print 10.0.x
```

### 3. Container runtime (choose one)

**Docker** — follow [Install Docker Engine](https://docs.docker.com/engine/install/) (or Docker Desktop), then allow your user to run it without `sudo`:

```bash
sudo usermod -aG docker "$USER"   # log out and back in afterwards
docker info
```

**Podman** — install it and tell Aspire to use it (add the `export` to `~/.bashrc`):

```bash
sudo apt install -y podman
export ASPIRE_CONTAINER_RUNTIME=podman
podman info
```

The dev-harness scripts and `postgres-mcp.sh` prefer `podman` and fall back to `docker`. `tests/run-tests.sh` (containerized Bruno/Playwright suite) needs `docker compose` or `podman-compose`.

### 4. Aspire CLI (recommended)

```bash
curl -sSL https://aspire.dev/install.sh | bash   # Windows: irm https://aspire.dev/install.ps1 | iex
aspire --version
```

Restart the terminal if `aspire` is not found afterwards (the installer adds it to `PATH`).

### 5. Node.js (recommended)

Install a current LTS release, e.g. via [nvm](https://github.com/nvm-sh/nvm):

```bash
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.3/install.sh | bash
# open a new shell, then:
nvm install --lts
node --version && npx --version
```

### 6. Git identity and GitHub access

Set your commit identity once (commits fail with "Author identity unknown" otherwise):

```bash
git config --global user.name "Your Name"
git config --global user.email "you@example.com"
```

The repository is cloned via SSH (`git@github.com:mamu7211/feirb-mailclient.git`), so add an SSH key to your GitHub account first ([guide](https://docs.github.com/authentication/connecting-to-github-with-ssh)). For issue/PR work install the [GitHub CLI](https://github.com/cli/cli#installation) and run `gh auth login`.

### 7. Trust the ASP.NET Core dev certificate (optional)

```bash
dotnet dev-certs https --trust
```

On Linux this only covers some clients; browsers may still warn about `https://localhost:7272`. The dev-harness scripts use `curl -k` and the Playwright MCP runs with `--ignore-https-errors`, so nothing depends on it.

## Clone & First Run

```bash
# Clone the repository
git clone git@github.com:mamu7211/feirb-mailclient.git
cd feirb-mailclient

# Restore dependencies and build
dotnet restore Feirb.sln
dotnet build Feirb.sln

# Start with database seeding (recommended for development)
.claude/skills/dev-harness/start.sh --seeding

# Authenticate and check that everything is up
.claude/skills/dev-harness/login.sh
.claude/skills/dev-harness/status.sh
```

The dev-harness `start.sh --seeding` script starts Aspire with database seeding enabled, creating a preconfigured admin user and SMTP settings so you can skip the setup wizard. Use `start.sh` (without `--seeding`) to start bare. Without the dev-harness you can run `dotnet run --project src/Feirb.AppHost` (or `aspire start`) directly.

On first run, Aspire will:
1. Start a PostgreSQL container (plus pgAdmin) for the database
2. Start the API, which also serves the Blazor WebAssembly frontend
3. Pull and start the Ollama container
4. Download the `qwen3:0.6b` development model (~400MB, cached in `.ollama-data/` across restarts)
5. Start GreenMail for development email testing (SMTP + IMAP)

The first start pulls several container images and the Ollama model, so it can take a few minutes.

### Verify the installation

```bash
dotnet --version        # 10.0.x
aspire --version        # optional
docker info             # or: podman info
psql --version
python3 --version
node --version          # optional
dotnet test             # unit tests use in-memory databases, no containers required
dotnet format --verify-no-changes
```

### Claude Code setup (optional)

If you develop with Claude Code, the project ships its skills in `.claude/skills/` and MCP servers in `.mcp.json`:

| MCP server | Needs |
|------------|-------|
| `aspire` | `aspire` CLI on `PATH` |
| `playwright` | `npx` (Node.js) |
| `postgres` | `npx`, a running Aspire Postgres container (`postgres-mcp.sh` reads its password) |

Servers that fail to connect are simply unavailable; the dev-harness shell scripts work without them. Claude Code asks once to approve the project MCP servers; the choice is stored in the git-ignored `.claude/settings.local.json`. Shared tool permissions are in `.claude/settings.json`.

## Development Services

Once running, these services are available:

| Service | URL | Description |
|---------|-----|-------------|
| Aspire Dashboard | https://localhost:18888 | Logs, traces, metrics, health |
| Blazor Frontend + API Backend | https://localhost:7272 | The mail client UI and REST API (the API serves the WebAssembly frontend) |
| PostgreSQL | localhost:15432 | Database (`mailclientdb`, user `postgres`) |
| pgAdmin | Aspire-assigned port | Database UI, link in the Aspire Dashboard |
| Ollama | http://localhost:11434 | Local LLM (`qwen3:0.6b` in development) |
| GreenMail API / OpenAPI UI | http://localhost:8080 | View and manage test emails |
| GreenMail SMTP | localhost:3025 | Send test emails |
| GreenMail IMAP | localhost:3143 | Fetch test emails |

## Helper Scripts

| Script | Purpose |
|--------|---------|
| `.claude/skills/dev-harness/start.sh` | Start Aspire (bare, no seed data) |
| `.claude/skills/dev-harness/start.sh --seeding` | Start Aspire with database seeding (admin + alice users, mailboxes, GreenMail SMTP) |
| `.claude/skills/dev-harness/stop.sh` | Stop Aspire gracefully |
| `.claude/skills/dev-harness/cleanup.sh` | Stop Aspire and remove containers + volumes for fresh start |
| `.claude/skills/dev-harness/login.sh [user] [pass]` | Authenticate (default: admin) and store tokens |
| `.claude/skills/dev-harness/status.sh` | Check API, GreenMail, Ollama and token |
| `.claude/skills/dev-harness/check.sh /api/...` | GET an API endpoint with the stored token |
| `.claude/skills/dev-harness/query.sh 'SELECT ...'` | Run SQL against PostgreSQL via `psql` |
| `.claude/skills/dev-harness/send-mail.sh` | Send a test email via SMTP |
| `.claude/skills/dev-harness/trigger-job.sh <type>` | Trigger a background job |
| `.claude/skills/dev-harness/logs.sh` | Show recent job execution history |

### Database Seeding

When started via `.claude/skills/dev-harness/start.sh --seeding` (or with `FEIRB_SEED_DATA=true`), the following data is seeded:

| Data | Value |
|------|-------|
| Admin login | username `admin`, password `password` (email: `admin@feirb.local`) |
| Alice login | username `alice`, password `password` (email: `alice@feirb.local`) |
| System SMTP | `localhost:3025` (GreenMail) |
| SMTP from address | `noreply@feirb.local` |
| TLS / Auth | disabled |
| IMAP host | `localhost:3143` (GreenMail) |
| Mailbox credentials | email address as both username and password |

The seeding is idempotent — it checks whether the data already exists and skips if so. That means it does **not** reset the credentials of users that already exist: if you completed the setup wizard before, or seeded with an older version, log in with the credentials you chose then, or reset the database with `.claude/skills/dev-harness/cleanup.sh` and start again.

> **Log in with the username, not the email address.** The login only looks up `Username`. The mailbox credentials in the table above (email address as username and password) are the GreenMail IMAP/SMTP account, not the Feirb login.

> **Production safety:** `DatabaseSeeder` refuses to run when `ASPNETCORE_ENVIRONMENT=Production`, even if `FEIRB_SEED_DATA=true` is set. Seeded accounts use well-known credentials and are intended for development and testing only. Attempting to seed in Production raises an exception and stops application startup.

## Development Workflow

### Branching

| Prefix | Use |
|--------|-----|
| `feature/` | New features |
| `fix/` | Bug fixes |
| `docs/` | Documentation changes |
| `chore/` | Tooling, CI, config |

```bash
git checkout -b feature/mail-list-view
# ... make changes ...
git commit -m "feat(web): add mail list component with pagination"
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific project tests
dotnet test tests/Feirb.Api.Tests

# Run with verbose output
dotnet test --verbosity normal

# Filter by test name
dotnet test --filter "MailService_GetMessages"
```

### Code Formatting

```bash
# Check formatting (CI runs this)
dotnet format --verify-no-changes

# Auto-fix formatting
dotnet format
```

## Ollama Setup

Aspire manages the Ollama container and downloads the development model (`qwen3:0.6b`, ~400MB) automatically on first start. The container bind-mounts `.ollama-data/` (git-ignored) at the repository root, so models persist across restarts and container rebuilds. Copy that directory to skip the download on another machine.

Production targets the larger `qwen3:4b` model (~2.6GB), which is configurable via Aspire/appsettings.

## GreenMail (Dev Mail Server)

GreenMail provides SMTP, IMAP, and a REST API in a single container for development. No real emails are sent.

- **SMTP** at localhost:3025 — system and per-mailbox outgoing mail
- **IMAP** at localhost:3143 — mail fetching for inbox sync
- **REST API / OpenAPI UI** at http://localhost:8080 — inspect and manage test emails
- **Preloaded mail** — 30 .eml files (10 per account) are mounted from `seeding/mails/` and loaded on startup
- **Test accounts:** admin@feirb.local, alice@feirb.local, bob@feirb.local (bob has mail but no Feirb account)

## Configuration

### Application Settings

Settings are managed via `appsettings.json` and `appsettings.Development.json` in each project. Aspire injects service URLs and connection strings automatically.

### Sensitive Values

Use .NET User Secrets for local sensitive configuration:

```bash
cd src/Feirb.Api
dotnet user-secrets init
dotnet user-secrets set "Mail:TestPassword" "your-test-password"
```

Never commit passwords or secrets to the repository.

## Troubleshooting

### Docker not running

Aspire requires a container runtime for PostgreSQL, Ollama and GreenMail. Ensure Docker (or Podman) is running:

```bash
docker info      # or: podman info
```

With Podman, make sure `ASPIRE_CONTAINER_RUNTIME=podman` is exported in the shell that starts Aspire. With Docker on Linux, your user must be in the `docker` group (log out and back in after `sudo usermod -aG docker "$USER"`).

### `aspire`, `dotnet` or `npx` not found

Open a new terminal after installing; the installers only update `PATH` for new shells. For the .NET install script, `DOTNET_ROOT` and `PATH` must be exported (see [Installing the Prerequisites](#2-net-10-sdk)). Missing `aspire` or `npx` only disables the corresponding Claude Code MCP servers, not the build or the dev-harness scripts.

### `DllNotFoundException` for `libSkiaSharp` / fontconfig (Linux)

Install the native dependency: `sudo apt install libfontconfig1`.

### Port conflicts

If default ports are in use, check which process occupies them:

```bash
# Linux
ss -tlnp | grep -E '(15432|7272|5263|3025|3143|8080|11434|18888)'
```

Stop conflicting processes or modify `launchSettings.json` in the respective projects.

### Ollama model not found

If the `qwen3:0.6b` development model fails to download automatically:

```bash
# Check Ollama container logs via Aspire dashboard
# Or manually pull:
docker exec -it <ollama-container-name> ollama pull qwen3:0.6b
```

### Reset PostgreSQL Database

The PostgreSQL container and its data volume are managed by Aspire. To reset the database, stop the application and remove containers and volumes, then start again — EF Core migrations recreate the schema:

```bash
.claude/skills/dev-harness/cleanup.sh
.claude/skills/dev-harness/start.sh --seeding   # or: dotnet run --project src/Feirb.AppHost
```

> **One-time reset required (post #266):** The accumulated 28 EF Core migrations were squashed into a single `InitialSchema` migration on 2026-05-14. If your local dev DB was created before this change, the `__EFMigrationsHistory` table still references the old migration names and EF will refuse to start. Run the dev-harness cleanup script to wipe the volume, then start with seeding to recreate everything from scratch:
>
> ```bash
> .claude/skills/dev-harness/cleanup.sh
> .claude/skills/dev-harness/start.sh --seeding
> ```
>
> No production deployments exist yet, so this is a development-only concern.

### Clean Build

```bash
dotnet clean Feirb.sln
dotnet build Feirb.sln
```
