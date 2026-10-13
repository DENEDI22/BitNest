FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BitNest.Sharing.Contracts/ BitNest.Sharing.Contracts/
COPY BitNest.Sharing/ BitNest.Sharing/
RUN dotnet publish BitNest.Sharing/BitNest.Sharing.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "BitNest.Sharing.dll"]
