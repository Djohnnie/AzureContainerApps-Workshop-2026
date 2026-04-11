FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AzureContainerApps.Exercise401.Orleans.Host/AzureContainerApps.Exercise401.Orleans.Host.csproj", "AzureContainerApps.Exercise401.Orleans.Host/"]
COPY ["AzureContainerApps.Exercise401.Api/AzureContainerApps.Exercise401.Api.csproj", "AzureContainerApps.Exercise401.Api/"]
RUN dotnet restore "AzureContainerApps.Exercise401.Api/AzureContainerApps.Exercise401.Api.csproj"
COPY . .
RUN dotnet publish "AzureContainerApps.Exercise401.Api/AzureContainerApps.Exercise401.Api.csproj" -c Release -o /app/publish /p:UseAspire=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AzureContainerApps.Exercise401.Api.dll"]
