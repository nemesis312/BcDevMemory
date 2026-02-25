# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first so dotnet restore is cached independently of source changes
COPY src/DevMemory.Core/DevMemory.Core.csproj                               src/DevMemory.Core/
COPY src/DevMemory.Infrastructure/DevMemory.Infrastructure.csproj           src/DevMemory.Infrastructure/
COPY src/DevMemory.Infrastructure.Neo4j/DevMemory.Infrastructure.Neo4j.csproj src/DevMemory.Infrastructure.Neo4j/
COPY src/DevMemory.Mcp/DevMemory.Mcp.csproj                                 src/DevMemory.Mcp/

RUN dotnet restore src/DevMemory.Mcp/DevMemory.Mcp.csproj

# Copy full source and publish
COPY src/DevMemory.Core/                    src/DevMemory.Core/
COPY src/DevMemory.Infrastructure/          src/DevMemory.Infrastructure/
COPY src/DevMemory.Infrastructure.Neo4j/    src/DevMemory.Infrastructure.Neo4j/
COPY src/DevMemory.Mcp/                     src/DevMemory.Mcp/

RUN dotnet publish src/DevMemory.Mcp/DevMemory.Mcp.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# libgssapi-krb5-2 is required by Npgsql for GSSAPI/Kerberos support.
# Without it the driver logs a warning and may fail in some auth scenarios.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Defaults — override via environment variables or docker-compose
ENV DEVMEMORY_TRANSPORT=http
ENV DEVMEMORY_PORT=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "devmemory-mcp.dll"]
