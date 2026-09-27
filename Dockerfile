# =============================================================================
# Rescauta - Dockerfile multi-stage
#
# Deploy en Render:
#   Root Directory  : la raiz del repo (la que contiene esta Dockerfile y la carpeta Rescauta/)
#   Dockerfile Path : Dockerfile
#   Build Command   : (vacio - Render usa este Dockerfile)
#   Start Command   : (vacio - usa el ENTRYPOINT)
#
# -----------------------------------------------------------------------------
# POR QUE ESTE ARCHIVO NORMALIZA EL LAYOUT EN LA CAPA 0
#
# En Render, "Root Directory" decide a la vez DONDE se busca el Dockerfile y CUAL es el
# contexto de build. Si se deja en la raiz, el contexto contiene "Rescauta/Rescauta.sln".
# Si alguien lo cambia a "Rescauta" (porque ve la solucion ahi), el contexto PASa a ser la
# carpeta Rescauta/ y el archivo se llama "Rescauta.sln", sin carpeta delante. Con un
# Dockerfile de rutas fijas, el build revienta con:
#     COPY failed: file not found in build context: stat Rescauta/Rescauta.sln
#
# La capa 0 copia el contexto entero y lo aplana a /src segun detecte cual de los dos
# layouts llego. A partir de ahi, TODAS las rutas del Dockerfile son relativas a /src y son
# iguales en los dos casos. Asi el deploy no vuelve a depender de un ajuste de la UI.
#
# -----------------------------------------------------------------------------
# VERSIONES DE LAS IMAGENES
#
# La imagen SDK debe coincidir EXACTAMENTE con el TargetFramework de
# Rescauta/Directory.Build.props. Tras el merge de Donaciones ese archivo quedo en
# net10.0, asi que las imagenes van en 10.0. Si se vuelve a bajar el TFM a net8.0,
# hay que bajar ESTAS DOS imagenes a 8.0 o el build falla con NETSDK1045.
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: build. Se descarta; no llega a la imagen final.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# --- Capa 0: normalizar el layout ---------------------------------------------
COPY . /tmp/ctx

RUN set -eux; \
    if [ -f "/tmp/ctx/Rescauta/Rescauta.sln" ]; then \
        echo ">> Contexto = raiz del repo. Aplanando Rescauta/ hacia /src"; \
        cp -a /tmp/ctx/Rescauta/. /src/; \
    elif [ -f "/tmp/ctx/Rescauta.sln" ]; then \
        echo ">> Contexto = carpeta Rescauta/. Copiando directo a /src"; \
        cp -a /tmp/ctx/. /src/; \
    else \
        echo "!! No se encontro ninguna .sln. Rutas candidatas:"; \
        find /tmp/ctx -maxdepth 3 -name "*.sln" -print; \
        exit 1; \
    fi; \
    rm -rf /tmp/ctx; \
    # Los bin/obj del host se descartan: su project.assets.json apunta a rutas
    # absolutas de otra maquina y rompe el restore.
    find /src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + ; \
    ls -la /src

# --- Capa 1: solo los .csproj, para cachear el restore ------------------------
# El restore de NuGet solo depende de los .csproj. Mientras no cambien, Docker reutiliza
# esta capa y no vuelve a descargar paquetes. Copiar el codigo antes de restaurar
# haria que cada cambio de una sola linea invalidara el restore completo.
COPY Directory.Build.props Directory.Packages.props Rescauta.sln ./
COPY Rescauta.Domain/Rescauta.Domain.csproj Rescauta.Domain/
COPY Rescauta.Application/Rescauta.Application.csproj Rescauta.Application/
COPY Rescauta.Infrastructure/Rescauta.Infrastructure.csproj Rescauta.Infrastructure/
COPY Rescauta.Api/Rescauta.Api.csproj Rescauta.Api/

# Se restaura la SOLUCION entera, no un proyecto suelto, para que las referencias entre
# los 4 proyectos queden resueltas.
RUN dotnet restore Rescauta.sln

# --- Capa 2: el codigo fuente --------------------------------------------------
# Se copian carpeta por carpeta y no con "COPY . ." a proposito: un COPY de todo el
# contexto volveria a traer un arbol anidado Rescauta/ dentro de /src y duplicaria todo.
COPY Rescauta.Domain/ Rescauta.Domain/
COPY Rescauta.Application/ Rescauta.Application/
COPY Rescauta.Infrastructure/ Rescauta.Infrastructure/
COPY Rescauta.Api/ Rescauta.Api/

# --no-restore: el restore ya se resolvio en la capa 1 y no debe repetirse.
# UseAppHost=false: no genera el ejecutable nativo; en Linux el runtime ejecuta la DLL.
RUN dotnet publish Rescauta.Api/Rescauta.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 2: runtime. Imagen de runtime, no de SDK: sin compilador, sin Roslyn y sin
# package manager. Es la mayor reduccion de tamano posible.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

# --- Red ----------------------------------------------------------------------
# El "+" es imprescindible: con "http://:8080" Kestrel escucharia solo en loopback y
# Render no alcanzaria el contenedor.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# --- Usuario sin privilegios ---------------------------------------------------
# La imagen de runtime .NET define APP_UID. Ejecutar como root es innecesario: un
# incidente en la app no debe dar acceso al host.
USER $APP_UID

# El entrypoint apunta al ensamblado que produce el proyecto Rescauta.Api.
ENTRYPOINT ["dotnet", "Rescauta.Api.dll"]
