
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy source code
COPY src/MessageMicroservice/MessageMicroservice/ MessageMicroservice/
COPY src/MessageMicroservice/SharedModels/ SharedModels/

# Remove Windows build files
RUN find /src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +

# Restore dependencies
RUN dotnet restore MessageMicroservice/MessageMicroservice.csproj

# Publish application
RUN dotnet publish MessageMicroservice/MessageMicroservice.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MessageMicroservice.dll"]
