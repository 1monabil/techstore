# Build context: repository root (see docker-compose.yml, profile "full").
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/TechStore.Domain/TechStore.Domain.csproj src/TechStore.Domain/
COPY src/TechStore.Application/TechStore.Application.csproj src/TechStore.Application/
COPY src/TechStore.Infrastructure/TechStore.Infrastructure.csproj src/TechStore.Infrastructure/
COPY src/TechStore.Indexer/TechStore.Indexer.csproj src/TechStore.Indexer/
RUN dotnet restore src/TechStore.Indexer/TechStore.Indexer.csproj
COPY src/ src/
RUN dotnet publish src/TechStore.Indexer/TechStore.Indexer.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
# synonyms live in the ES container's config dir; nothing else to copy
ENTRYPOINT ["dotnet", "TechStore.Indexer.dll"]
