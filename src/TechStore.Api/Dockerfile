# Build context: repository root (see docker-compose.yml, profile "full").
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/TechStore.Domain/TechStore.Domain.csproj src/TechStore.Domain/
COPY src/TechStore.Application/TechStore.Application.csproj src/TechStore.Application/
COPY src/TechStore.Infrastructure/TechStore.Infrastructure.csproj src/TechStore.Infrastructure/
COPY src/TechStore.Api/TechStore.Api.csproj src/TechStore.Api/
RUN dotnet restore src/TechStore.Api/TechStore.Api.csproj
COPY src/ src/
RUN dotnet publish src/TechStore.Api/TechStore.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TechStore.Api.dll"]
