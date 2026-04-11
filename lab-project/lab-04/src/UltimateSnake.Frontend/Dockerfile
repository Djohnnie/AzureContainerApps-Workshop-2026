FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["UltimateSnake.Frontend/UltimateSnake.Frontend.csproj", "UltimateSnake.Frontend/"]
COPY ["UltimateSnake.Frontend.Client/UltimateSnake.Frontend.Client.csproj", "UltimateSnake.Frontend.Client/"]
RUN dotnet restore "UltimateSnake.Frontend/UltimateSnake.Frontend.csproj"
COPY UltimateSnake.Frontend/ UltimateSnake.Frontend/
COPY UltimateSnake.Frontend.Client/ UltimateSnake.Frontend.Client/
WORKDIR /src/UltimateSnake.Frontend
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UltimateSnake.Frontend.dll"]
