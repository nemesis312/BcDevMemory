# Building the MCP Executable

Steps to publish `devmemory-mcp` as a self-contained single-file binary.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- `zip` (macOS/Linux — usually pre-installed)
- Run all commands from the **repository root**

Verify your SDK:

```bash
dotnet --version
# Expected: 10.x.x
```

---

## Option A — Automated (all platforms at once)

```bash
chmod +x scripts/publish-all.sh
./scripts/publish-all.sh
```

Outputs to `publish/`:

```
publish/
├── win-x64/devmemory-mcp.exe
├── osx-arm64/devmemory-mcp
├── osx-x64/devmemory-mcp
├── devmemory-mcp-win-x64.zip
├── devmemory-mcp-osx-arm64.zip
└── devmemory-mcp-osx-x64.zip
```

---

## Option B — Manual (single platform)

### Windows x64

```bash
dotnet publish src/DevMemory.Mcp -c Release -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o publish/win-x64
```

Output: `publish/win-x64/devmemory-mcp.exe`

### macOS Apple Silicon (M1/M2/M3/M4)

```bash
dotnet publish src/DevMemory.Mcp -c Release -r osx-arm64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o publish/osx-arm64
```

Output: `publish/osx-arm64/devmemory-mcp`

### macOS Intel

```bash
dotnet publish src/DevMemory.Mcp -c Release -r osx-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o publish/osx-x64
```

Output: `publish/osx-x64/devmemory-mcp`

---

## Publish flags explained

| Flag | Effect |
|------|--------|
| `-c Release` | Optimized build (no debug symbols) |
| `-r <rid>` | Target runtime identifier |
| `--self-contained true` | Bundles the .NET runtime — no SDK needed on target machine |
| `PublishSingleFile=true` | Merges all assemblies into one file |
| `EnableCompressionInSingleFile=true` | Reduces binary size (~30–40%) |
| `IncludeNativeLibrariesForSelfExtract=true` | Bundles native libs (Npgsql, Neo4j driver) |

---

## Verify the build

```bash
# macOS/Linux — test MCP handshake
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' \
  | publish/osx-arm64/devmemory-mcp

# Windows (PowerShell)
'{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' \
  | publish\win-x64\devmemory-mcp.exe
```

Expected response:

```json
{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{"tools":{"listChanged":false}},"serverInfo":{"name":"devmemory","version":"1.0.0"}}}
```

---

## macOS Gatekeeper

The first time you run the binary on macOS you may need to remove the quarantine flag:

```bash
xattr -d com.apple.quarantine publish/osx-arm64/devmemory-mcp
chmod +x publish/osx-arm64/devmemory-mcp
```

---

## What to ship

After building, distribute the ZIP files from `publish/`:

| File | Platform |
|------|----------|
| `devmemory-mcp-win-x64.zip` | Windows x64 |
| `devmemory-mcp-osx-arm64.zip` | macOS Apple Silicon |
| `devmemory-mcp-osx-x64.zip` | macOS Intel |

See `README-DISTRIBUTION.md` for end-user installation and MCP configuration steps.
