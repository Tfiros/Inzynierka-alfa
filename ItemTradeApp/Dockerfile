FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["ItemTradeApp.csproj", "./"]

RUN dotnet restore "ItemTradeApp.csproj"

COPY . .

RUN dotnet publish "ItemTradeApp.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

COPY --from=build /app/publish .
COPY Resources/EmailTemplates ./Resources/EmailTemplates

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "ItemTradeApp.dll"]