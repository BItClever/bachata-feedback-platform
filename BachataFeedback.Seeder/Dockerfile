FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BachataFeedback.Seeder/BachataFeedback.Seeder.csproj", "BachataFeedback.Seeder/"]
RUN dotnet restore "BachataFeedback.Seeder/BachataFeedback.Seeder.csproj"
COPY . .
WORKDIR "/src/BachataFeedback.Seeder"
RUN dotnet publish "BachataFeedback.Seeder.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BachataFeedback.Seeder.dll"]