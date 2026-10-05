# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY ReviewOrchestrator.slnx ./
COPY src/ReviewOrchestrator.Core/ReviewOrchestrator.Core.csproj src/ReviewOrchestrator.Core/
COPY src/ReviewOrchestrator.Infrastructure/ReviewOrchestrator.Infrastructure.csproj src/ReviewOrchestrator.Infrastructure/
COPY src/ReviewOrchestrator.Api/ReviewOrchestrator.Api.csproj src/ReviewOrchestrator.Api/
RUN dotnet restore src/ReviewOrchestrator.Api/ReviewOrchestrator.Api.csproj
COPY src/ReviewOrchestrator.Core/ src/ReviewOrchestrator.Core/
COPY src/ReviewOrchestrator.Infrastructure/ src/ReviewOrchestrator.Infrastructure/
COPY src/ReviewOrchestrator.Api/ src/ReviewOrchestrator.Api/
RUN dotnet publish src/ReviewOrchestrator.Api/ReviewOrchestrator.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /app/data && chown -R app:app /app/data
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__ReviewHub="Data Source=/app/data/reviewhub.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "ReviewOrchestrator.Api.dll"]
