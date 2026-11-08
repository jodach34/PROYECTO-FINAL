(function () {
  "use strict";

  const select = (selector, scope) => (scope || document).querySelector(selector);
  const selectAll = (selector, scope) => Array.from((scope || document).querySelectorAll(selector));

  function initNavigation() {
    const toggle = select("[data-nav-toggle]");
    const menu = select("[data-mobile-nav]");

    if (toggle && menu) {
      toggle.addEventListener("click", function () {
        const open = menu.classList.toggle("is-open");
        toggle.setAttribute("aria-expanded", String(open));
        menu.hidden = !open;
      });
    }

    const sidebarToggle = select("[data-sidebar-toggle]");
    const sidebar = select("[data-sidebar]");
    const backdrop = select("[data-sidebar-backdrop]");

    const closeSidebar = function () {
      if (!sidebar) return;

      sidebar.classList.remove("is-open");
      if (backdrop) backdrop.classList.remove("is-open");
      document.body.classList.remove("rs-no-scroll");
    };

    if (sidebarToggle && sidebar) {
      sidebarToggle.addEventListener("click", function () {
        const open = sidebar.classList.toggle("is-open");
        if (backdrop) backdrop.classList.toggle("is-open", open);
        document.body.classList.toggle("rs-no-scroll", open);
        sidebarToggle.setAttribute("aria-expanded", String(open));
      });
    }

    if (backdrop) backdrop.addEventListener("click", closeSidebar);

    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape") {
        closeSidebar();

        if (menu && menu.classList.contains("is-open")) {
          menu.classList.remove("is-open");
          menu.hidden = true;
          if (toggle) toggle.setAttribute("aria-expanded", "false");
        }
      }
    });
  }

  const MAP_ICON_SVG = '<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 12h16"/><path d="M5 12a7 7 0 0 0 14 0"/><path d="M9 16h6"/></svg>';

  const mapColor = function (color) {
    return ["ok", "alerta", "critico", "muted"].indexOf(color) === -1 ? "muted" : color;
  };

  const createMapIcon = function (color) {
    const safeColor = mapColor(color);

    return L.divIcon({
      className: "rs-map-marker",
      html: '<div class="rs-map-marker__pin rs-map-marker__pin--' + safeColor + '">' +
        '<span class="rs-map-marker__dot">' + MAP_ICON_SVG + "</span>" +
        '<span class="rs-map-marker__stem"></span>' +
        "</div>",
      iconSize: [36, 44],
      iconAnchor: [18, 44],
      popupAnchor: [0, -38]
    });
  };

  const createMapPopup = function (comedor) {
    const popup = document.createElement("div");
    popup.className = "rs-map-popup";

    const title = document.createElement("strong");
    title.className = "rs-map-popup__title";
    title.textContent = comedor.nombre || "Comedor";

    const meta = document.createElement("span");
    meta.className = "rs-map-popup__meta";
    meta.textContent = (comedor.distrito || "Sin distrito") + " · " +
      (Number(comedor.raciones) || 0) + " raciones/día";

    const footer = document.createElement("span");
    footer.className = "rs-map-popup__footer";

    const badge = document.createElement("span");
    badge.className = "rs-badge rs-badge--" + mapColor(comedor.color);
    badge.textContent = comedor.etiqueta || "Sin datos";

    const link = document.createElement("a");
    link.className = "rs-map-popup__link";
    link.href = comedor.url || "#";
    link.textContent = "Ver ficha →";

    footer.appendChild(badge);
    footer.appendChild(link);
    popup.appendChild(title);
    popup.appendChild(meta);
    popup.appendChild(footer);

    return popup;
  };

  function initMaps() {
    selectAll("[data-map]").forEach(function (map) {
      const canvas = select("[data-map-canvas]", map);
      const dataNode = select("[data-map-data]", map);
      const zoomValue = select("[data-map-zoom]", map);
      const zoomIn = select("[data-map-zoom-in]", map);
      const zoomOut = select("[data-map-zoom-out]", map);
      const reset = select("[data-map-reset]", map);

      if (!canvas || !dataNode || typeof L === "undefined") {
        return;
      }

      let comedores = [];

      try {
        comedores = JSON.parse(dataNode.textContent || "[]");
      } catch (error) {
        comedores = [];
      }

      const leafletMap = L.map(canvas, {
        zoomControl: false,
        scrollWheelZoom: false,
        attributionControl: true,
        minZoom: 5,
        maxZoom: 19
      }).setView([-12.0464, -77.0428], 11);

      map._rescautaLeafletMap = leafletMap;

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
      }).addTo(leafletMap);

      const bounds = L.latLngBounds([]);

      comedores.forEach(function (comedor) {
        const lat = Number(comedor.lat);
        const lng = Number(comedor.lng);

        if (!isFinite(lat) || !isFinite(lng) || (lat === 0 && lng === 0)) {
          return;
        }

        const marker = L.marker([lat, lng], {
          icon: createMapIcon(comedor.color),
          title: comedor.nombre,
          alt: comedor.nombre,
          keyboard: true
        });

        marker.bindPopup(createMapPopup(comedor), { maxWidth: 300 });
        marker.addTo(leafletMap);

        const element = marker.getElement();

        if (element) {
          element.dataset.mapPin = "";
          element.dataset.comedorId = String(comedor.id || "");
          element.dataset.estado = comedor.estado || "";
          element.dataset.zona = comedor.distrito || "";
          element.dataset.search = (comedor.nombre || "") + " " + (comedor.distrito || "");
          element.setAttribute("aria-label", (comedor.nombre || "Comedor") + ", " +
            (comedor.distrito || "sin distrito") + ". " + (comedor.etiqueta || "Sin datos"));
        }

        bounds.extend([lat, lng]);
      });

      let initialZoom = leafletMap.getZoom();

      const syncZoomLabel = function () {
        if (!zoomValue) return;

        const percentage = Math.round(Math.pow(2, leafletMap.getZoom() - initialZoom) * 100);
        zoomValue.textContent = percentage + "%";
      };

      const fitToMarkers = function () {
        if (bounds.isValid()) {
          leafletMap.fitBounds(bounds, { padding: [38, 38], maxZoom: 14 });
        } else {
          leafletMap.setView([-12.0464, -77.0428], 11);
        }

        initialZoom = leafletMap.getZoom();
        syncZoomLabel();
      };

      fitToMarkers();
      leafletMap.on("zoomend", syncZoomLabel);

      if (zoomIn) {
        zoomIn.addEventListener("click", function () {
          leafletMap.zoomIn();
        });
      }

      if (zoomOut) {
        zoomOut.addEventListener("click", function () {
          leafletMap.zoomOut();
        });
      }

      if (reset) {
        reset.addEventListener("click", function () {
          leafletMap.closePopup();
          fitToMarkers();
        });
      }

      window.addEventListener("resize", function () {
        leafletMap.invalidateSize();
      });

      window.setTimeout(function () {
        leafletMap.invalidateSize();
      }, 250);
    });
  }

  function initOriginPickers() {
    selectAll("[data-origin-picker]").forEach(function (picker) {
      const canvas = select("[data-origin-canvas]", picker);
      const latInput = select("[data-origin-lat]", picker);
      const lngInput = select("[data-origin-lng]", picker);
      const status = select("[data-origin-status]", picker);
      const clear = select("[data-origin-clear]", picker);

      if (!canvas || !latInput || !lngInput || typeof L === "undefined") {
        return;
      }

      const readInput = function (input, limite) {
        const raw = (input.value || "").trim();

        if (!raw) return null;

        const value = Number(raw);

        return isFinite(value) && Math.abs(value) <= limite ? value : null;
      };

      const setStatus = function (text) {
        if (status) status.textContent = text;
      };

      const leafletMap = L.map(canvas, {
        scrollWheelZoom: false,
        minZoom: 5,
        maxZoom: 19
      });

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
      }).addTo(leafletMap);

      let marker = null;

      const writePoint = function (latlng) {
        const lat = Number(latlng.lat);
        const lng = Number(latlng.lng);

        if (!isFinite(lat) || !isFinite(lng) ||
            Math.abs(lat) > 90 || Math.abs(lng) > 180) {
          return;
        }

        latInput.value = lat.toFixed(6);
        lngInput.value = lng.toFixed(6);

        if (!marker) {
          marker = L.marker([lat, lng], { draggable: true }).addTo(leafletMap);
          marker.on("dragend", function () {
            writePoint(marker.getLatLng());
          });
        } else {
          marker.setLatLng([lat, lng]);
        }

        setStatus("Ubicación marcada: " + lat.toFixed(5) + ", " + lng.toFixed(5) + ".");
      };

      leafletMap.on("click", function (event) {
        writePoint(event.latlng);
      });

      const lat = readInput(latInput, 90);
      const lng = readInput(lngInput, 180);

      if (lat !== null && lng !== null && !(lat === 0 && lng === 0)) {
        writePoint({ lat: lat, lng: lng });
        leafletMap.setView([lat, lng], 15);
      } else {
        leafletMap.setView([-12.0464, -77.0428], 11);
        setStatus("Sin ubicación marcada. Haz clic en el mapa para marcarla.");
      }

      if (clear) {
        clear.addEventListener("click", function () {
          latInput.value = "";
          lngInput.value = "";

          if (marker) {
            leafletMap.removeLayer(marker);
            marker = null;
          }

          setStatus("Sin ubicación marcada. Haz clic en el mapa para marcarla.");
        });
      }

      window.addEventListener("resize", function () {
        leafletMap.invalidateSize();
      });

      window.setTimeout(function () {
        leafletMap.invalidateSize();
      }, 250);
    });
  }

  function initRouteMaps() {
    selectAll("[data-route-map]").forEach(function (map) {
      const canvas = select("[data-route-canvas]", map);
      const endpoint = map.getAttribute("data-route-endpoint");
      const status = select("[data-route-status]", map);
      const panel = map.closest("[data-route-panel]") || map;
      const alert = select("[data-route-alert]", panel);

      if (!canvas || !endpoint || typeof L === "undefined") {
        return;
      }

      const setStatus = function (text) {
        if (status) status.textContent = text;
      };

      const showAlert = function (text) {
        if (!alert) return;

        alert.textContent = text || "";
        alert.hidden = !text;
      };

      setStatus("Calculando ruta…");

      const leafletMap = L.map(canvas, {
        scrollWheelZoom: false,
        minZoom: 5,
        maxZoom: 19
      }).setView([-12.0464, -77.0428], 11);

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
      }).addTo(leafletMap);

      const layers = L.layerGroup().addTo(leafletMap);
      const bounds = L.latLngBounds([]);

      const readPoint = function (raw) {
        if (!raw) return null;

        const lat = Number(raw.lat);
        const lng = Number(raw.lng);

        if (!isFinite(lat) || !isFinite(lng) ||
            Math.abs(lat) > 90 || Math.abs(lng) > 180 ||
            (lat === 0 && lng === 0)) {
          return null;
        }

        return { lat: lat, lng: lng, nombre: raw.nombre || "" };
      };

      const addPoint = function (value, color) {
        if (!value) return;

        const marker = L.circleMarker([value.lat, value.lng], {
          radius: 8,
          color: "#ffffff",
          weight: 2,
          fillColor: color,
          fillOpacity: 1
        }).addTo(layers);

        marker.bindTooltip(value.nombre || "Punto", { direction: "top", offset: [0, -8] });
        bounds.extend([value.lat, value.lng]);
      };

      const formatNumber = function (value, decimals) {
        const number = Number(value);

        if (!isFinite(number)) return "0";

        return number.toLocaleString("es-PE", {
          minimumFractionDigits: decimals,
          maximumFractionDigits: decimals
        });
      };

      const pintar = function (data) {
        const origen = readPoint(data.origen);
        const destino = readPoint(data.destino);
        const geometria = Array.isArray(data.geometria)
          ? data.geometria
            .filter(function (coordenada) {
              return Array.isArray(coordenada) && coordenada.length >= 2 &&
                isFinite(Number(coordenada[0])) && isFinite(Number(coordenada[1]));
            })
            .map(function (coordenada) {
              return [Number(coordenada[0]), Number(coordenada[1])];
            })
          : [];

        addPoint(origen, "#1b6b4a");
        addPoint(destino, "#0f3d2e");

        if (data.disponible && geometria.length > 1) {
          const linea = L.polyline(geometria, {
            color: "#1b6b4a",
            weight: 5,
            opacity: 0.85,
            lineCap: "round",
            lineJoin: "round"
          }).addTo(layers);

          bounds.extend(linea.getBounds());
        }

        if (bounds.isValid()) {
          leafletMap.fitBounds(bounds, { padding: [32, 32], maxZoom: 15 });
        } else if (origen) {
          leafletMap.setView([origen.lat, origen.lng], 14);
        } else if (destino) {
          leafletMap.setView([destino.lat, destino.lng], 14);
        }

        if (data.disponible) {
          setStatus("Ruta por carretera: " + formatNumber(data.distanciaKm, 1) +
            " km · " + formatNumber(data.duracionMinutos, 0) + " min aprox.");
          showAlert("");
        } else {
          setStatus("Ruta no disponible.");
          showAlert(data.mensaje || "No se pudo calcular la ruta.");
        }
      };

      fetch(endpoint, {
        headers: { "Accept": "application/json" },
        credentials: "same-origin"
      })
        .then(function (response) {
          if (!response.ok) {
            throw new Error("HTTP " + response.status);
          }

          return response.json();
        })
        .then(pintar)
        .catch(function () {
          setStatus("Ruta no disponible.");
          showAlert("No se pudo consultar la ruta. La donación registrada sigue siendo válida.");
        });

      window.addEventListener("resize", function () {
        leafletMap.invalidateSize();
      });

      window.setTimeout(function () {
        leafletMap.invalidateSize();
      }, 250);
    });
  }

  function initDirectory() {
    selectAll("[data-directory]").forEach(function (directory) {
      const search = select("[data-directory-search]", directory);
      const statusFilter = select("[data-directory-status]", directory);
      const zoneFilter = select("[data-directory-zone]", directory);
      const count = select("[data-directory-count]", directory);
      const empty = select("[data-directory-empty]", directory);
      const layout = select("[data-directory-layout]", directory);
      const cards = selectAll("[data-comedor]", directory);
      const viewButtons = selectAll("[data-view]", directory);
      const pins = selectAll("[data-map-pin]", directory);

      const sync = function () {
        const term = (search ? search.value : "").trim().toLowerCase();
        const status = statusFilter ? statusFilter.value : "";
        const zone = zoneFilter ? zoneFilter.value : "";
        let visible = 0;

        cards.forEach(function (card) {
          const text = (card.getAttribute("data-search") || "").toLowerCase();
          const cardStatus = card.getAttribute("data-estado") || "";
          const cardZone = card.getAttribute("data-zona") || "";
          const matches = (!term || text.indexOf(term) !== -1) &&
            (!status || cardStatus === status) &&
            (!zone || cardZone === zone);

          card.hidden = !matches;
          if (matches) visible += 1;
        });

        pins.forEach(function (pin) {
          const pinStatus = pin.getAttribute("data-estado") || "";
          const pinZone = pin.getAttribute("data-zona") || "";
          const pinName = (pin.getAttribute("data-search") || "").toLowerCase();
          const matches = (!term || pinName.indexOf(term) !== -1) &&
            (!status || pinStatus === status) &&
            (!zone || pinZone === zone);

          pin.hidden = !matches;
        });

        if (count) {
          count.textContent = visible === 1
            ? "1 comedor visible"
            : visible + " comedores visibles";
        }

        if (empty) empty.hidden = visible !== 0;
      };

      [search, statusFilter, zoneFilter].forEach(function (control) {
        if (control) {
          control.addEventListener("input", sync);
          control.addEventListener("change", sync);
        }
      });

      viewButtons.forEach(function (button) {
        button.addEventListener("click", function () {
          const view = button.getAttribute("data-view");

          viewButtons.forEach(function (other) {
            other.classList.toggle("is-active", other === button);
            other.setAttribute("aria-pressed", String(other === button));
          });

          if (layout) {
            layout.classList.remove("view-map", "view-list", "view-split");
            layout.classList.add("view-" + view);

            const mapElement = select("[data-map]", directory);

            if (mapElement && mapElement._rescautaLeafletMap) {
              window.setTimeout(function () {
                mapElement._rescautaLeafletMap.invalidateSize();
              }, 60);
            }

            if (view === "map" && window.innerWidth < 1024) {
              const map = select("[data-directory-map]", directory);
              if (map) map.scrollIntoView({ behavior: "smooth", block: "start" });
            }
          }
        });
      });

      sync();
    });
  }

  function initProductLookup() {
    selectAll("[data-product-lookup]").forEach(function (lookup) {
      const endpoint = lookup.getAttribute("data-product-endpoint");
      const input = select("[data-barcode-input]", lookup);
      const button = select("[data-barcode-button]", lookup);
      const status = select("[data-barcode-status]", lookup);
      const result = select("[data-barcode-result]", lookup);
      const form = lookup.closest("form");
      const nameInput = form ? select("[name='Nombre']", form) : null;

      if (!endpoint || !input || !button) {
        return;
      }

      const setStatus = function (text, isError) {
        if (!status) return;

        status.textContent = text || "";
        status.hidden = !text;
        status.style.color = isError ? "#b33a48" : "";
      };

      const setText = function (selector, value) {
        const node = result ? select(selector, result) : null;

        if (node) node.textContent = value || "";
      };

      const setBlock = function (selector, visible) {
        const node = result ? select(selector, result) : null;

        if (node) node.hidden = !visible;
      };

      const clearResult = function () {
        if (!result) return;

        result.hidden = true;
        setText("[data-product-name]", "");
        setText("[data-product-brand]", "");
        setText("[data-product-quantity]", "");
        setText("[data-product-ingredients]", "");
        setBlock("[data-product-brand-line]", false);
        setBlock("[data-product-quantity-line]", false);
        setBlock("[data-product-ingredients-block]", false);
        setBlock("[data-product-nutrition-block]", false);

        const image = select("[data-product-image]", result);
        if (image) {
          image.hidden = true;
          image.removeAttribute("src");
        }

        const nutrition = select("[data-product-nutrition]", result);
        if (nutrition) nutrition.textContent = "";
      };

      const render = function (data) {
        if (!result) return;

        result.hidden = false;
        setText("[data-product-name]", data.nombre);
        setText("[data-product-brand]", data.marca);
        setText("[data-product-quantity]", data.cantidad);
        setText("[data-product-ingredients]", data.ingredientes);

        setBlock("[data-product-brand-line]", Boolean(data.marca));
        setBlock("[data-product-quantity-line]", Boolean(data.cantidad));
        setBlock("[data-product-ingredients-block]", Boolean(data.ingredientes));

        const imagen = select("[data-product-image]", result);
        if (imagen) {
          if (data.imagenUrl) {
            imagen.alt = data.nombre ? "Imagen de " + data.nombre : "Imagen del producto";
            imagen.onerror = function () {
              imagen.hidden = true;
            };
            imagen.src = data.imagenUrl;
            imagen.hidden = false;
          } else {
            imagen.hidden = true;
            imagen.removeAttribute("src");
          }
        }

        const nutricion = select("[data-product-nutrition]", result);
        const filas = Array.isArray(data.nutricion) ? data.nutricion : [];

        if (nutricion) {
          nutricion.textContent = "";

          filas.forEach(function (fila) {
            const item = document.createElement("li");
            item.textContent = fila.etiqueta + ": " + fila.valor;
            nutricion.appendChild(item);
          });
        }

        setBlock("[data-product-nutrition-block]", filas.length > 0);
      };

      const buscar = async function () {
        if (button.disabled) return;

        const codigo = (input.value || "").replace(/[\s-]/g, "");

        if (!/^[0-9]{8,14}$/.test(codigo)) {
          clearResult();
          setStatus("Ingresa un codigo de barras numerico de 8 a 14 digitos.", true);
          input.focus();
          return;
        }

        const textoOriginal = button.textContent;

        button.disabled = true;
        button.textContent = "Buscando…";
        setStatus("Consultando Open Food Facts…", false);
        clearResult();

        try {
          const respuesta = await fetch(
            endpoint + "?codigo=" + encodeURIComponent(codigo),
            {
              headers: { Accept: "application/json" },
              credentials: "same-origin"
            });

          if (!respuesta.ok) {
            throw new Error("HTTP " + respuesta.status);
          }

          const data = await respuesta.json();

          if (data && data.encontrado) {
            if (data.nombre && nameInput) {
              nameInput.value = data.nombre;
              nameInput.dispatchEvent(new Event("input", { bubbles: true }));
            }

            render(data);
            setStatus(data.mensaje || "Producto encontrado. Revisa los datos antes de guardar.", false);
          } else {
            clearResult();
            setStatus(
              data && data.mensaje
                ? data.mensaje
                : "No se encontro el producto. Puedes registrarlo manualmente.",
              true);
          }
        } catch (error) {
          clearResult();
          setStatus(
            "No se pudo conectar con Open Food Facts. Puedes registrar el insumo manualmente.",
            true);
        } finally {
          button.disabled = false;
          button.textContent = textoOriginal;
        }
      };

      button.addEventListener("click", buscar);
      input.addEventListener("keydown", function (event) {
        if (event.key === "Enter") {
          event.preventDefault();
          buscar();
        }
      });
    });
  }

  function initChatbot() {
    const root = select("[data-chatbot]");

    if (!root) return;

    const panel = select("[data-chatbot-panel]", root);
    const toggle = select("[data-chatbot-toggle]", root);
    const close = select("[data-chatbot-close]", root);
    const messages = select("[data-chatbot-messages]", root);
    const form = select("[data-chatbot-form]", root);
    const input = select("[data-chatbot-input]", root);

    if (!panel || !toggle || !messages || !form || !input) return;

    const scrollToEnd = function () {
      messages.scrollTop = messages.scrollHeight;
    };

    const endpoint = root.getAttribute("data-chatbot-endpoint");
    const status = select("[data-chatbot-status]", root);
    const retry = select("[data-chatbot-retry]", root);
    const cancel = select("[data-chatbot-cancel]", root);
    const send = select("[type='submit']", form);
    const antiforgery = select("input[name='__RequestVerificationToken']", root);
    let activeRequest = null;
    let lastFailedQuestion = null;
    let requestSequence = 0;

    const setStatus = function (texto, visible) {
      if (!status) return;

      status.textContent = texto || "";
      status.hidden = !visible;
    };

    const appendMessage = function (texto, autor) {
      const wrapper = document.createElement("div");
      const bubble = document.createElement("span");

      wrapper.className = "rs-chatbot__message rs-chatbot__message--" + autor;
      bubble.className = "rs-chatbot__bubble";
      bubble.textContent = texto;
      wrapper.appendChild(bubble);
      messages.appendChild(wrapper);
      scrollToEnd();
      return wrapper;
    };

    const renderSources = function (wrapper, fuentes) {
      if (!Array.isArray(fuentes) || fuentes.length === 0) return;

      const heading = document.createElement("strong");
      heading.textContent = "Fuentes";

      const list = document.createElement("ul");
      list.className = "rs-chatbot__sources";

      fuentes.forEach(function (fuente) {
        const item = document.createElement("li");
        const title = fuente.titulo || fuente.fuente || "Fuente documental";
        item.textContent = fuente.id ? fuente.id + ": " + title : title;
        list.appendChild(item);
      });

      wrapper.appendChild(heading);
      wrapper.appendChild(list);
    };

    const renderResponse = function (wrapper, data) {
      const bubble = select(".rs-chatbot__bubble", wrapper);
      if (!bubble) return;

      bubble.textContent = data && data.respuesta
        ? data.respuesta
        : "No se encontró información suficiente para responder.";

      const oldSources = select(".rs-chatbot__sources", wrapper);
      if (oldSources) oldSources.remove();

      const oldHeading = select("strong", wrapper);
      if (oldHeading) oldHeading.remove();

      renderSources(wrapper, data ? data.fuentes : []);
    };

    const setBusy = function (busy) {
      input.disabled = busy;
      if (send) send.disabled = busy;
      if (cancel) cancel.hidden = !busy;
      if (retry && busy) retry.hidden = true;
    };

    const errorMessage = function (response, payload) {
      if (payload && payload.mensaje) return payload.mensaje;

      if (response.status === 429) {
        return "El asistente está recibiendo muchas solicitudes. Intenta nuevamente en unos segundos.";
      }

      if (response.status === 504) {
        return "El asistente tardó demasiado en responder. Intenta nuevamente.";
      }

      return "El asistente no está disponible en este momento. Intenta nuevamente.";
    };

    const executeQuestion = async function (question, userMessage, botMessage) {
      if (!endpoint || activeRequest) return;

      const controller = new AbortController();
      const requestId = ++requestSequence;
      activeRequest = { controller: controller, requestId: requestId };
      lastFailedQuestion = null;
      setBusy(true);
      setStatus("Consultando al asistente...", true);

      const botBubble = select(".rs-chatbot__bubble", botMessage);
      if (botBubble) botBubble.textContent = "Consultando...";

      try {
        const response = await fetch(endpoint, {
          method: "POST",
          headers: {
            "Accept": "application/json",
            "Content-Type": "application/json",
            "RequestVerificationToken": antiforgery ? antiforgery.value : ""
          },
          credentials: "same-origin",
          body: JSON.stringify({ pregunta: question }),
          signal: controller.signal
        });

        let payload = null;
        try {
          payload = await response.json();
        } catch (parseError) {
          payload = null;
        }

        if (!response.ok) {
          const failure = new Error(errorMessage(response, payload));
          failure.status = response.status;
          throw failure;
        }

        renderResponse(botMessage, payload);
        setStatus(payload && payload.suficienteInformacion === false
          ? "La respuesta no contiene información suficiente."
          : "", Boolean(payload && payload.suficienteInformacion === false));
        if (retry) retry.hidden = true;
      } catch (error) {
        if (controller.signal.aborted) {
          if (botBubble) botBubble.textContent = "Consulta cancelada.";
          setStatus("Consulta cancelada.", true);
          return;
        }

        if (requestId !== requestSequence) return;

        if (botBubble) botBubble.textContent = error.message || "No se pudo completar la consulta.";
        lastFailedQuestion = {
          question: question,
          userMessage: userMessage,
          botMessage: botMessage
        };
        setStatus("No se pudo completar la consulta.", true);
        if (retry) retry.hidden = false;
      } finally {
        if (activeRequest && activeRequest.requestId === requestId) {
          activeRequest = null;
          setBusy(false);
        }
      }
    };

    const preguntar = function (texto, opciones) {
      const limpio = (texto || "").trim();

      if (!limpio || activeRequest) return;

      const userMessage = appendMessage(limpio, "user");
      const botMessage = appendMessage("Consultando...", "bot");
      input.value = "";
      executeQuestion(limpio, userMessage, botMessage);

      if (!opciones || opciones.focusInput !== false) input.focus();
    };

    const abrir = function () {
      panel.hidden = false;
      toggle.setAttribute("aria-expanded", "true");
      scrollToEnd();

      window.setTimeout(function () {
        input.focus();
      }, 60);
    };

    const cerrar = function (opciones) {
      if (panel.hidden) return;

      panel.hidden = true;
      toggle.setAttribute("aria-expanded", "false");

      if (opciones && opciones.focusToggle) {
        toggle.focus();
      }
    };

    toggle.addEventListener("click", function () {
      if (panel.hidden) {
        abrir();
      } else {
        cerrar();
      }
    });

    if (close) {
      close.addEventListener("click", function () {
        cerrar({ focusToggle: true });
      });
    }

    form.addEventListener("submit", function (event) {
      event.preventDefault();

      const texto = input.value.trim();

      if (!texto) {
        input.focus();
        return;
      }

      preguntar(texto);
    });

    if (cancel) {
      cancel.addEventListener("click", function () {
        if (activeRequest) activeRequest.controller.abort();
      });
    }

    if (retry) {
      retry.addEventListener("click", function () {
        if (!lastFailedQuestion || activeRequest) return;

        executeQuestion(
          lastFailedQuestion.question,
          lastFailedQuestion.userMessage,
          lastFailedQuestion.botMessage);
      });
    }

    selectAll("[data-chatbot-question]", root).forEach(function (button) {
      button.addEventListener("click", function () {
        preguntar(button.getAttribute("data-chatbot-question"), { focusInput: false });
      });
    });

    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape") {
        cerrar({ focusToggle: true });
      }
    });
  }

  function initReveal() {
    if (!("IntersectionObserver" in window)) {
      selectAll("[data-reveal]").forEach(function (item) {
        item.classList.add("is-visible");
      });
      return;
    }

    const observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.08, rootMargin: "0px 0px -40px 0px" });

    selectAll("[data-reveal]").forEach(function (item) {
      observer.observe(item);
    });
  }

  function initPendingActions() {
    selectAll("[data-pending-form]").forEach(function (form) {
      form.addEventListener("submit", function (event) {
        event.preventDefault();
        const target = select(form.getAttribute("data-pending-form"));

        if (target) {
          target.hidden = false;
          target.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }
      });
    });

    selectAll("[data-pending-action]").forEach(function (button) {
      button.addEventListener("click", function (event) {
        event.preventDefault();
        const target = select(button.getAttribute("data-pending-action"));

        if (target) {
          target.hidden = false;
          target.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }
      });
    });

    selectAll("[data-password-toggle]").forEach(function (button) {
      button.addEventListener("click", function () {
        const input = select(button.getAttribute("data-password-toggle"));

        if (!input) return;

        const show = input.type === "password";
        input.type = show ? "text" : "password";
        button.setAttribute("aria-label", show ? "Ocultar contraseña" : "Mostrar contraseña");
        button.setAttribute("aria-pressed", String(show));
      });
    });
  }

  document.addEventListener("DOMContentLoaded", function () {
    initNavigation();
    initMaps();
    initOriginPickers();
    initRouteMaps();
    initProductLookup();
    initChatbot();
    initDirectory();
    initReveal();
    initPendingActions();
  });
})();
