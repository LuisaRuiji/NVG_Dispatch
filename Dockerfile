# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

ENV SYSTEM_NET_HTTP_SOCKETS_HTTP2_SUPPORT=false
ENV NUGET_NET_TIMEOUT=300

COPY NVGInventory.csproj ./
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore --disable-parallel /nodeReuse:false

COPY . ./
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish NVGInventory.csproj -c Release -o /out /p:UseAppHost=false /nodeReuse:false -p:UseSharedCompilation=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "NVGInventory.dll"]
