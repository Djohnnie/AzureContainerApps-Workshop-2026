FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UltimateSnake.Backend.Api.csproj", "."]
RUN dotnet restore "UltimateSnake.Backend.Api.csproj"

COPY . .
RUN dotnet publish "UltimateSnake.Backend.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UltimateSnake.Backend.Api.dll"]
