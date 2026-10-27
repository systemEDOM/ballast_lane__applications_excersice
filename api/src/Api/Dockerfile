FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props BallastLane.sln ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Api/Api.csproj src/Api/

RUN dotnet restore src/Api/Api.csproj

COPY src/ src/

RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM build AS test
COPY tests/ tests/
RUN dotnet restore BallastLane.sln \
    && dotnet test BallastLane.sln -c Release --nologo --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /data && chown -R app:app /data

COPY --from=build --chown=app:app /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "Api.dll"]
