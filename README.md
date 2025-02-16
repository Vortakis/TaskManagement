# Task Management WebApp

An efficient task management WebApp built with .NET 8 and SQLite.

## Prerequisites

- .NET 8 SDK
- SQLite (NuGet Package) 9.0.2
- Developed in Visual Studio 2022, but any IDE can work

## Setup

1. **Clone the repository:**
   ```sh
   git clone https://github.com/Vortakis/TaskManagement.git
   ```

2. **Configuration:**
   - Ensure `appsettings.json` contains the correct SQLite connection string.
   - Apply changes to these settings before running if required.

3. **Run the application:**
   ```sh
   cd TaskManagement/src/TaskManagement.Api
   dotnet run
   ```

3. **Test APIs:**
   - All Endpoints & their structure can be viewed in the Swagger page:
   ```sh
   http://localhost:<PORT>/swagger/index.html
   ```
## Features

- Task Create, UpdateStatus, Get and Delete APIs
- Paginated GetAll API
- Optimised Bulk Update Status API (Multithreading & Batching)
- Status & Priority Handling
- Caching & Invalidation (replacable by Redis)
- Concurrent Updates Hanlding
- Exposed Settings for Configuration
- Logging & Exception Handling
- Initialized Unit Testing with Two Example Cases
- Clean Code & SOLID Principles applied
