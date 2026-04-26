FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
USER root
WORKDIR /app
EXPOSE 80

ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

RUN apk add --no-cache icu-data-full=76.1-r1 icu-libs=76.1-r1

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["src/MagicEightBall.API/MagicEightBall.API.csproj", "MagicEightBall.API/"]
RUN dotnet restore "MagicEightBall.API/MagicEightBall.API.csproj"
COPY /src .
WORKDIR "/src/MagicEightBall.API"
RUN dotnet build "MagicEightBall.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MagicEightBall.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "MagicEightBall.API.dll"]
