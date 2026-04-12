FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["AzureContainerApps.Exercise502.Web/AzureContainerApps.Exercise502.Web.csproj", "AzureContainerApps.Exercise502.Web/"]
COPY ["AzureContainerApps.Exercise502.Web.Client/AzureContainerApps.Exercise502.Web.Client.csproj", "AzureContainerApps.Exercise502.Web.Client/"]
RUN dotnet restore "AzureContainerApps.Exercise502.Web/AzureContainerApps.Exercise502.Web.csproj"

COPY . .
RUN dotnet publish "AzureContainerApps.Exercise502.Web/AzureContainerApps.Exercise502.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AzureContainerApps.Exercise502.Web.dll"]
