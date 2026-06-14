FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/ReqSimulator.Api/ReqSimulator.Api.csproj src/ReqSimulator.Api/
RUN dotnet restore src/ReqSimulator.Api/ReqSimulator.Api.csproj

# Copy everything else and build
COPY . .
WORKDIR /app/src/ReqSimulator.Api
RUN dotnet publish -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render requires apps to bind to port 8080 (or any PORT env var)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ReqSimulator.Api.dll"]
