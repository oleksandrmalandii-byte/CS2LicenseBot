FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet build -c Release

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build /app/bin/Release/net6.0 .
ENTRYPOINT ["dotnet", "CS2LicenseBot.dll"]
