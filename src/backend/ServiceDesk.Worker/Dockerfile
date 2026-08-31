# ============================================
# ServiceDesk.Worker — Multi-stage Dockerfile
# ============================================

# Stage 1: Restore & Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/backend/ServiceDesk.sln src/backend/Directory.Build.props ./
COPY src/backend/ServiceDesk.Domain/ServiceDesk.Domain.csproj ServiceDesk.Domain/
COPY src/backend/ServiceDesk.Application/ServiceDesk.Application.csproj ServiceDesk.Application/
COPY src/backend/ServiceDesk.Infrastructure/ServiceDesk.Infrastructure.csproj ServiceDesk.Infrastructure/
COPY src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj ServiceDesk.Api/
COPY src/backend/ServiceDesk.Worker/ServiceDesk.Worker.csproj ServiceDesk.Worker/

RUN dotnet restore ServiceDesk.Worker/ServiceDesk.Worker.csproj

COPY src/backend/ .
RUN dotnet publish ServiceDesk.Worker/ServiceDesk.Worker.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN groupadd -r appuser && useradd -r -g appuser -s /bin/false appuser
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ServiceDesk.Worker.dll"]
