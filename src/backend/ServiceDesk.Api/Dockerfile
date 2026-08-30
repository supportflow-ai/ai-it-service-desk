# ============================================
# ServiceDesk.Api — Multi-stage Dockerfile
# ============================================

# Stage 1: Restore & Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files first (layer caching)
COPY src/backend/ServiceDesk.sln src/backend/Directory.Build.props ./
COPY src/backend/ServiceDesk.Domain/ServiceDesk.Domain.csproj ServiceDesk.Domain/
COPY src/backend/ServiceDesk.Application/ServiceDesk.Application.csproj ServiceDesk.Application/
COPY src/backend/ServiceDesk.Infrastructure/ServiceDesk.Infrastructure.csproj ServiceDesk.Infrastructure/
COPY src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj ServiceDesk.Api/
COPY src/backend/ServiceDesk.Worker/ServiceDesk.Worker.csproj ServiceDesk.Worker/

RUN dotnet restore ServiceDesk.Api/ServiceDesk.Api.csproj

# Copy all source and build
COPY src/backend/ .
RUN dotnet publish ServiceDesk.Api/ServiceDesk.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser -s /bin/false appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "ServiceDesk.Api.dll"]
