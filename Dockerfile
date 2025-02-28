FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

# Expose port 4840 for opc server
EXPOSE 4840

# Use root to avoid permission issues
USER root

# Create and set permissions
RUN mkdir -p /app/pki && chmod -R 777 /app/pki

# Revert to a safer non-root user if needed

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OPCUA.Server.Simulator.csproj", "."]
RUN dotnet restore "./OPCUA.Server.Simulator.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./OPCUA.Server.Simulator.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OPCUA.Server.Simulator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OPCUA.Server.Simulator.dll"]
