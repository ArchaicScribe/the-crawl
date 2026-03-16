FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/TheCrawl.API/TheCrawl.API.csproj", "src/TheCrawl.API/"]
COPY ["src/TheCrawl.Application/TheCrawl.Application.csproj", "src/TheCrawl.Application/"]
COPY ["src/TheCrawl.Infrastructure/TheCrawl.Infrastructure.csproj", "src/TheCrawl.Infrastructure/"]
COPY ["src/TheCrawl.Domain/TheCrawl.Domain.csproj", "src/TheCrawl.Domain/"]
RUN dotnet restore "src/TheCrawl.API/TheCrawl.API.csproj"
COPY . .
WORKDIR "/src/src/TheCrawl.API"
RUN dotnet build "TheCrawl.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TheCrawl.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TheCrawl.API.dll"]
