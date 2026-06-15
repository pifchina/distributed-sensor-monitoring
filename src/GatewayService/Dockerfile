FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY distributed-sensor-monitoring.sln .
COPY src/GatewayService/GatewayService.csproj src/GatewayService/

RUN dotnet restore src/GatewayService/GatewayService.csproj

COPY src/ src/
RUN dotnet publish src/GatewayService/GatewayService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "GatewayService.dll"]
