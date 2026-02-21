# DevMemory MCP — Installation & Configuration Guide

## What is DevMemory?

DevMemory is a persistent memory system for AI coding agents (Claude Code, Cursor, GitHub Copilot). It remembers your architecture decisions, bug fixes, patterns, and project context across sessions — eliminating the ~30 minutes of context re-explanation at the start of every session.

---

## Quick Start (SQLite — No Database Required)

### 1. Download the binary

| Platform | File |
|----------|------|
| **Windows x64** | `devmemory-mcp-win-x64.zip` |
| **macOS Apple Silicon** (M1/M2/M3/M4) | `devmemory-mcp-osx-arm64.zip` |
| **macOS Intel** | `devmemory-mcp-osx-x64.zip` |

### 2. Place the executable

**macOS:**
```bash
mkdir -p ~/.local/bin
unzip devmemory-mcp-osx-arm64.zip -d ~/.local/bin
chmod +x ~/.local/bin/devmemory-mcp
```

**Windows:** Extract to:
```
C:\Users\<YOU>\AppData\Local\devmemory\devmemory-mcp.exe
```

### 3. Configure your AI agent

The sections below cover every agent and storage backend combination.

---

## Storage Backends

DevMemory supports three storage backends. The default is **SQLite** (zero setup).

| Backend | Best For | Setup Required |
|---------|----------|----------------|
| **SQLite** | Local/solo use, zero config | None — file at `~/.devmemory/devmemory.db` |
| **PostgreSQL** | Teams, shared memory, advanced search | PostgreSQL server + schema migration |
| **Neo4j** | Graph relationships, decision chains | Neo4j 5.x instance |

---

## MCP Configuration

### Claude Code

Claude Code reads MCP server configuration from `~/.claude/mcp.json` (global) or `.mcp.json` in your project root (project-scoped).

#### SQLite (default)

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

SQLite data is stored at `~/.devmemory/devmemory.db` automatically.

#### PostgreSQL

```json
{
  "mcpServers": {
    "devmemory": {
      "type": "stdio",
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp",
      "env": {
        "DEVMEMORY_STORAGE": "PostgreSQL",
        "DEVMEMORY_POSTGRES_CONNECTION": "Host=your-server;Database=devmemory;Username=user;Password=pass"
      }
    }
  }
}
```

#### Neo4j

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

---

### VS Code (GitHub Copilot / Claude extension)

**Config file location:**
- **macOS:** `~/Library/Application Support/Code/User/mcp.json`
- **Windows:** `%APPDATA%\Code\User\mcp.json`

#### SQLite (default)

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

**Windows:**
```json
{
  "servers": {
    "devmemory": {
      "type": "stdio",
      "command": "C:\\Users\\<YOU>\\AppData\\Local\\devmemory\\devmemory-mcp.exe"
    }
  }
}
```

#### PostgreSQL

```json
{
  "servers": {
    "devmemory": {
      "type": "stdio",
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp",
      "env": {
        "DEVMEMORY_STORAGE": "PostgreSQL",
        "DEVMEMORY_POSTGRES_CONNECTION": "Host=your-server;Database=devmemory;Username=user;Password=pass"
      }
    }
  }
}
```

#### Neo4j

```json
{
  "servers": {
    "devmemory": {
      "type": "stdio",
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp",
      "env": {
        "DEVMEMORY_STORAGE": "Neo4j",
        "DEVMEMORY_NEO4J_URI": "bolt://192.168.1.140:7687",
        "DEVMEMORY_NEO4J_USER": "neo4j",
        "DEVMEMORY_NEO4J_PASSWORD": "your-password",
        "DEVMEMORY_NEO4J_DATABASE": "devmemory"
      }
    }
  }
}
```

---

### Cursor

Cursor MCP configuration lives in `~/.cursor/mcp.json` (global) or `.cursor/mcp.json` (project root).

#### SQLite (default)

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

#### PostgreSQL

```json
{
  "mcpServers": {
    "devmemory": {
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp",
      "args": [],
      "env": {
        "DEVMEMORY_STORAGE": "PostgreSQL",
        "DEVMEMORY_POSTGRES_CONNECTION": "Host=your-server;Database=devmemory;Username=user;Password=pass"
      }
    }
  }
}
```

#### Neo4j

