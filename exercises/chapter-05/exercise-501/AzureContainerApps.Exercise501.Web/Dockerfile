FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["AzureContainerApps.Exercise501.Web/AzureContainerApps.Exercise501.Web.csproj", "AzureContainerApps.Exercise501.Web/"]
COPY ["AzureContainerApps.Exercise501.Web.Client/AzureContainerApps.Exercise501.Web.Client.csproj", "AzureContainerApps.Exercise501.Web.Client/"]
RUN dotnet restore "AzureContainerApps.Exercise501.Web/AzureContainerApps.Exercise501.Web.csproj"

COPY . .
RUN dotnet publish "AzureContainerApps.Exercise501.Web/AzureContainerApps.Exercise501.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AzureContainerApps.Exercise501.Web.dll"]
