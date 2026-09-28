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
COPY Rescauta/Directory.Build.props Rescauta/Directory.Packages.props ./
COPY Rescauta/Rescauta.sln ./
COPY Rescauta/Rescauta.Domain/Rescauta.Domain.csproj Rescauta/Rescauta.Domain/
COPY Rescauta/Rescauta.Application/Rescauta.Application.csproj Rescauta/Rescauta.Application/
COPY Rescauta/Rescauta.Infrastructure/Rescauta.Infrastructure.csproj Rescauta/Rescauta.Infrastructure/
COPY Rescauta/Rescauta.Api/Rescauta.Api.csproj Rescauta/Rescauta.Api/

# Se restaura la SOLUCION, no un proyecto suelto, para que los 4 queden resueltos
# incluidas las referencias entre ellos.
RUN dotnet restore Rescauta/Rescauta.sln

# --- Capa 2: el codigo fuente ------------------------------------------------
# OJO con la ruta del publish. "COPY Rescauta/ ./" NO copia la CARPETA Rescauta dentro
# de /src, sino su CONTENIDO: /src/Rescauta.Api/, /src/Directory.Build.props, etc. Por
# eso el publish usa Rescauta.Api/... y no Rescauta/Rescauta.Api/... . Las dos rutas
# distintas del mismo fichero, en las dos lineas de arriba y en esta, no son un descuido:
# el restore de la capa 1 ocurre ANTES del "COPY Rescauta/ ./", cuando la carpeta todavia
# no existe dentro de /src.
COPY Rescauta/ ./

# --no-restore: el restore ya se hizo arriba y no debe repetirse.
# /p:UseAppHost=false: no genera el ejecutable nativo. En Linux se ejecuta la DLL con
# "dotnet", asi que el apphost solo anadiria peso.
RUN dotnet publish Rescauta.Api/Rescauta.Api.csproj \
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

# --- Carpeta de la base de datos ---------------------------------------------
# SQLite es UN ARCHIVO, y ese archivo tiene que estar en un sitio que el usuario de la
# imagen pueda escribir. Este paso es obligatorio, no cosmetico: la imagen base corre
# como root, /app queda propiedad de root, y mas abajo se ejecuta como $APP_UID. Sin
# mkdir+chown, el arranque falla con "SqliteException: unable to open database file" y
# la API se queda sin base de datos.
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data

# --- Base de datos por defecto ------------------------------------------------
# Esta cadena es la que se usa en el plan gratis de Render, donde NO hay disco: el
# archivo vive en el sistema de archivos efimero del contenedor. Sobrevive a los
# reinicios, pero NO a un redespliegue (ahi se vuelve a crear y la semilla corre de
# nuevo). Para que los datos persistan hay dos pasos, en este orden:
#   1) Poner el plan del servicio en "starter" y anadir el bloque "disk:" de
#      render.yaml, que monta un volumen en /var/data.
#   2) Cambiar esta variable a  Data Source=/var/data/rescauta.db
ENV Database__Provider=Sqlite
ENV Database__ConnectionString="Data Source=/app/data/rescauta.db"

# El esquema y los datos de arranque los crea la API al arrancar, en TODOS los entornos
# (ver Program.cs, seccion 11). Con esto en false arrancaria contra una base ya
# provisionada.
ENV Database__MigrateOnStartup=true
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

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
# La imagen de runtime .NET ya define APP_UID. Ejecutar como root es innecesario: un
# incidente en la app no da acceso al host.
USER $APP_UID

# "exec" reemplaza el shell por el proceso de .NET, para que sea el PID 1 y reciba las
# senales de parada de Render (SIGTERM) en vez de que las ignore el shell intermedio.
ENTRYPOINT ["sh", "-c", "exec dotnet Rescauta.Api.dll --urls http://+:${PORT:-8080}"]
