# ---------- BUILD STAGE ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore Sovva.WebAPI/Sovva.WebAPI.csproj
RUN dotnet publish Sovva.WebAPI/Sovva.WebAPI.csproj -c Release -o /app/publish


# ---------- RUNTIME STAGE ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

# Render requires port 10000
ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

RUN addgroup --system --gid 1001 dotnetgroup && \
    adduser --system --uid 1001 --ingroup dotnetgroup dotnetuser && \
    mkdir -p /app/logs && \
    chown -R dotnetuser:dotnetgroup /app/logs
    
USER dotnetuser
ENTRYPOINT ["dotnet", "Sovva.WebAPI.dll"]