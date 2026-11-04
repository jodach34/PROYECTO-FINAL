#!/bin/sh
set -eu
umask 077

DATA_DIR="${DATA_DIR:-/app/data}"

ID_BIN=/usr/bin/id
CHOWN_BIN=/usr/bin/chown
CHMOD_BIN=/usr/bin/chmod
FIND_BIN=/usr/bin/find
MKDIR_BIN=/usr/bin/mkdir
SETPRIV_BIN=/usr/bin/setpriv
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

APP_UID_NUM=
APP_GID_NUM=

if [ -n "${APP_UID:-}" ]; then
    APP_UID_NUM="$("$ID_BIN" -u -- "$APP_UID")" || {
        echo "ERROR: APP_UID '$APP_UID' no existe o no se pudo resolver" >&2
        exit 1
    }

    case "$APP_UID_NUM" in
        ''|*[!0-9]*)
            echo "ERROR: APP_UID '$APP_UID' no resolvio a un UID numerico" >&2
            exit 1
            ;;
    esac

    if [ "$APP_UID_NUM" -eq 0 ]; then
        echo "ERROR: APP_UID no puede ser 0 (root)" >&2
        exit 1
    fi

    APP_GID_NUM="$("$ID_BIN" -g -- "$APP_UID")" || {
        echo "ERROR: no se pudo resolver el GID de APP_UID '$APP_UID'" >&2
        exit 1
    }

    case "$APP_GID_NUM" in
        ''|*[!0-9]*)
            echo "ERROR: APP_UID '$APP_UID' no resolvio a un GID numerico" >&2
            exit 1
            ;;
    esac

    if [ "$APP_GID_NUM" -eq 0 ]; then
        echo "ERROR: el GID de APP_UID '$APP_UID' no puede ser 0 (root)" >&2
        exit 1
    fi
fi

if [ "$CURRENT_UID" -eq 0 ]; then
    if [ -z "${APP_UID:-}" ]; then
        echo "ERROR: APP_UID no definido" >&2
        exit 1
    fi

    "$MKDIR_BIN" -p -- "$DATA_DIR"
    "$CHOWN_BIN" -R -- "$APP_UID_NUM:$APP_GID_NUM" "$DATA_DIR"
    "$CHMOD_BIN" 700 -- "$DATA_DIR"
    "$FIND_BIN" "$DATA_DIR" -maxdepth 1 -type f -name 'rescauta.db*' -exec "$CHMOD_BIN" 600 -- {} +

    if ! "$SETPRIV_BIN" \
        --reuid="$APP_UID_NUM" \
        --regid="$APP_GID_NUM" \
        --clear-groups \
        --inh-caps=-all \
        --bounding-set=-all \
        --no-new-privs \
        -- "$SH_BIN" -c '
            f="$1/.write-test.$$"
            : > "$f"
            "$2" -f -- "$f"
        ' sh "$DATA_DIR" "$RM_BIN"
    then
        echo "ERROR: el UID $APP_UID_NUM no puede escribir en $DATA_DIR" >&2
        exit 1
    fi

    exec "$SETPRIV_BIN" \
        --reuid="$APP_UID_NUM" \
        --regid="$APP_GID_NUM" \
        --clear-groups \
        --inh-caps=-all \
        --bounding-set=-all \
        --no-new-privs \
        -- "$@"
fi

if [ ! -r "$DATA_DIR" ] || [ ! -w "$DATA_DIR" ] || [ ! -x "$DATA_DIR" ]; then
    echo "ERROR: $DATA_DIR no es legible/escribible/ejecutable por el UID $CURRENT_UID" >&2
    exit 1
fi

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
