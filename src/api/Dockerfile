# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY SecureFileTransfer.sln ./
COPY src/api/SecureFileTransfer.Api/SecureFileTransfer.Api.csproj src/api/SecureFileTransfer.Api/
COPY src/api/SecureFileTransfer.Api.Tests/SecureFileTransfer.Api.Tests.csproj src/api/SecureFileTransfer.Api.Tests/
COPY src/functions/SecureFileTransfer.Functions/SecureFileTransfer.Functions.csproj src/functions/SecureFileTransfer.Functions/
COPY src/functions/SecureFileTransfer.Functions.Tests/SecureFileTransfer.Functions.Tests.csproj src/functions/SecureFileTransfer.Functions.Tests/

RUN dotnet restore src/api/SecureFileTransfer.Api/SecureFileTransfer.Api.csproj

# Copy API source and publish
COPY src/api/ src/api/
RUN dotnet publish src/api/SecureFileTransfer.Api/SecureFileTransfer.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SecureFileTransfer.Api.dll"]
