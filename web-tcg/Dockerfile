# Consultez https://aka.ms/customizecontainer pour savoir comment personnaliser votre conteneur de débogage et comment Visual Studio utilise ce Dockerfile pour générer vos images afin d’accélérer le débogage.

# Cet index est utilisé lors de l’exécution à partir de VS en mode rapide (par défaut pour la configuration de débogage)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Le CSS généré est exclu de Git et reconstruit dans l'image.
FROM node:24-alpine AS frontend
WORKDIR /frontend
COPY web-tcg/package.json web-tcg/package-lock.json ./
RUN npm ci
COPY web-tcg/ ./
RUN npm run build:css

# Cette phase est utilisée pour générer le projet de service
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["web-tcg/web-tcg.csproj", "web-tcg/"]
RUN dotnet restore "./web-tcg/web-tcg.csproj"
COPY . .
COPY --from=frontend /frontend/wwwroot/css/output.css /src/web-tcg/wwwroot/css/output.css
WORKDIR "/src/web-tcg"
RUN dotnet build "./web-tcg.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Cette étape permet de publier le projet de service à copier dans la phase finale
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./web-tcg.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Cette phase est utilisée en production ou lors de l’exécution à partir de VS en mode normal (par défaut quand la configuration de débogage n’est pas utilisée)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "web-tcg.dll"]
