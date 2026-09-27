# =============================================================================
# Rescauta - Dockerfile multi-stage
#
# Se despliega en Render con:
#   Build Command : (vacío - Render detecta el Dockerfile)
#   Start Command : (vacío - usa el ENTRYPOINT de este archivo)
#
# Contexto de build: la RAÍZ del repositorio, porque la solución vive en Rescauta/.
# En Render: Root Directory = "." (raíz) para que el contexto incluya la carpeta Rescauta/.
#
# OJO con las versiones: la imagen SDK debe coincidir EXACTAMENTE con el
# TargetFramework de Rescauta/Directory.Build.props. Hoy es net8.0. Si ese archivo
# pasa a net10.0, hay que subir AMBAS imágenes a 10.0 o el build falla con NETSDK1045
# ("The current .NET SDK does not support targeting .NET X.0").
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: build. Solo queda en la capa de imágenes intermedia; no llega a producción.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Capa 1: SOLO los archivos de proyecto -----------------------------------
# Se copian primero y se restauran aparte a proposito. El restore de NuGet solo
# depende de los .csproj, asi que mientras estos no cambien Docker reutiliza la
# capa cacheada y no vuelve a descargar paquetes en cada build. Si se copiara todo
# el codigo antes de restaurar, cada cambio de una linea invalidaria el restore.
COPY Rescauta/Directory.Build.props Rescauta/Directory.Packages.props ./
COPY Rescauta/Rescauta.sln ./
COPY Rescauta/Rescauta.Domain/Rescauta.Domain.csproj Rescauta/Rescauta.Domain/
COPY Rescauta/Rescauta.Application/Rescauta.Application.csproj Rescauta/Rescauta.Application/
COPY Rescauta/Rescauta.Infrastructure/Rescauta.Infrastructure.csproj Rescauta/Rescauta.Infrastructure/
COPY Rescauta/Rescauta.Api/Rescauta.Api.csproj Rescauta/Rescauta.Api/

# Se restaura la SOLUCION (no un proyecto suelto) para que los 4 proyectos queden
# resueltos, incluidas las referencias entre ellos.
RUN dotnet restore Rescauta/Rescauta.sln

# --- Capa 2: el codigo fuente ------------------------------------------------
COPY Rescauta/ ./

# --no-restore: el restore ya se hizo arriba y no debe repetirse.
# /p:UseAppHost=false: no genera el ejecutable nativo; en Linux el runtime ejecuta
# la DLL con "dotnet", y el apphost solo anadiria peso.
RUN dotnet publish Rescauta/Rescauta.Api/Rescauta.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 2: runtime. Imagen de runtime, no de SDK: no lleva el compilador, ni
# Roslyn, ni el package manager. Es la diferencia de tamaño mas grande posible.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

# --- Red ---------------------------------------------------------------------
# "+" en 0.0.0.0 es imprescindible: si se dejara solo "http://:8080" el Kestrel
# escucharia unicamente en loopback y Render no llegaria al contenedor.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# --- Usuario sin privilegios --------------------------------------------------
# La imagen de runtime .NET 8 ya define APP_UID. Ejecutar como root es
# innecesario: un incidente en la app no da acceso al host.
USER $APP_UID

# El entrypoint apunta al ensamblado que produce el proyecto Rescauta.Api.
ENTRYPOINT ["dotnet", "Rescauta.Api.dll"]
