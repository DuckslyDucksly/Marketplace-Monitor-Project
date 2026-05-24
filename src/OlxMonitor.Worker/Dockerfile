# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Marketplace-Monitor.slnx", "./"]
COPY ["src/OlxMonitor.Worker/OlxMonitor.Worker.csproj", "src/OlxMonitor.Worker/"]
COPY ["src/OlxMonitor.Core/OlxMonitor.Core.csproj", "src/OlxMonitor.Core/"]
COPY ["src/OlxMonitor.Infrastructure/OlxMonitor.Infrastructure.csproj", "src/OlxMonitor.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/OlxMonitor.Worker/OlxMonitor.Worker.csproj"

# Copy all source code
COPY . .

# Build and publish
WORKDIR "/src/src/OlxMonitor.Worker"
RUN dotnet publish "OlxMonitor.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create data directory for SQLite database
RUN mkdir -p /app/Data

# Copy published app
COPY --from=build /app/publish .

# Set environment
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "OlxMonitor.Worker.dll"]