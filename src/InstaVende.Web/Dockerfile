# syntax=docker/dockerfile:1.7
# API InstaVende (.NET 10). Contexto de build: raíz del repositorio.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props InstaVende.slnx ./
COPY src/InstaVende.Core/InstaVende.Core.csproj src/InstaVende.Core/
COPY src/InstaVende.Infrastructure/InstaVende.Infrastructure.csproj src/InstaVende.Infrastructure/
COPY src/InstaVende.Web/InstaVende.Web.csproj src/InstaVende.Web/
RUN dotnet restore src/InstaVende.Web/InstaVende.Web.csproj
COPY src/ src/
RUN dotnet publish src/InstaVende.Web/InstaVende.Web.csproj -c Release -o /app/publish --no-restore \
    -p:OpenApiGenerateDocumentsOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# ICU completo para zonas horarias y culturas (es-PE)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://+:8080 \
    WhatsAppClient__AutoStart=false
COPY --from=build /app/publish .
RUN mkdir -p /app/wwwroot/uploads /app/logs && chown -R app:app /app/wwwroot/uploads /app/logs
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "InstaVende.Web.dll"]
