FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
# Chỉ chép csproj và lock file trước để tận dụng cache của bước restore.
COPY src/backend/BuildingBlocks/ServiceDesk.SharedKernel/ServiceDesk.SharedKernel.csproj src/backend/BuildingBlocks/ServiceDesk.SharedKernel/packages.lock.json src/backend/BuildingBlocks/ServiceDesk.SharedKernel/
COPY src/backend/BuildingBlocks/ServiceDesk.SharedKernel.Web/ServiceDesk.SharedKernel.Web.csproj src/backend/BuildingBlocks/ServiceDesk.SharedKernel.Web/packages.lock.json src/backend/BuildingBlocks/ServiceDesk.SharedKernel.Web/
COPY src/backend/Modules/AIAssistance/ServiceDesk.Modules.AIAssistance.csproj src/backend/Modules/AIAssistance/packages.lock.json src/backend/Modules/AIAssistance/
COPY src/backend/Modules/Identity/ServiceDesk.Modules.Identity.csproj src/backend/Modules/Identity/packages.lock.json src/backend/Modules/Identity/
COPY src/backend/Modules/Knowledge/ServiceDesk.Modules.Knowledge.csproj src/backend/Modules/Knowledge/packages.lock.json src/backend/Modules/Knowledge/
COPY src/backend/Modules/Ticketing/ServiceDesk.Modules.Ticketing.csproj src/backend/Modules/Ticketing/packages.lock.json src/backend/Modules/Ticketing/
COPY src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj src/backend/ServiceDesk.Api/packages.lock.json src/backend/ServiceDesk.Api/
COPY src/backend/ServiceDesk.Migrator/ServiceDesk.Migrator.csproj src/backend/ServiceDesk.Migrator/packages.lock.json src/backend/ServiceDesk.Migrator/
COPY src/backend/ServiceDesk.Platform/ServiceDesk.Platform.csproj src/backend/ServiceDesk.Platform/packages.lock.json src/backend/ServiceDesk.Platform/
COPY src/backend/ServiceDesk.Worker/ServiceDesk.Worker.csproj src/backend/ServiceDesk.Worker/packages.lock.json src/backend/ServiceDesk.Worker/
RUN dotnet restore src/backend/ServiceDesk.Migrator/ServiceDesk.Migrator.csproj --locked-mode
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/ServiceDesk.Migrator/ServiceDesk.Migrator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.8 AS runtime
WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServiceDesk.Migrator.dll"]
