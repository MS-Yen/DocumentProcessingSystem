# =============================================================================
# Dockerfile — Multi-stage build for the Report Service
#
# Stage 1 (build): Uses the .NET SDK image to compile the application
# Stage 2 (runtime): Uses the slim ASP.NET runtime image to run it
#
# This is a multi-project build (solution with 3 projects), so we need
# to copy all .csproj files first for layer caching, restore dependencies,
# then copy source code and publish.
#
# KEY CONCEPT — Multi-stage builds:
# The SDK image (~800MB) includes compilers and build tools.
# The runtime image (~200MB) only includes what's needed to RUN the app.
# By using two stages, the final image is much smaller.
#
# QuestPDF NOTE:
# QuestPDF uses SkiaSharp for PDF rendering, which requires native
# libraries. The base ASP.NET image includes the necessary dependencies,
# but if you see "libSkiaSharp.so" errors, install libfontconfig1:
#   RUN apt-get update && apt-get install -y libfontconfig1 && rm -rf /var/lib/apt/lists/*
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file and all .csproj files first.
# Docker caches each layer — if .csproj files haven't changed, the
# restore step is skipped entirely on rebuild (huge time saver).
COPY ReportService.sln ./
COPY src/ReportService.Core/ReportService.Core.csproj src/ReportService.Core/
COPY src/ReportService.Infrastructure/ReportService.Infrastructure.csproj src/ReportService.Infrastructure/
COPY src/ReportService.API/ReportService.API.csproj src/ReportService.API/

# Restore NuGet packages using the solution file (cached unless .csproj files change)
RUN dotnet restore ReportService.sln

# Copy all source code
COPY src/ src/

# Publish the API project in Release configuration.
# We include restore here to ensure package resolution matches the target framework
# exactly (avoids version mismatch issues with transitive dependencies).
# -o /app/publish = output directory for the published files
RUN dotnet publish src/ReportService.API/ReportService.API.csproj \
    -c Release \
    -o /app/publish

# ---------------------------------------------------------------------------
# Stage 2: Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install font libraries needed by QuestPDF/SkiaSharp for PDF rendering.
# Without these, QuestPDF may fail with "unable to load font" errors.
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        libfontconfig1 \
        fonts-dejavu-core && \
    rm -rf /var/lib/apt/lists/*

# Copy the published app from the build stage
COPY --from=build /app/publish .

# Set the port the app listens on
ENV ASPNETCORE_URLS=http://+:5003

# Expose port 5003 (documentation — doesn't actually open the port)
EXPOSE 5003

# Start the application
ENTRYPOINT ["dotnet", "ReportService.API.dll"]
