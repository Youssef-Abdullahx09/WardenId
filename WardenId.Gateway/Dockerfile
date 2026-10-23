# syntax=docker/dockerfile:1.7-labs

# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
EXPOSE 8080

# Build image (SDK required)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only project and lock files first for better caching
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY --parents ["src/*/*.csproj", "src/*/packages.lock.json", "."]
COPY ["WardenId.Gateway/WardenId.Gateway.csproj", "WardenId.Gateway/"]
COPY ["WardenId.Gateway/packages.lock.json", "WardenId.Gateway/"]

# Restore dependencies
RUN dotnet restore "WardenId.Gateway/WardenId.Gateway.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/WardenId.Gateway"
RUN dotnet build "WardenId.Gateway.csproj" -c $BUILD_CONFIGURATION --no-restore

# Publish
FROM build AS publish
RUN dotnet publish "WardenId.Gateway.csproj" -c $BUILD_CONFIGURATION --no-build /p:UseAppHost=false -o /app/publish

# Final runtime image
FROM base AS final

WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "WardenId.Gateway.dll"]
