FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
# Chỉ chép csproj và lock file trước để tận dụng cache của bước restore.
COPY src/backend/BuildingBlocks/SharedKernel/ServiceDesk.SharedKernel.csproj src/backend/BuildingBlocks/SharedKernel/packages.lock.json src/backend/BuildingBlocks/SharedKernel/
COPY src/backend/BuildingBlocks/SharedKernel.Web/ServiceDesk.SharedKernel.Web.csproj src/backend/BuildingBlocks/SharedKernel.Web/packages.lock.json src/backend/BuildingBlocks/SharedKernel.Web/
COPY src/backend/Modules/AIAssistance/ServiceDesk.Modules.AIAssistance.csproj src/backend/Modules/AIAssistance/packages.lock.json src/backend/Modules/AIAssistance/
COPY src/backend/Modules/Identity/ServiceDesk.Modules.Identity.csproj src/backend/Modules/Identity/packages.lock.json src/backend/Modules/Identity/
COPY src/backend/Modules/Knowledge/ServiceDesk.Modules.Knowledge.csproj src/backend/Modules/Knowledge/packages.lock.json src/backend/Modules/Knowledge/
COPY src/backend/Modules/Ticketing/ServiceDesk.Modules.Ticketing.csproj src/backend/Modules/Ticketing/packages.lock.json src/backend/Modules/Ticketing/
COPY src/backend/Hosts/Api/ServiceDesk.Api.csproj src/backend/Hosts/Api/packages.lock.json src/backend/Hosts/Api/
COPY src/backend/Hosts/Migrator/ServiceDesk.Migrator.csproj src/backend/Hosts/Migrator/packages.lock.json src/backend/Hosts/Migrator/
COPY src/backend/Platform/ServiceDesk.Platform.csproj src/backend/Platform/packages.lock.json src/backend/Platform/
COPY src/backend/Hosts/Worker/ServiceDesk.Worker.csproj src/backend/Hosts/Worker/packages.lock.json src/backend/Hosts/Worker/
RUN dotnet restore src/backend/Hosts/Api/ServiceDesk.Api.csproj --locked-mode
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/Hosts/Api/ServiceDesk.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.8 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
HEALTHCHECK --interval=5s --timeout=3s --start-period=20s --retries=12 CMD curl --fail --silent http://localhost:8080/health/ready || exit 1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServiceDesk.Api.dll"]
