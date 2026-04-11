FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["UltimateSnake.Backend.Api/UltimateSnake.Backend.Api.csproj", "UltimateSnake.Backend.Api/"]
COPY ["UltimateSnake.Orleans.Contracts/UltimateSnake.Orleans.Contracts.csproj", "UltimateSnake.Orleans.Contracts/"]
RUN dotnet restore "UltimateSnake.Backend.Api/UltimateSnake.Backend.Api.csproj" /p:UseAspire=false
COPY UltimateSnake.Backend.Api/ UltimateSnake.Backend.Api/
COPY UltimateSnake.Orleans.Contracts/ UltimateSnake.Orleans.Contracts/
WORKDIR /src/UltimateSnake.Backend.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAspire=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UltimateSnake.Backend.Api.dll"]