```json
{
  "mcpServers": {
    "devmemory": {
      "command": "/Users/<YOU>/.local/bin/devmemory-mcp",
      "args": [],
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

---

## Environment Variables Reference

| Variable | Default | Description |
|----------|---------|-------------|
| `DEVMEMORY_STORAGE` | `SQLite` | Storage backend: `SQLite`, `PostgreSQL`, or `Neo4j` |
| `DEVMEMORY_SQLITE_PATH` | `~/.devmemory/devmemory.db` | SQLite database file path |
| `DEVMEMORY_POSTGRES_CONNECTION` | _(none)_ | Full PostgreSQL connection string |
| `DEVMEMORY_NEO4J_URI` | `bolt://localhost:7687` | Neo4j Bolt URI |
| `DEVMEMORY_NEO4J_USER` | `neo4j` | Neo4j username |
| `DEVMEMORY_NEO4J_PASSWORD` | _(none)_ | Neo4j password (required) |
| `DEVMEMORY_NEO4J_DATABASE` | `neo4j` | Neo4j database name |

---

## PostgreSQL Setup

### 1. Create database and user

```sql
CREATE DATABASE devmemory;
CREATE USER devmemory_user WITH PASSWORD 'your-password';
GRANT ALL PRIVILEGES ON DATABASE devmemory TO devmemory_user;
```

### 2. Run migrations

The DevMemory CLI handles schema migrations automatically on first run:

```bash
DEVMEMORY_STORAGE=PostgreSQL \
DEVMEMORY_POSTGRES_CONNECTION="Host=localhost;Database=devmemory;Username=devmemory_user;Password=your-password" \
devmemory serve
```

Or trigger them manually via:

```bash
devmemory stats  # any command will run migrations on startup
```

### 3. Connection string format

```
Host=localhost;Port=5432;Database=devmemory;Username=devmemory_user;Password=your-password
```

For SSL/TLS:
```
Host=your-host;Database=devmemory;Username=user;Password=pass;SSL Mode=Require;Trust Server Certificate=true
```

---

## Neo4j Setup

### 1. Install Neo4j

**Docker (recommended for local):**
```bash
docker run \
  --name devmemory-neo4j \
  -p 7474:7474 -p 7687:7687 \
  -e NEO4J_AUTH=neo4j/your-password \
  -e NEO4J_PLUGINS='["apoc"]' \
  -v $HOME/.devmemory/neo4j:/data \
  neo4j:5
```

