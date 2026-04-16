# OLX Monitor

A simple .NET 8 background worker that scrapes OLX.pl for new listings based on your configured searches and saves them into SQLite.

## Features

- Configurable searches via `appsettings.json`
- Easy way to add new searches: `dotnet run -- add-url <full-olx-url>`
- Console viewer: `dotnet run -- viewer`

## Quick Start

1. Add a search
```powershell
cd src\OlxMonitor.Worker
dotnet run -- add-url https://www.olx.pl/elektronika/komputery/podzespoly-i-czesci/q-ddr4-ram/ (example)
2. Run the monitor
PowerShellcd src\OlxMonitor.Worker
dotnet run
3. View your saved listings
PowerShellcd src\OlxMonitor.Worker
dotnet run -- viewer
4. Build everything
PowerShelldotnet build Marketplace-Monitor.slnx
Current Configuration
Your appsettings.json currently monitors:

DDR4 RAM (/elektronika/komputery/podzespoly-i-czesci/q-ddr4-ram/)

You can add more searches anytime using the add-url command.

Project Structure:

OlxMonitor.Core → Models (Listing, MonitorSettings)
OlxMonitor.Infrastructure → Database + Scraper (HtmlAgilityPack)
OlxMonitor.Worker → Background service + Viewer + Program

Database
SQLite file: src\OlxMonitor.Worker\Data\olxmonitor.db

Future Improvements (planned):

Better title cleaning (remove "Odświeżono dnia...", dates, etc.)
Email / Discord notifications when new items appear
Simple web viewer
Docker + docker-compose setup