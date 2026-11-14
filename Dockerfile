# =============================================================================
# Rescauta API - Dockerfile multi-stage
#
# Que servicio de Render lo usa: "rescauta-api" (ver render.yaml, runtime: docker).
# El otro servicio, "rescauta-mvc", usa Dockerfile.mvc.
#
# Contexto de build: la RAIZ del repositorio, porque la solucion vive en Rescauta/.
# En render.yaml eso es dockerContext: .
#
# -----------------------------------------------------------------------------
# LAS VERSIONES DE LAS IMAGENES TIENEN QUE COINCIDIR CON EL TargetFramework
#
# Rescauta/Directory.Build.props declara net10.0. Con una imagen sdk:8.0 el build
# falla con NETSDK1045 ("The current .NET SDK does not support targeting .NET 10.0"),
# que es exactamente el fallo que rompia el despliegue. Si algum dia se cambia el
# TargetFramework, hay que cambiar LAS DOS lineas FROM de este archivo a la misma
# version. Las de arriba (SDK, para compilar) y las de abajo (runtime, para ejecutar).
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: build. Solo queda en la capa intermedia; no llega a produccion.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# --- Capa 1: SOLO los archivos de proyecto -----------------------------------
# Se copian primero y se restauran aparte a proposito: el restore de NuGet solo
# depende de los .csproj, asi que mientras estos no cambien Docker reutiliza la capa
# cacheada. Si se copiara todo el codigo antes de restaurar, cada cambio de una linea
# invalidaria el restore y habria que volver a descargar paquetes.
COPY Rescauta/Directory.Build.props Rescauta/Directory.Packages.props Rescauta/
COPY Rescauta/Rescauta.sln Rescauta/
COPY Rescauta/Rescauta.Domain/Rescauta.Domain.csproj Rescauta/Rescauta.Domain/
COPY Rescauta/Rescauta.Application/Rescauta.Application.csproj Rescauta/Rescauta.Application/
COPY Rescauta/Rescauta.Infrastructure/Rescauta.Infrastructure.csproj Rescauta/Rescauta.Infrastructure/
COPY Rescauta/Rescauta.Api/Rescauta.Api.csproj Rescauta/Rescauta.Api/
COPY Rescauta/Rescauta.Tests/Rescauta.Tests.csproj Rescauta/Rescauta.Tests/

# Se restaura la SOLUCION, no un proyecto suelto, para que los 5 queden resueltos
# incluidas las referencias entre ellos.
RUN dotnet restore Rescauta/Rescauta.sln

# --- Capa 2: el codigo fuente ------------------------------------------------
# Se copia DEBAJO de Rescauta/ para conservar la MISMA ruta que en la capa 1. Si
# aqui se hiciera "COPY Rescauta/ ./", el codigo acabaria en /src/Rescauta.Api/
# mientras que el restore dejo los assets en /src/Rescauta/Rescauta.Api/obj/.
# Entonces el publish --no-restore buscaria project.assets.json en una carpeta
# distinta y fallaria con NETSDK1004. Restore y publish deben ver exactamente las
# mismas rutas.
COPY Rescauta/ ./Rescauta/

# --no-restore: el restore ya se hizo arriba sobre las mismas rutas y no debe repetirse.
# /p:UseAppHost=false: no genera el ejecutable nativo. En Linux se ejecuta la DLL con
# "dotnet", asi que el apphost solo anadiria peso.
RUN dotnet publish Rescauta/Rescauta.Api/Rescauta.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 2: runtime. Imagen de runtime, no de SDK: no lleva compilador, ni Roslyn, ni
# gestor de paquetes. Es la diferencia de tamaño mas grande posible.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

# --- Entrypoint -----------------------------------------------------------------
# El entrypoint ya NO cambia privilegios: Render Free no permite operaciones como
# setpriv --bounding-set (falla con "Operation not permitted"). Solo valida que el
# usuario no root de la imagen puede escribir en /app/data y arranca la API.
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

# Herramientas que usa el entrypoint en tiempo de ejecucion. setpriv ya no aparece a
# proposito. Si falta alguna, el build falla aqui en vez de fallar en produccion.
RUN for tool in \
        /usr/bin/id \
        /usr/bin/find \
        /bin/sh \
        /bin/rm; do \
        test -x "$tool" || { echo "ERROR: falta $tool" >&2; exit 1; }; \
    done

# --- Carpeta de la base de datos ---------------------------------------------
# SQLite es UN ARCHIVO y debe vivir en una ruta escribible. La carpeta se crea y se
# asigna a $APP_UID durante el BUILD (aqui si hay privilegios de root). En Render
# Free no hay disco persistente: esta ruta queda dentro del filesystem efimero del
# contenedor y el proceso no root puede crear /app/data/rescauta.db directamente.
RUN mkdir -p /app/data \
    && chown -R $APP_UID:$APP_UID /app/data \
    && chmod 700 /app/data

# --- Base de datos por defecto ------------------------------------------------
# La API usa SQLite en /app/data/rescauta.db. En Render Free no hay disco
# persistente: el archivo vive en el sistema de archivos efimero del contenedor y
# se pierde al redesplegar. La API lo vuelve a crear al arrancar (MigrateOnStartup).
ENV Database__Provider=Sqlite
ENV Database__ConnectionString="Data Source=/app/data/rescauta.db"

# El esquema y los datos de arranque los crea la API al arrancar, en TODOS los entornos
# (ver Program.cs, seccion 11). Con esto en false arrancaria contra una base ya
# provisionada.
ENV Database__MigrateOnStartup=true
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# HOME explicito: el usuario app tiene /home/app, no /root.
ENV HOME=/home/app

# --- Puerto --------------------------------------------------------------------
# Render asigna el puerto en la variable PORT y espera que el contenedor escuche
# ahi. ASP.NET Core NO lee PORT por su cuenta: lee ASPNETCORE_URLS y
# ASPNETCORE_HTTP_PORTS, que son variables distintas. Por eso el puerto se pasa por
# linea de comandos (--urls), que es la fuente con MAS prioridad de todas, y no
# mediante un ENV fijo.
#
# El ${PORT:-8080} lo resuelve el shell en tiempo de ejecucion, no Docker en tiempo de
# build: si el puerto se escribiera en un ENV, quedaria congelado al valor que tuviera
# PORT durante el build (que no existe) y Render no alcanzaria el contenedor nunca.
# El 8080 es el valor por defecto para un "docker run" en local.
EXPOSE 8080

# --- Usuario sin privilegios ----------------------------------------------------
# El contenedor arranca directamente como $APP_UID (1654 en la imagen de .NET). No
# se arranca como root ni se intenta cambiar de usuario: /app/data ya pertenece a
# $APP_UID desde el build. Render Free ejecuta la imagen con el USER del Dockerfile.
USER $APP_UID

# El entrypoint recibe el comando como argumentos y lo ejecuta ya como $APP_UID.
# "exec" hace que .NET sea el PID 1 y reciba SIGTERM de Render.
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
CMD ["sh", "-c", "exec dotnet Rescauta.Api.dll --urls http://+:${PORT:-8080}"]
