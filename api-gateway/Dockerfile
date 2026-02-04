# =============================================================================
# Dockerfile for the API Gateway (.NET 8 + YARP)
#
# Same multi-stage build pattern as the Document Service:
# Stage 1: Build with full SDK
# Stage 2: Run with slim runtime
# =============================================================================

# --- STAGE 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies first (layer caching)
COPY ApiGateway.csproj .
RUN dotnet restore

# Copy source and publish
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- STAGE 2: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "ApiGateway.dll"]
