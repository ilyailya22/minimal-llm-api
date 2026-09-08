# Build from the repository root: docker build -f src/MinimalLlm.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore against the project files alone, so a source-only change reuses this layer.
COPY MinimalLlm.slnx ./
COPY src/MinimalLlm.Api/MinimalLlm.Api.csproj src/MinimalLlm.Api/
COPY tests/MinimalLlm.Tests/MinimalLlm.Tests.csproj tests/MinimalLlm.Tests/
RUN dotnet restore src/MinimalLlm.Api/MinimalLlm.Api.csproj

COPY src/ src/
RUN dotnet publish src/MinimalLlm.Api/MinimalLlm.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The aspnet image ships a non-root "app" user; generating text needs no privileges.
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MinimalLlm.Api.dll"]
