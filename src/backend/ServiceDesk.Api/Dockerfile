FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj src/backend/ServiceDesk.Api/packages.lock.json src/backend/ServiceDesk.Api/
COPY src/backend/ServiceDesk.Application/ServiceDesk.Application.csproj src/backend/ServiceDesk.Application/packages.lock.json src/backend/ServiceDesk.Application/
COPY src/backend/ServiceDesk.Database/ServiceDesk.Database.csproj src/backend/ServiceDesk.Database/packages.lock.json src/backend/ServiceDesk.Database/
COPY src/backend/ServiceDesk.Domain/ServiceDesk.Domain.csproj src/backend/ServiceDesk.Domain/packages.lock.json src/backend/ServiceDesk.Domain/
COPY src/backend/ServiceDesk.Infrastructure/ServiceDesk.Infrastructure.csproj src/backend/ServiceDesk.Infrastructure/packages.lock.json src/backend/ServiceDesk.Infrastructure/
COPY src/backend/ServiceDesk.Worker/ServiceDesk.Worker.csproj src/backend/ServiceDesk.Worker/packages.lock.json src/backend/ServiceDesk.Worker/
RUN dotnet restore src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj --locked-mode
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.8 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
HEALTHCHECK --interval=5s --timeout=3s --start-period=20s --retries=12 CMD curl --fail --silent http://localhost:8080/health/live || exit 1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServiceDesk.Api.dll"]
