FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["UltimateSnake.Orleans.Host/UltimateSnake.Orleans.Host.csproj", "UltimateSnake.Orleans.Host/"]
COPY ["UltimateSnake.Orleans.Contracts/UltimateSnake.Orleans.Contracts.csproj", "UltimateSnake.Orleans.Contracts/"]
RUN dotnet restore "UltimateSnake.Orleans.Host/UltimateSnake.Orleans.Host.csproj" /p:UseAspire=false
COPY UltimateSnake.Orleans.Host/ UltimateSnake.Orleans.Host/
COPY UltimateSnake.Orleans.Contracts/ UltimateSnake.Orleans.Contracts/
WORKDIR /src/UltimateSnake.Orleans.Host
RUN dotnet publish -c Release -o /app/publish /p:UseAspire=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UltimateSnake.Orleans.Host.dll"]
