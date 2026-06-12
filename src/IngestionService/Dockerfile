FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY distributed-sensor-monitoring.sln .
COPY src/SensorMonitoring.Contracts/SensorMonitoring.Contracts.csproj src/SensorMonitoring.Contracts/
COPY src/SensorMonitoring.Data/SensorMonitoring.Data.csproj src/SensorMonitoring.Data/
COPY src/SensorMonitoring.Security/SensorMonitoring.Security.csproj src/SensorMonitoring.Security/
COPY src/IngestionService/IngestionService.csproj src/IngestionService/

RUN dotnet restore src/IngestionService/IngestionService.csproj

COPY src/ src/
RUN dotnet publish src/IngestionService/IngestionService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "IngestionService.dll"]
