# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TamircimAPI/TamircimAPI.csproj TamircimAPI/
RUN dotnet restore TamircimAPI/TamircimAPI.csproj

COPY TamircimAPI/ TamircimAPI/
RUN dotnet publish TamircimAPI/TamircimAPI.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/logs

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TamircimAPI.dll"]
