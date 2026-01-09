# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/out

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Render provides PORT
ENV ASPNETCORE_URLS=http://0.0.0.0:$PORT

CMD ["dotnet", "YourProjectName.dll"]