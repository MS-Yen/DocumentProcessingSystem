# =============================================================================
# Dockerfile for the RAG Service
#
# C# equivalent: This is like a .csproj + publish + deployment script combined.
# Docker creates a reproducible, isolated environment for the application.
#
# HOW DOCKER WORKS (for .NET developers):
# - A Dockerfile is a recipe for building a container image
# - Each instruction (FROM, COPY, RUN, etc.) creates a "layer"
# - Layers are cached — if a layer hasn't changed, Docker reuses it
# - The final image is a lightweight Linux environment with your app inside
# =============================================================================

# --- BASE IMAGE ---
# Start from an official Python 3.12 image (slim = minimal, ~150MB).
# C# equivalent: FROM mcr.microsoft.com/dotnet/aspnet:8.0
# "slim" is like the ASP.NET runtime image — no build tools, just what's needed to run.
FROM python:3.12-slim

# --- WORKING DIRECTORY ---
# Set /app as the working directory inside the container.
# All subsequent commands run from this directory.
# C# equivalent: WORKDIR /app in a .NET Dockerfile
WORKDIR /app

# --- INSTALL DEPENDENCIES ---
# Copy requirements.txt FIRST, then install.
# This is a Docker best practice: dependencies rarely change, so this layer
# gets cached. When you change code (but not dependencies), Docker skips
# reinstalling packages — much faster rebuilds.
# C# equivalent: COPY *.csproj . && dotnet restore (same caching strategy)
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
# --no-cache-dir: Don't store pip's download cache (saves ~50MB in image size)

# --- COPY APPLICATION CODE ---
# Copy the Python source code and .env into the container.
# This layer changes whenever you edit code, but dependencies (above) stay cached.
# C# equivalent: COPY . . && dotnet publish -c Release -o out
COPY app/ app/
COPY .env .env

# --- PREPARE DATA DIRECTORY ---
# Create the directory where ChromaDB will store its data.
# In production, this is mounted as a Docker volume so data persists
# even if the container is destroyed and recreated.
# C# equivalent: mkdir for a SQLite database file location
RUN mkdir -p /app/chroma_data

# --- EXPOSE PORT ---
# Document that this container listens on port 5002.
# This doesn't actually open the port — you still need -p 5002:5002 when running.
# It's metadata for documentation and Docker Compose.
# C# equivalent: "applicationUrl": "http://+:5002" in launchSettings.json
EXPOSE 5002

# --- START COMMAND ---
# The command that runs when the container starts.
# uvicorn is the ASGI server for FastAPI (like Kestrel for ASP.NET).
# - "app.main:app" means: in module app.main, use the object named 'app'
# - "--host 0.0.0.0" binds to all network interfaces (required in Docker)
# - "--port 5002" matches our EXPOSE declaration
# C# equivalent: dotnet MyApp.dll --urls http://0.0.0.0:5002
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "5002"]
