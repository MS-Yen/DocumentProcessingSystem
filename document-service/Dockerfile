# =============================================================================
# Dockerfile for the Document Service (.NET 8)
#
# This uses a multi-stage build — a Docker best practice for .NET:
# Stage 1 ("build"): Restore + build + publish using the full SDK image (~900MB)
# Stage 2 ("runtime"): Copy only the published output into a slim runtime image (~220MB)
#
# The final image is much smaller because it doesn't include:
# - The .NET SDK (compiler, MSBuild, NuGet)
# - Source code
# - Intermediate build artifacts
#
# Python comparison: The RAG service Dockerfile was simpler because Python
# doesn't have a separate compile step — you just copy the .py files.
# .NET requires compilation, so we use multi-stage builds.
# =============================================================================

# --- STAGE 1: Build ---
# Use the full .NET 8 SDK image for building.
# This image includes the compiler, MSBuild, NuGet, and all build tools.
# It's ~900MB — way too large for a production container.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Restore dependencies (layer caching optimization) ---
# Copy only the .csproj files first and restore NuGet packages.
# Docker caches this layer, so if your .csproj files don't change,
# NuGet restore is skipped on subsequent builds — much faster.
#
# This is the same strategy we used in the RAG Dockerfile:
#   COPY requirements.txt . && pip install -r requirements.txt
COPY src/DocumentService.Core/DocumentService.Core.csproj src/DocumentService.Core/
COPY src/DocumentService.Infrastructure/DocumentService.Infrastructure.csproj src/DocumentService.Infrastructure/
COPY src/DocumentService.API/DocumentService.API.csproj src/DocumentService.API/
RUN dotnet restore src/DocumentService.API/DocumentService.API.csproj

# --- Copy source code and build ---
# Now copy all source files and build in Release mode.
# -o /app/publish: Output the published files to /app/publish
# --no-restore: Skip restore since we already did it above
COPY src/ src/
RUN dotnet publish src/DocumentService.API/DocumentService.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- STAGE 2: Runtime ---
# Use the slim ASP.NET runtime image — only includes what's needed to run.
# This is like using python:3.12-slim instead of the full python image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy the published output from the build stage.
# COPY --from=build means "copy from the 'build' stage, not from the host."
COPY --from=build /app/publish .

# Document the port this service listens on.
# Must match the ASPNETCORE_URLS environment variable.
EXPOSE 5001

# Set environment variables for ASP.NET Core.
# ASPNETCORE_URLS tells Kestrel which port to listen on.
# Python equivalent: CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "5002"]
ENV ASPNETCORE_URLS=http://+:5001

# Start the application.
# In .NET, the published output includes a DLL that's run with "dotnet".
# The entrypoint is the API project's DLL.
ENTRYPOINT ["dotnet", "DocumentService.API.dll"]
