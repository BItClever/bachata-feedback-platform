FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BachataFeedback.TelegramBot/BachataFeedback.TelegramBot.csproj", "BachataFeedback.TelegramBot/"]
COPY ["BachataFeedback.Api/BachataFeedback.Api.csproj", "BachataFeedback.Api/"]
COPY ["BachataFeedback.Core/BachataFeedback.Core.csproj", "BachataFeedback.Core/"]
RUN dotnet restore "BachataFeedback.TelegramBot/BachataFeedback.TelegramBot.csproj"
COPY . .
RUN rm -f /src/BachataFeedback.Api/appsettings.json /src/BachataFeedback.Api/appsettings.*.json || true
WORKDIR "/src/BachataFeedback.TelegramBot"
RUN dotnet build "BachataFeedback.TelegramBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BachataFeedback.TelegramBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BachataFeedback.TelegramBot.dll"]
