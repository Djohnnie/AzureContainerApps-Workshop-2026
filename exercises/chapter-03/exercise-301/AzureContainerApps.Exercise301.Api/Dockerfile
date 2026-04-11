FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AzureContainerApps.Exercise301.Orleans.Host/AzureContainerApps.Exercise301.Orleans.Host.csproj", "AzureContainerApps.Exercise301.Orleans.Host/"]
COPY ["AzureContainerApps.Exercise301.Api/AzureContainerApps.Exercise301.Api.csproj", "AzureContainerApps.Exercise301.Api/"]
RUN dotnet restore "AzureContainerApps.Exercise301.Api/AzureContainerApps.Exercise301.Api.csproj"
COPY . .
RUN dotnet publish "AzureContainerApps.Exercise301.Api/AzureContainerApps.Exercise301.Api.csproj" -c Release -o /app/publish /p:UseAspire=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AzureContainerApps.Exercise301.Api.dll"]
