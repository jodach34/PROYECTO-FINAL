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

  function initMaps() {
    selectAll("[data-map]").forEach(function (map) {
      const stage = select("[data-map-stage]", map);
      const zoomValue = select("[data-map-zoom]", map);
      const zoomIn = select("[data-map-zoom-in]", map);
      const zoomOut = select("[data-map-zoom-out]", map);
      const reset = select("[data-map-reset]", map);

      let zoom = 1;

      const applyZoom = function () {
        if (!stage) return;

        zoom = Math.min(1.6, Math.max(1, zoom));
        stage.style.transform = "scale(" + zoom.toFixed(2) + ")";
        if (zoomValue) zoomValue.textContent = Math.round(zoom * 100) + "%";
      };

      if (zoomIn) {
        zoomIn.addEventListener("click", function () {
          zoom += 0.15;
          applyZoom();
        });
      }

      if (zoomOut) {
        zoomOut.addEventListener("click", function () {
          zoom -= 0.15;
          applyZoom();
        });
      }

      if (reset) {
        reset.addEventListener("click", function () {
          zoom = 1;
          applyZoom();
          selectAll(".rs-pin.is-open", map).forEach(function (pin) {
            pin.classList.remove("is-open");
          });
        });
      }

      selectAll(".rs-pin", map).forEach(function (pin) {
        pin.addEventListener("click", function (event) {
          const isLink = pin.tagName === "A";

          if (isLink && !pin.classList.contains("is-open")) {
            event.preventDefault();
          }

          selectAll(".rs-pin.is-open", map).forEach(function (other) {
            if (other !== pin) other.classList.remove("is-open");
          });

          pin.classList.toggle("is-open");
        });
      });
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
      const pins = selectAll(".rs-pin", directory);

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
