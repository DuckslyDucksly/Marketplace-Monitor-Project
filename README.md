# OLX Monitor

A .NET 8 background worker application that scrapes OLX.pl for new listings based on configured searches and stores them in a local SQLite database. Includes both console and WPF GUI viewers for browsing saved listings.

## Features

- 🔍 **Configurable searches** via `appsettings.json`
- ➕ **Easy search management**: `dotnet run -- add-url <full-olx-url>`
- 🖥️ **Console viewer**: Browse listings from the terminal
- 🪟 **WPF GUI viewer**: Desktop application with clickable listing URLs
- 🗄️ **SQLite storage** for all scraped listings
- 🧪 **Unit tests** for scraper and database operations

## Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### 1. Add a Search
```powershell
dotnet run --project src/OlxMonitor.Worker -- add-url https://www.olx.pl/elektronika/gry-konsole/q-xbox-360/
```

### 2. Run the Monitor
```powershell
dotnet run --project src/OlxMonitor.Worker
```

### 3. View Saved Listings

**Console Viewer:**
```powershell
dotnet run --project src/OlxMonitor.Worker -- viewer
```

**GUI Viewer:**
```powershell
dotnet run --project src/OlxMonitor.Viewer
```

### 4. Build Solution
```powershell
dotnet build Marketplace-Monitor.slnx
```

## Docker Deployment

The OLX Monitor Worker can be easily deployed using Docker.

### Prerequisites
- [Docker](https://www.docker.com/) and Docker Compose installed

### Using Docker Compose (Recommended)
```powershell
# Build and start the container
docker-compose up -d --build

# View logs
docker-compose logs -f olx-monitor

# Stop the container
docker-compose down
```

Data is persisted in a Docker volume named `olx-data`.

### Using Docker (standalone)
```powershell
# Build the image
docker build -t olx-monitor -f src/OlxMonitor.Worker/Dockerfile .

# Run the container
docker run -d `
  --name olx-monitor `
  -v olx-data:/app/Data `
  olx-monitor
```

## Configuration

Searches are configured in `src/OlxMonitor.Worker/appsettings.json`:

```json
{
  "MonitorSettings": {
    "Searches": [
      {
        "CategoryPath": "/elektronika/gry-konsole/",
        "Keyword": "xbox 360"
      }
    ]
  }
}
```

**Current searches** (example):
- DDR4 RAM (`/elektronika/komputery/podzespoly-i-czesci/q-ddr4-ram/`)

## Project Structure

| Project | Description |
|---------|-------------|
| `OlxMonitor.Core` | Shared models (`Listing`, `MonitorSettings`) |
| `OlxMonitor.Infrastructure` | Database context, migrations, and OLX scraper (HtmlAgilityPack) |
| `OlxMonitor.Worker` | Background service, console viewer, and CLI commands |
| `OlxMonitor.Viewer` | WPF desktop GUI viewer with clickable links |
| `tests/OlxMonitor.Tests` | Unit tests for scraper and database |

## Database

- **Location**: `src/OlxMonitor.Worker/Data/olxmonitor.db`
- **Provider**: SQLite with Entity Framework Core migrations

## Recent Updates

- ✅ Added WPF desktop viewer with clickable listing URLs
- ✅ Added unit tests for scraper and database operations

## Planned Improvements

- [ ] Improved title cleaning (remove "Odświeżono dnia...", dates, etc.)
- [ ] Email / Discord notifications for new listings

## License

This project is open source and available under the [MIT License](LICENSE).