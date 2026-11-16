# Build context is the repo root (compose sets it): CombatCore is linked from Assets/Scripts/CombatCore.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Server/src/DoomScroller.Server/ Server/src/DoomScroller.Server/
COPY Server/data/ Server/data/
COPY Assets/Scripts/CombatCore/ Assets/Scripts/CombatCore/
RUN dotnet publish Server/src/DoomScroller.Server -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DoomScroller.Server.dll"]
