FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AzureContainerApps.Exercise302.Orleans.Host/AzureContainerApps.Exercise302.Orleans.Host.csproj", "AzureContainerApps.Exercise302.Orleans.Host/"]
COPY ["AzureContainerApps.Exercise302.Api/AzureContainerApps.Exercise302.Api.csproj", "AzureContainerApps.Exercise302.Api/"]
RUN dotnet restore "AzureContainerApps.Exercise302.Api/AzureContainerApps.Exercise302.Api.csproj"
COPY . .
RUN dotnet publish "AzureContainerApps.Exercise302.Api/AzureContainerApps.Exercise302.Api.csproj" -c Release -o /app/publish /p:UseAspire=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AzureContainerApps.Exercise302.Api.dll"]
