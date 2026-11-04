FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/DoomScroller.Server/ src/DoomScroller.Server/
# TODO(016): CombatCore is linked from Assets/Scripts/CombatCore (outside this context). When it exists, build with the repo root as context and COPY it to /src/Assets/Scripts/CombatCore.
RUN dotnet publish src/DoomScroller.Server -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DoomScroller.Server.dll"]