**Neo4j Desktop:** Download from [neo4j.com/download](https://neo4j.com/download/) and create a local database.

**Neo4j AuraDB (cloud):** Use `neo4j+s://` URI scheme:
```
neo4j+s://xxxxxxxx.databases.neo4j.io
```

### 2. Schema initialization

DevMemory **automatically** creates all required constraints, indexes, and the full-text search index on first startup. No manual schema setup required.

Created automatically:
```cypher
-- Constraints
CREATE CONSTRAINT observation_id FOR (o:Observation) REQUIRE o.id IS UNIQUE
CREATE CONSTRAINT session_id     FOR (s:Session)     REQUIRE s.id IS UNIQUE
CREATE CONSTRAINT tag_name       FOR (t:Tag)         REQUIRE t.name IS UNIQUE
CREATE CONSTRAINT project_name   FOR (p:Project)     REQUIRE p.name IS UNIQUE

-- Indexes
CREATE INDEX observation_project FOR (o:Observation) ON (o.project)
CREATE INDEX observation_type    FOR (o:Observation) ON (o.type)
CREATE INDEX session_project     FOR (s:Session)     ON (s.project)

-- Full-text search
CREATE FULLTEXT INDEX observation_search FOR (o:Observation) ON EACH [o.title, o.content]
```

### 3. Verify connection

```bash
DEVMEMORY_STORAGE=Neo4j \
DEVMEMORY_NEO4J_URI=bolt://localhost:7687 \
DEVMEMORY_NEO4J_USER=neo4j \
DEVMEMORY_NEO4J_PASSWORD=your-password \
devmemory-mcp
# Should print: [devmemory] Using Neo4j: bolt://localhost:7687/neo4j
```

---

## Available MCP Tools

### Core Tools (all backends)

| Tool | Description |
|------|-------------|
| `mem_save` | Save a structured observation (decision, bugfix, pattern, etc.) |
| `mem_search` | Full-text search across all memories |
| `mem_context` | Load recent session history for a project |
| `mem_get_observation` | Retrieve the full content of an observation by ID |
| `mem_timeline` | Get chronological context around an observation |
| `mem_session_start` | Start a new coding session |
| `mem_session_end` | End the current session with a summary |
| `mem_session_summary` | Update the current session summary |
| `mem_stats` | Memory statistics (total observations, sessions, projects) |
| `mem_save_prompt` | Save a user prompt to the current session |

### Graph Tools (Neo4j only)

These tools are only registered when `DEVMEMORY_STORAGE=Neo4j`.

| Tool | Description |
|------|-------------|
| `mem_link` | Create a typed relationship between two observations |
| `mem_related` | Find observations transitively related to a given one |
| `mem_decision_chain` | Trace all observations that led to a decision |

#### `mem_link` example

```json
{
  "name": "mem_link",
  "arguments": {
    "fromId": "8a7f3c12-...",
    "toId": "b2e9f1a0-...",
    "reason": "This bugfix influenced the architecture decision",
    "strength": 0.9
  }
}
```

#### `mem_related` example

```json
{
  "name": "mem_related",
  "arguments": {
    "id": "8a7f3c12-...",
    "depth": 2
  }
}
```

#### `mem_decision_chain` example

```json
{
  "name": "mem_decision_chain",
  "arguments": {
    "id": "8a7f3c12-..."
  }
}
```

---

## Observation Types

Use these values for the `type` field in `mem_save`:

| Type | When to use |
|------|------------|
| `bugfix` | A bug was found and fixed |
| `architecture-decision` | A design or architecture choice was made |
| `security-pattern` | A security-related pattern or constraint |
| `performance-fix` | A performance optimization |
| `multi-tenant-rule` | Multi-tenancy isolation or scoping rule |
| `stored-procedure-pattern` | Database stored procedure usage pattern |
| `config-change` | Configuration or environment change |
| `pattern` | A reusable code pattern |
| `discovery` | Something discovered about the codebase or system |
| `preference` | A user or team preference |

---

## Recommended Agent Instructions

Add to your `CLAUDE.md`, `copilot-instructions.md`, or system prompt:

```markdown
## Memory

You have access to DevMemory persistent memory via MCP tools.

### On session start
- Call `mem_context` with the current project name to load recent history.
- Review returned observations before writing new code.

### During work
- After any significant decision, call `mem_save` with type `architecture-decision`.
- After fixing a bug, call `mem_save` with type `bugfix`.
- Save proactively — don't wait to be asked.

### On session end
- Call `mem_session_end` with a 2–3 sentence summary of what was accomplished.

### After context reset or compaction
- Immediately call `mem_context` to recover session state.
```

---

## Troubleshooting

### "Command not found" on macOS

```bash
chmod +x ~/.local/bin/devmemory-mcp
# If blocked by Gatekeeper:
xattr -d com.apple.quarantine ~/.local/bin/devmemory-mcp
```

### Test MCP connection manually

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | devmemory-mcp
```

Expected response:
```json
{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{"tools":{"listChanged":false}},"serverInfo":{"name":"devmemory","version":"1.0.0"}}}
```

### List available tools

```bash
echo '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' | devmemory-mcp
```

### Neo4j: "Unable to connect" error

1. Verify Neo4j is running: `docker ps` or check Neo4j Desktop
2. Check Bolt port is reachable: `nc -zv localhost 7687`
3. Confirm credentials in Neo4j Browser at `http://localhost:7474`
4. For remote Neo4j, check firewall rules on port 7687

### PostgreSQL: "Connection refused" error

1. Check PostgreSQL is running: `pg_isready -h localhost`
2. Verify the connection string has the correct host, port, and credentials
3. Ensure the `devmemory` database exists and the user has access

---

## Data Location

| Backend | Location |
|---------|----------|
| SQLite | `~/.devmemory/devmemory.db` |
| PostgreSQL | Your configured database server |
| Neo4j | Your configured Neo4j instance |

**Backup SQLite:** Copy `~/.devmemory/devmemory.db` to a safe location.

**Export all memories to JSON:**
```bash
devmemory export --format json --output memories.json
```
