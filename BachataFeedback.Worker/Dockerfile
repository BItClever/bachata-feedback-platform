FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BachataFeedback.Worker/BachataFeedback.Worker.csproj", "BachataFeedback.Worker/"]
COPY ["BachataFeedback.Api/BachataFeedback.Api.csproj", "BachataFeedback.Api/"]
COPY ["BachataFeedback.Core/BachataFeedback.Core.csproj", "BachataFeedback.Core/"]
RUN dotnet restore "BachataFeedback.Worker/BachataFeedback.Worker.csproj"
COPY . .
WORKDIR "/src/BachataFeedback.Worker"
RUN dotnet publish "BachataFeedback.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BachataFeedback.Worker.dll"]