# OLX Monitor

A simple .NET 8 background worker that scrapes OLX.pl for new listings based on your configured searches and saves them into SQLite.

## Features

- Configurable searches via `appsettings.json`
- Easy way to add new searches: `dotnet run -- add-url <full-olx-url>`
- Console viewer: `dotnet run -- viewer` (from Worker directory)
- WPF GUI viewer: `dotnet run -p src/OlxMonitor.Viewer/OlxMonitor.Viewer.csproj` (from project root)

## Quick Start

```
1. Add a search
Powershell
cd src\OlxMonitor.Worker
dotnet run -- add-url https://www.olx.pl/elektronika/komputery/podzespoly-i-czesci/q-ddr4-ram/ (example)
2. Run the monitor
PowerShell
cd src\OlxMonitor.Worker
dotnet run
3. View your saved listings (console)
PowerShell
cd src\OlxMonitor.Worker
dotnet run -- viewer

3.1 View your saved listings (GUI)
PowerShell
dotnet run -p src/OlxMonitor.Viewer/OlxMonitor.Viewer.csproj
4. Build everything
PowerShell
dotnet build Marketplace-Monitor.slnx

Current Configuration
Your appsettings.json currently monitors:
DDR4 RAM (/elektronika/komputery/podzespoly-i-czesci/q-ddr4-ram/)

You can add more searches anytime using the add-url command.

Project Structure:
- OlxMonitor.Core → Models (Listing, MonitorSettings)
- OlxMonitor.Infrastructure → Database + Scraper (HtmlAgilityPack)
- OlxMonitor.Worker → Background service + Console viewer + CLI commands (add-url)
- OlxMonitor.Viewer → WPF GUI viewer (listings with clickable URLs)
- tests/OlxMonitor.Tests → Unit tests (scraper, DbContext)

Database
SQLite file: src\OlxMonitor.Worker\Data\olxmonitor.db

Recent Updates:
- Added WPF desktop viewer with clickable listing URLs
- Added unit tests for scraper and database

Future Improvements (planned):
- Better title cleaning (remove "Odświeżono dnia...", dates, etc.)
- Email / Discord notifications when new items appear
- Docker + docker-compose setup
