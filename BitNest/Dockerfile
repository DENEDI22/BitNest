FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BitNest/ BitNest/
COPY BitNest.Sharing.Contracts/ BitNest.Sharing.Contracts/
RUN dotnet restore BitNest/BitNest.csproj
RUN dotnet publish BitNest/BitNest.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

RUN mkdir -p /app/data/storage

ENTRYPOINT ["dotnet", "BitNest.dll"]
