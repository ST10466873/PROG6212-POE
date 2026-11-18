# RaceDay MVC web application — multi-stage, non-root production image.
# Build context: repository root.  docker build -t raceday-web .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (copy only project files) so NuGet layers are cached between builds.
COPY RaceDay.slnx ./
COPY src/RaceDay.Api/RaceDay.Api.csproj src/RaceDay.Api/
COPY src/RaceDay.Web/RaceDay.Web.csproj src/RaceDay.Web/
RUN dotnet restore src/RaceDay.Web/RaceDay.Web.csproj

COPY . ./
RUN dotnet publish src/RaceDay.Web/RaceDay.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Run as an unprivileged user rather than root.
RUN groupadd --system raceday && useradd --system --gid raceday raceday
COPY --from=build /app/publish ./
RUN chown -R raceday:raceday /app
USER raceday

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
