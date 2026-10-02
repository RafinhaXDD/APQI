# Build context: repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/BookExchange.Domain/BookExchange.Domain.csproj src/BookExchange.Domain/
COPY src/BookExchange.Application/BookExchange.Application.csproj src/BookExchange.Application/
COPY src/BookExchange.Infrastructure/BookExchange.Infrastructure.csproj src/BookExchange.Infrastructure/
COPY src/BookExchange.Api/BookExchange.Api.csproj src/BookExchange.Api/
RUN dotnet restore src/BookExchange.Api/BookExchange.Api.csproj

COPY src/ src/
RUN dotnet publish src/BookExchange.Api/BookExchange.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Non-root user built into the official .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "BookExchange.Api.dll"]
