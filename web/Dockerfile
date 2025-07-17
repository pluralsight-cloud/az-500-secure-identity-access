FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e6748a3b3d8cea753f643cacac0e577d8c409285b3924232db1354693bdbd3d7 AS build
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:35095f3e2bf5ab1f0c6953ed1364028343b5aef029932454cf2ce9e9680827d8
WORKDIR /App
COPY --from=build /App/out .
EXPOSE 80
ENTRYPOINT ["dotnet", "web.dll"]