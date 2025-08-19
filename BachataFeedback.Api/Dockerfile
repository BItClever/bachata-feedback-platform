FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BachataFeedback.Api/BachataFeedback.Api.csproj", "BachataFeedback.Api/"]
COPY ["BachataFeedback.Core/BachataFeedback.Core.csproj", "BachataFeedback.Core/"]
RUN dotnet restore "BachataFeedback.Api/BachataFeedback.Api.csproj"
COPY . .
WORKDIR "/src/BachataFeedback.Api"
RUN dotnet build "BachataFeedback.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BachataFeedback.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BachataFeedback.Api.dll"]