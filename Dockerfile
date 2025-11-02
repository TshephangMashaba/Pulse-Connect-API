FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy from the Pulse-Connect-API subdirectory
COPY ["Pulse-Connect-API/Pulse-Connect-API.csproj", "Pulse-Connect-API/"]
RUN dotnet restore "Pulse-Connect-API/Pulse-Connect-API.csproj"

COPY . .
WORKDIR "/src/Pulse-Connect-API"
RUN dotnet build "Pulse-Connect-API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Pulse-Connect-API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Pulse-Connect-API.dll"]