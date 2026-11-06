/*
    Cliente SignalR del MVC contra el hub de la API.

    TOPICO: WEBSOCKET / TIEMPO REAL.

    Por que hace falta un hub y no un fetch cada 5 segundos:
      fetch es HTTP. El servidor contesta, se cierra el socket y el navegador tiene que
      preguntar otra vez: "polling". Son 200 peticiones por minuto para no enterarse de nada.
      Un WebSocket es un socket que queda abierto en las DOS direcciones: el servidor empuja
      el evento en el instante en que ocurre, y el cliente no pregunta. Para las alertas de
      emergencia de un comedor eso es la diferencia entre enterarse al instante y 5 segundos
      despues.

    SignalR es el protocolo de .NET para eso. Por encima del WebSocket pone:
      * reconexion automatica con espera creciente (una red que se cae no rompe la pantalla),
      * negociacion del transporte: WebSocket, y si el proxy lo bloquea, cae a Server-Sent
        Events o Long Polling sin cambiar una linea de este archivo,
      * negociacion de version del protocolo,
      * "groups", para que una alerta de guardia vaya solo a quien esta de turno.

    Este archivo NO usa un hub propio: se suscribe al MISMO hub de la API
    (/hubs/rescauta) que usan los modulos Kardex, Mapas y Donaciones. Una sola conexion por
    pestana aunque haya tres modulos abiertos.

    Nota: el hub se sirve desde la API, no desde el MVC, asi que la URL se pasa por
    data-attributes en el HTML en vez de estar escrita aqui. Si esa URL cambia (por ejemplo
    al desplegar), no hay que tocar este archivo.
*/
(function () {
    'use strict';

    // Rutas de los eventos servidor -> cliente. Deben coincidir con los que emite la API
    // (ver Rescauta.Infrastructure/Hubs/RescautaHub.cs y SignalRInventoryNotifier).
    const EVENTO_ECHO = 'updateReceived';
    const EVENTO_KARDEX = 'kardex.movimiento.registrado';
    const EVENTO_INVENTARIO = 'inventario.actualizado';
    const EVENTO_DONACION = 'donacion.registrada';

    function log(element, texto, clase) {
        if (!element) {
            return;
        }

        const linea = document.createElement('li');
        linea.className = 'flex items-start gap-2 py-1.5 border-b border-gray-100 last:border-0';
        linea.innerHTML =
            '<span class="text-[10px] text-gray-400 font-mono flex-none mt-0.5 w-14">' +
            new Date().toLocaleTimeString('es-PE') +
            '</span><span class="text-xs ' + (clase || 'text-ink') + ' break-all">' + texto + '</span>';

        element.prepend(linea);

        // El log no crece sin limite: en una sesion larga de una jornada entera son miles de
        // lineas en el DOM, y el navegador se arrastra por mantenerlas todas.
        while (element.childElementCount > 40) {
            element.removeChild(element.lastElementChild);
        }
    }

    function setEstado(badge, texto, clase) {
        if (!badge) {
            return;
        }

        badge.textContent = texto;
        badge.className = 'px-2.5 py-1 rounded-md text-[10px] font-extrabold uppercase tracking-wide ' + clase;
    }

    function iniciar() {
        const raiz = document.getElementById('hub-panel');

        if (!raiz) {
            return;
        }

        const urlHub = raiz.dataset.hubUrl;
        const apiBase = raiz.dataset.apiBase;

        if (!urlHub) {
            setEstado(
                document.getElementById('hub-estado'),
                'Sin URL del hub',
                'bg-critico/10 text-critico border border-critico/25');

            return;
        }

        const badge = document.getElementById('hub-estado');
        const lista = document.getElementById('hub-eventos');
        const boton = document.getElementById('hub-probar');
        const botonTurno = document.getElementById('hub-turno');

        // ------------------------------------------------------------------
        // Sin la libreria, no se puede negociar el protocolo. Se avisa en vez
        // de fallar en silencio: un boton que no hace nada parece un bug.
        // ------------------------------------------------------------------
        if (typeof signalR === 'undefined') {
            setEstado(badge, 'Falta signalr.js', 'bg-critico/10 text-critico border border-critico/25');
            log(lista, 'No se pudo cargar la libreria signalR.js del CDN.', 'text-critico');

            return;
        }

        setEstado(badge, 'Conectando...', 'bg-alerta/10 text-alerta border border-alerta/25');

        // ------------------------------------------------------------------
        // La conexion se construye con withUrl: la URL ya trae /hubs/rescauta.
        // ------------------------------------------------------------------
        const conexion = new signalR.HubConnectionBuilder()
            .withUrl(urlHub)
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // ------------------------------------------------------------------
        // Reconexion visible. onreconnecting/onreconnected/onclose Dan los
        // estados intermedios, que si no se ignoran dejan la pantalla con un
        // "conectado" que ya no lo esta.
        // ------------------------------------------------------------------
        conexion.onreconnecting(error => {
            setEstado(badge, 'Reconectando...', 'bg-alerta/10 text-alerta border border-alerta/25');
            log(lista, 'Se perdio la conexion, reintentando. ' + (error ? error.message : ''), 'text-alerta');
        });

        conexion.onreconnected(id => {
            setEstado(badge, 'Reconectado', 'bg-ok/10 text-ok border border-ok/25');
            log(lista, 'Reconectado con el id de conexion ' + id + '.', 'text-ok');

            // La pertenencia a un grupo NO sobrevive a la reconexion: el servidor tiene una
            // conexion nueva, sin los grupos de la anterior. Por eso hay que volver a
            // unirse al turno, o la alerta de emergencia dejaria de llegar sin dar error.
            if (botonTurno && botonTurno.dataset.unido === 'true') {
                conexion.invoke('Kardex_UnirseAlTurno').catch(() => { });
            }
        });

        conexion.onclose(error => {
            setEstado(badge, 'Desconectado', 'bg-critico/10 text-critico border border-critico/25');
            log(lista, 'Conexion cerrada. ' + (error ? error.message : 'Motivo no informado.'), 'text-critico');
        });

        // ------------------------------------------------------------------
        // Eventos servidor -> cliente. on() es suscripcion; se puede llamar
        // con el mismo nombre las veces que se quiera.
        //
        // El manejador recibe el payload YA deserializado al tipo que declara
        // el servidor, porque el hub lo serializa con la convencion de .NET.
        // ------------------------------------------------------------------
        conexion.on(EVENTO_ECHO, payload => {
            log(
                lista,
                'updateReceived: ' + (payload ? (payload.message || '(sin mensaje)') : '(vacio)'),
                'text-ink');
        });

        conexion.on(EVENTO_KARDEX, payload => {
            log(
                lista,
                'kardex.movimiento.registrado: ' +
                (payload ? (payload.insumoNombre + ' ' + payload.cantidad + ' ' + payload.unidadMedida) : '(vacio)'),
                'text-brand font-semibold');
        });

        conexion.on(EVENTO_INVENTARIO, payload => {
            log(lista, 'inventario.actualizado: ' + JSON.stringify(payload), 'text-brand font-semibold');
        });

        conexion.on(EVENTO_DONACION, payload => {
            log(lista, 'donacion.registrada: ' + JSON.stringify(payload), 'text-ok font-semibold');
        });

        conexion.start()
            .then(() => {
                setEstado(badge, 'Conectado (WebSocket)', 'bg-ok/10 text-ok border border-ok/25');
                log(lista, 'Conectado al hub ' + urlHub + '.', 'text-ok');

                raiz.dataset.conectado = 'true';
            })
            .catch(error => {
                // Falla típica al probar en local: la API no esta levantada, o el hub quedo
                // en otra ruta. El mensaje se muestra tal cual porque es la unica pista de
                // por que no hay tiempo real.
                setEstado(badge, 'Error de conexion', 'bg-critico/10 text-critico border border-critico/25');
                log(lista, 'No se pudo conectar: ' + error.message, 'text-critico');

                raiz.dataset.conectado = 'false';
            });

        // ------------------------------------------------------------------
        // Cliente -> servidor.
        // ------------------------------------------------------------------
        if (boton) {
            boton.addEventListener('click', () => {
                if (!raiz.dataset.conectado) {
                    log(lista, 'No hay conexion: levanta la API con "dotnet run".', 'text-alerta');

                    return;
                }

                // invoke = "llama a este metodo del hub y espera su Task". La API responde
                // con el mismo mensaje por updateReceived, que es lo que aparece en la lista.
                conexion
                    .invoke('SendUpdate', 'Prueba desde el navegador ' + new Date().toLocaleTimeString('es-PE'))
                    .catch(error => log(lista, 'Fallo al invocar SendUpdate: ' + error.message, 'text-critico'));
            });
        }

        if (botonTurno) {
            botonTurno.addEventListener('click', () => {
                if (!raiz.dataset.conectado) {
                    log(lista, 'No hay conexion: no se puede cambiar de turno.', 'text-alerta');

                    return;
                }

                const unido = botonTurno.dataset.unido === 'true';
                const metodo = unido ? 'Kardex_SalirDelTurno' : 'Kardex_UnirseAlTurno';

                conexion
                    .invoke(metodo)
                    .then(() => {
                        botonTurno.dataset.unido = unido ? 'false' : 'true';
                        botonTurno.textContent = unido ? 'Unirse al turno' : 'Salir del turno';
                        botonTurno.className = unido
                            ? 'px-3 py-1.5 rounded-lg text-[10px] font-extrabold uppercase tracking-wide bg-brand text-white hover:bg-brand-dk transition-colors'
                            : 'px-3 py-1.5 rounded-lg text-[10px] font-extrabold uppercase tracking-wide bg-white text-ink border border-gray-300 hover:bg-canvas transition-colors';

                        log(
                            lista,
                            unido ? 'Saliste del grupo de guardia.' : 'Te uniste al grupo de guardia: ahora recibes las alertas.',
                            'text-brand font-semibold');
                    })
                    .catch(error => log(lista, 'Fallo al cambiar de turno: ' + error.message, 'text-critico'));
            });
        }

        // ------------------------------------------------------------------
        // Boton "mover el inventario": comprueba el circuito completo.
        //   navegador --POST /api/v1/donaciones--> API --evento--> hub --WebSocket--> este panel
        // Si el alta aparece sola en la lista, los cuatro topics de HTTP metodo, API,
        // base de datos y WebSocket estan funcionando a la vez.
        // ------------------------------------------------------------------
        const botonMover = document.getElementById('hub-mover');

        if (botonMover) {
            botonMover.addEventListener('click', async () => {
                botonMover.disabled = true;

                try {
                    const respuesta = await fetch(apiBase + 'api/v1/donaciones', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            insumoId: botonMover.dataset.insumoId,
                            cantidad: 10,
                            donante: 'Prueba WebSocket',
                            puntoRecojo: 'Deposito central'
                        })
                    });

                    if (!respuesta.ok) {
                        throw new Error('HTTP ' + respuesta.status);
                    }

                    const donacion = await respuesta.json();
                    log(lista, 'POST /donaciones -> 201 Created, codigo ' + donacion.codigoSeguimiento, 'text-ok font-semibold');
                } catch (error) {
                    log(lista, 'Fallo el POST de donacion: ' + error.message, 'text-critico');
                } finally {
                    botonMover.disabled = false;
                }
            });
        }
    }

    // El DOM tiene que estar listo antes de buscar los ids: el script va con defer, pero
    // conviene no depender de eso.
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})();
