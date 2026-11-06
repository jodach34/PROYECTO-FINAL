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
    initDirectory();
    initReveal();
    initPendingActions();
  });
})();
