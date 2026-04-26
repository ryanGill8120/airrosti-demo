# syntax=docker/dockerfile:1
# Build context is the repo root (see render.yaml -> dockerContext: .)

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AirrostiDemo.Server/AirrostiDemo.Server.csproj AirrostiDemo.Server/
COPY AirrostiDemo.Shared/AirrostiDemo.Shared.csproj AirrostiDemo.Shared/
RUN dotnet restore AirrostiDemo.Server/AirrostiDemo.Server.csproj

COPY AirrostiDemo.Server/ AirrostiDemo.Server/
COPY AirrostiDemo.Shared/ AirrostiDemo.Shared/
RUN dotnet publish AirrostiDemo.Server/AirrostiDemo.Server.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render's Docker services route to whatever port the app listens on.
# 10000 is Render's default PORT for Docker; keep them aligned.
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "AirrostiDemo.Server.dll"]
