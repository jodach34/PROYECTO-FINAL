#!/bin/sh
set -eu
umask 077

# /app/data se crea y se asigna a APP_UID durante el BUILD de la imagen. En Render
# Free no hay disco persistente ni privilegios para cambiar propietarios al arrancar,
# asi que este script YA NO usa setpriv, chown ni chmod: solo comprueba que el
# usuario no root de la imagen puede escribir y cede el control a la API.
DATA_DIR="${DATA_DIR:-/app/data}"

ID_BIN=/usr/bin/id
FIND_BIN=/usr/bin/find
SH_BIN=/bin/sh
RM_BIN=/bin/rm

case "$DATA_DIR" in
    *[!/]*) ;;
    *)
        echo "ERROR: DATA_DIR no puede ser /" >&2
        exit 1
        ;;
esac

case "/$DATA_DIR/" in
    *"/../"*)
        echo "ERROR: DATA_DIR no puede contener .." >&2
        exit 1
        ;;
esac

case "$DATA_DIR" in
    /*) ;;
    *)
        echo "ERROR: DATA_DIR debe ser una ruta absoluta" >&2
        exit 1
        ;;
esac

if [ -L "$DATA_DIR" ]; then
    echo "ERROR: DATA_DIR no puede ser un symlink" >&2
    exit 1
fi

CURRENT_UID="$("$ID_BIN" -u)" || {
    echo "ERROR: no se pudo ejecutar $ID_BIN -u" >&2
    exit 1
}

case "$CURRENT_UID" in
    ''|*[!0-9]*)
        echo "ERROR: $ID_BIN -u devolvio un UID no numerico: '$CURRENT_UID'" >&2
        exit 1
        ;;
esac

# El Dockerfile termina con USER $APP_UID. Si alguien lo cambia a root, no se intenta
# bajar privilegios (Render Free no lo permite): se falla con una causa explicita.
if [ "$CURRENT_UID" -eq 0 ]; then
    echo "ERROR: el contenedor arranco como root; debe ejecutarse con USER \$APP_UID" >&2
    echo "ERROR: /app/data se prepara en la imagen y no se cambian privilegios al arrancar" >&2
    exit 1
fi

if [ ! -d "$DATA_DIR" ]; then
    echo "ERROR: $DATA_DIR no existe; debe crearse durante el build de la imagen" >&2
    exit 1
fi

if [ ! -r "$DATA_DIR" ] || [ ! -w "$DATA_DIR" ] || [ ! -x "$DATA_DIR" ]; then
    echo "ERROR: $DATA_DIR no es legible/escribible/ejecutable por el UID $CURRENT_UID" >&2
    exit 1
fi

WRITE_TEST="$DATA_DIR/.write-test.$$"
if ! { : > "$WRITE_TEST"; } 2>/dev/null; then
    echo "ERROR: el UID $CURRENT_UID no puede escribir en $DATA_DIR" >&2
    exit 1
fi
"$RM_BIN" -f -- "$WRITE_TEST"

"$FIND_BIN" "$DATA_DIR" -maxdepth 1 -type f -name 'rescauta.db*' -exec "$SH_BIN" -c '
    uid="$1"
    shift
    for f do
        if [ ! -r "$f" ] || [ ! -w "$f" ]; then
            echo "ERROR: $f no es legible/escribible por el UID $uid" >&2
            exit 1
        fi
    done
' sh "$CURRENT_UID" {} +

exec "$@"
