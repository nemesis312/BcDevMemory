# DevMemory — Persistent Memory for AI Coding Agents

> **Give your AI a brain that survives context resets.**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#installation)
[![MCP](https://img.shields.io/badge/protocol-MCP-blueviolet)](https://modelcontextprotocol.io)
[![.NET](https://img.shields.io/badge/.NET-10-purple)](https://dotnet.microsoft.com)

---

## The Problem

Every time you start a new session with Claude Code, Cursor, or Copilot, your AI agent starts **completely fresh**. It doesn't remember:

- Why you chose that architecture
- That bug you spent 4 hours debugging last week
- Your team's security rules and patterns
- The decisions you made that shaped the whole codebase

You end up re-explaining the same context over and over — sometimes spending **30+ minutes** just getting your AI back up to speed.

**DevMemory fixes this.**

---

## What is DevMemory?

DevMemory is a **Model Context Protocol (MCP) server** that gives your AI coding agent persistent, searchable memory across sessions, projects, and even across your entire team.

It runs as a lightweight local binary. No cloud. No subscriptions. No data sent anywhere you don't control.

```
Your AI Agent  ←→  DevMemory MCP  ←→  SQLite / PostgreSQL / Neo4j
```

The AI saves what it learns. The next session, it retrieves it. That's it.

---

## Why DevMemory?

### Your AI gets smarter over time

After each session, DevMemory stores structured observations — bug fixes, architecture decisions, patterns, discoveries. The next time your AI starts, it loads that context and **already knows your codebase**.

### No more repeating yourself

Stop explaining your multi-tenant rules, your security constraints, your stored procedure patterns — session after session. Save them once, recalled forever.

### It's not a cloud service — it's yours

Your memories live in a local SQLite file (`~/.devmemory/devmemory.db`) or in your own PostgreSQL/Neo4j instance. No data leaves your machine unless you explicitly configure a remote database.

### Teams share knowledge

Connect your team to a shared PostgreSQL or Neo4j instance and every developer's AI agent benefits from what every other developer has learned. Onboard new devs in minutes, not weeks.

### Privacy is built-in

Wrap sensitive content in `<private>...</private>` tags. DevMemory **automatically strips** them before anything is written to the database. Your secrets stay secret.

```
content: "Use <private>API_KEY=sk-1234</private> for auth. Pattern: Bearer token in header."
# Stored as: "Use [REDACTED] for auth. Pattern: Bearer token in header."
```

### Zero setup for solo use

Download the binary, add 5 lines of JSON to your MCP config, done. SQLite requires no database server. Your first memory is one command away.

---

## Is it safe? Is it open source?

**Yes and yes.**

- **MIT licensed** — use it commercially, fork it, build on it, embed it. Free forever.
- **Auditable** — the full source is here. Read every line. It's plain C#.
- **No telemetry, no analytics, no phone-home** — the binary never makes outbound network calls unless you configure a remote database yourself.
- **Offline-first** — works entirely on your machine with SQLite.

---

## Features

| Feature                  | Details                                                              |
| ------------------------ | -------------------------------------------------------------------- |
| **MCP-native**           | Implements MCP via **stdio** (default) and **HTTP + SSE** transports |
| **3 storage backends**   | SQLite (local), PostgreSQL (teams), Neo4j (graph)                    |
| **Session management**   | Start/end sessions, persist summaries, survive context compaction    |
| **Full-text search**     | Search across all memories with `mem_search`                         |
| **13 observation types** | `bugfix`, `architecture-decision`, `pattern`, `discovery`, and more  |
| **Graph relationships**  | Link observations, trace decision chains (Neo4j)                     |
| **Privacy protection**   | `<private>` tag stripping at the repository layer                    |
| **Cross-platform**       | Native binaries for Windows x64, macOS ARM64/x64, Linux x64/ARM64    |
| **No runtime required**  | Single self-contained executable                                     |
| **CLI included**         | Export, import, search, stats — all from the terminal                |

---

## MCP Tools Reference

| Tool                           | What it does                                                  |
| ------------------------------ | ------------------------------------------------------------- |
| `mem_save`                     | Save a structured observation with type, title, content, tags |
| `mem_search`                   | Full-text search across all saved memories                    |
| `mem_context`                  | Load recent session history for a project                     |
| `mem_get_observation`          | Retrieve a specific observation by ID                         |
| `mem_timeline`                 | Get chronological context around an observation               |
| `mem_session_start`            | Explicitly start a new coding session                         |
| `mem_session_end`              | End the session with a summary                                |
| `mem_session_summary`          | Update the current session summary mid-session                |
| `mem_stats`                    | Total observations, sessions, projects at a glance            |
| `mem_save_prompt`              | Save a user prompt to the current session                     |
| `mem_link` _(Neo4j)_           | Create a typed relationship between two observations          |
| `mem_related` _(Neo4j)_        | Find observations transitively related to a given one         |
| `mem_decision_chain` _(Neo4j)_ | Trace the chain of decisions that led to a conclusion         |

---

## Installation

### 1. Download the binary

Go to the [Releases page](../../releases) and download the binary for your platform:

| Platform                      | File                            |
| ----------------------------- | ------------------------------- |
| Windows x64                   | `devmemory-mcp-win-x64.zip`     |
| macOS Apple Silicon (M1–M4)   | `devmemory-mcp-osx-arm64.zip`   |
| macOS Intel                   | `devmemory-mcp-osx-x64.zip`     |
| Linux x64                     | `devmemory-mcp-linux-x64.zip`   |
| Linux ARM64 (Graviton, Pi 4+) | `devmemory-mcp-linux-arm64.zip` |

### 2. Extract and place the binary

**macOS:**

```bash
mkdir -p ~/.local/bin
unzip devmemory-mcp-osx-arm64.zip -d ~/.local/bin
chmod +x ~/.local/bin/devmemory-mcp

# If macOS Gatekeeper blocks the binary:
xattr -d com.apple.quarantine ~/.local/bin/devmemory-mcp
```

**Linux:**

```bash
mkdir -p ~/.local/bin
unzip devmemory-mcp-linux-x64.zip -d ~/.local/bin
chmod +x ~/.local/bin/devmemory-mcp
```

**Windows:** Extract to:

```
C:\Users\<YOU>\AppData\Local\devmemory\devmemory-mcp.exe
```

### 3. Configure your agent

Pick your agent and add the config below. That's it.

---

## Configuration

### Claude Code

Edit `~/.claude/mcp.json` (global) or `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "devmemory": {
      "type": "stdio",
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp"
    }
  }
}
```

> On Windows, use `C:\\Users\\<YOU>\\AppData\\Local\\devmemory\\devmemory-mcp.exe`

### Cursor

Edit `~/.cursor/mcp.json` or `.cursor/mcp.json` in your project:

```json
{
  "mcpServers": {
    "devmemory": {
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp",
      "args": []
    }
  }
}
```

### VS Code (GitHub Copilot / Claude extension)

- **macOS:** `~/Library/Application Support/Code/User/mcp.json`
- **Windows:** `%APPDATA%\Code\User\mcp.json`

```json
{
  "servers": {
    "devmemory": {
      "type": "stdio",
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp"
    }
  }
}
```

> SQLite data is stored automatically at `~/.devmemory/devmemory.db` — no database setup needed.

---

## Team Setup with PostgreSQL

Share memory across your entire team by pointing everyone at the same PostgreSQL database.

### 1. Create the database

```sql
CREATE DATABASE devmemory;
CREATE USER devmemory_user WITH PASSWORD 'your-password';
GRANT ALL PRIVILEGES ON DATABASE devmemory TO devmemory_user;
```

### 2. Configure the MCP server

```json
{
  "mcpServers": {
    "devmemory": {
      "type": "stdio",
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp",
      "env": {
        "DEVMEMORY_STORAGE": "PostgreSQL",
        "DEVMEMORY_POSTGRES_CONNECTION": "Host=your-server;Database=devmemory;Username=devmemory_user;Password=your-password"
      }
    }
  }
}
```

Schema migrations run automatically on first startup. No manual setup required.

---

## Graph Memory with Neo4j

For teams that want to understand _how_ decisions relate to each other — not just search through them — Neo4j gives you a full knowledge graph.

### Quick start with Docker

```bash
docker run \
  --name devmemory-neo4j \
  -p 7474:7474 -p 7687:7687 \
  -e NEO4J_AUTH=neo4j/your-password \
  -v $HOME/.devmemory/neo4j:/data \
  neo4j:5
```

### Configure

```json
{
  "mcpServers": {
    "devmemory": {
      "type": "stdio",
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp",
      "env": {
        "DEVMEMORY_STORAGE": "Neo4j",
        "DEVMEMORY_NEO4J_URI": "bolt://localhost:7687",
        "DEVMEMORY_NEO4J_USER": "neo4j",
        "DEVMEMORY_NEO4J_PASSWORD": "your-password",
        "DEVMEMORY_NEO4J_DATABASE": "devmemory"
      }
    }
  }
}
```

DevMemory automatically creates all constraints, indexes, and full-text search indexes on first startup.

### Graph-exclusive tools

**Link two observations:**

```json
{
  "fromId": "8a7f3c12-...",
  "toId": "b2e9f1a0-...",
  "reason": "This bugfix influenced the architecture decision",
  "strength": 0.9
}
```

**Trace a decision chain:**

```json
{ "id": "8a7f3c12-..." }
```

Returns every observation that contributed to a final decision — a full audit trail of your reasoning.

---

## HTTP + SSE Transport

By default DevMemory communicates over **stdio**, which is what most desktop agent configs (Claude Code, Cursor, VS Code) expect. If you need to expose the server over the network — for example to run it as a shared daemon, inside Docker, or to support clients that use the newer Streamable HTTP spec — set `DEVMEMORY_TRANSPORT=http`.

### Protocol flows

**Legacy SSE** (MCP spec 2024-11-05, supported by most clients):

1. Client opens `GET /sse` → server sends an `endpoint` event with a session-specific POST URL
2. Client sends JSON-RPC requests to `POST /message?sessionId=<id>` → server responds `202`
3. Server pushes each JSON-RPC response back through the open SSE stream

**Streamable HTTP** (newer clients):

1. Client sends `POST /sse` with a JSON-RPC request → server streams the response back directly

A heartbeat ping is sent every 30 seconds to keep connections alive through proxies and load balancers.

### Starting in HTTP mode

```bash
# Stdio (default — same as omitting the variable)
DEVMEMORY_TRANSPORT=stdio ./devmemory-mcp

# HTTP + SSE on the default port 8080
DEVMEMORY_TRANSPORT=http ./devmemory-mcp

# HTTP + SSE on a custom port
DEVMEMORY_TRANSPORT=http DEVMEMORY_PORT=3100 ./devmemory-mcp
```

### Configuring agents for HTTP + SSE

**Claude Code (`~/.claude/mcp.json`):**

```json
{
  "mcpServers": {
    "devmemory": {
      "type": "sse",
      "url": "http://localhost:8080/sse"
    }
  }
}
```

**VS Code (`mcp.json`):**

```json
{
  "servers": {
    "devmemory": {
      "type": "sse",
      "url": "http://localhost:8080/sse"
    }
  }
}
```

**Docker example** (also set your storage env vars as needed):

```bash
docker run -d \
  --name devmemory \
  -p 8080:8080 \
  -e DEVMEMORY_TRANSPORT=http \
  -e DEVMEMORY_STORAGE=SQLite \
  -v $HOME/.devmemory:/root/.devmemory \
  devmemory-mcp
```

> When running in HTTP mode the process does **not** read from stdin — it binds to `0.0.0.0:<port>` and serves requests over HTTP. Keep the process running; agents connect and disconnect as needed.

---

## Environment Variables Reference

| Variable                        | Default                     | Description                                           |
| ------------------------------- | --------------------------- | ----------------------------------------------------- |
| `DEVMEMORY_STORAGE`             | `SQLite`                    | Backend: `SQLite`, `PostgreSQL`, or `Neo4j`           |
| `DEVMEMORY_SQLITE_PATH`         | `~/.devmemory/devmemory.db` | SQLite file path                                      |
| `DEVMEMORY_POSTGRES_CONNECTION` | _(none)_                    | Full PostgreSQL connection string                     |
| `DEVMEMORY_NEO4J_URI`           | `bolt://localhost:7687`     | Neo4j Bolt URI                                        |
| `DEVMEMORY_NEO4J_USER`          | `neo4j`                     | Neo4j username                                        |
| `DEVMEMORY_NEO4J_PASSWORD`      | _(none)_                    | Neo4j password                                        |
| `DEVMEMORY_NEO4J_DATABASE`      | `neo4j`                     | Neo4j database name                                   |
| `DEVMEMORY_SYNC_PATH`           | `~/.devmemory-sync`         | Global Git-backed memory sync repository path         |
| `DEVMEMORY_TRANSPORT`           | `stdio`                     | Transport mode: `stdio` or `http` (HTTP + SSE)        |
| `DEVMEMORY_PORT`                | `8080`                      | HTTP port (only used when `DEVMEMORY_TRANSPORT=http`) |

---

## Teach Your Agent to Use DevMemory

Add this to your `CLAUDE.md`, `copilot-instructions.md`, or agent system prompt:

```markdown
## Memory

You have access to DevMemory persistent memory via MCP tools.

### Session start

- Call `mem_context` with the current project name to load recent history.
- Review returned observations before writing any code.

### During work

- After any significant decision, call `mem_save` with type `architecture-decision`.
- After fixing a bug, call `mem_save` with type `bugfix`.
- Save proactively — don't wait to be asked.

### Session end

- Call `mem_session_end` with a 2–3 sentence summary of what was accomplished.

### After context reset or compaction

- Immediately call `mem_context` to recover session state.
```

---

## Observation Types

Structure your knowledge with the right type:

| Type                       | When to use                            |
| -------------------------- | -------------------------------------- |
| `bugfix`                   | A bug was found and fixed              |
| `architecture-decision`    | A design or structural choice was made |
| `security-pattern`         | A security constraint or pattern       |
| `performance-fix`          | A performance optimization             |
| `multi-tenant-rule`        | Tenancy isolation or scoping rules     |
| `stored-procedure-pattern` | Database stored procedure patterns     |
| `config-change`            | Configuration or environment changes   |
| `pattern`                  | A reusable code pattern                |
| `discovery`                | Something learned about the codebase   |
| `preference`               | A user or team preference              |
| `idea`                     | A future improvement or exploration    |
| `tech-debt`                | Known technical debt                   |
| `new-feature`              | A new feature added                    |

---

## CLI Usage

DevMemory also ships with a CLI for managing your memory outside of an agent session:

```bash
# Search memories
devmemory-mcp search "authentication middleware"

# View statistics
devmemory-mcp stats

# Export all memories to JSON
devmemory-mcp export --format json --output memories.json

# Import from a JSON export
devmemory-mcp import --file memories.json

# Export latest SQLite memory to sync chunks
devmemory-mcp sync --sync-path ~/devmemory-sync

# Import new chunks from sync repository
devmemory-mcp sync --import --sync-path ~/devmemory-sync

# Show sync repository status
devmemory-mcp sync --status --sync-path ~/devmemory-sync
```

---

## Troubleshooting

### "Command not found" on macOS

```bash
chmod +x ~/.local/bin/devmemory-mcp
xattr -d com.apple.quarantine ~/.local/bin/devmemory-mcp
```

### Test the MCP connection manually (stdio mode)

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | ~/.local/bin/devmemory-mcp
```

Expected response:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2024-11-05",
    "capabilities": { "tools": { "listChanged": false } },
    "serverInfo": { "name": "devmemory", "version": "1.0.0" }
  }
}
```

### Test the MCP connection manually (HTTP + SSE mode)

Start the server with `DEVMEMORY_TRANSPORT=http`, then in a separate terminal:

```bash
# 1. Open SSE stream (keep this running in a terminal tab):
curl -N http://localhost:8080/sse
# Server responds: event: endpoint\ndata: /message?sessionId=<id>

# 2. Send an initialize request using the sessionId from step 1:
curl -X POST "http://localhost:8080/message?sessionId=<id>" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'
# Response arrives on the SSE stream in step 1
```

### List available tools

```bash
echo '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' | ~/.local/bin/devmemory-mcp
```

### Backup your SQLite database

```bash
cp ~/.devmemory/devmemory.db ~/.devmemory/devmemory.db.bak
```

---

## Building from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/nemesis312/devmemory.git
cd devmemory
dotnet build src/DevMemory.sln
```

To publish a self-contained native binary:

```bash
dotnet publish src/DevMemory.Mcp/DevMemory.Mcp.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true
```

---

## Contributing

Issues, ideas, and PRs are welcome. This project follows standard GitHub flow:

1. Fork the repo
2. Create a branch (`git checkout -b feat/your-feature`)
3. Commit your changes
4. Open a pull request

---

## License

[MIT](LICENSE) — free for personal and commercial use.

---

_Built for developers who are tired of explaining their own codebase to their AI agent._
