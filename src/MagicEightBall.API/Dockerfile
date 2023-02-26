FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["MagicEightBall.API/MagicEightBall.API.csproj", "MagicEightBall.API/"]
RUN dotnet restore "MagicEightBall.API/MagicEightBall.API.csproj"
COPY . .
WORKDIR "/src/MagicEightBall.API"
RUN dotnet build "MagicEightBall.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MagicEightBall.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MagicEightBall.API.dll"]
