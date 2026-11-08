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

  function initChatbot() {
    const root = select("[data-chatbot]");

    if (!root) return;

    if (root.dataset.chatbotInitialized === "true") return;
    root.dataset.chatbotInitialized = "true";

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
    const suggestions = selectAll("[data-chatbot-question]", root);
    let activeRequest = null;
    let lastFailedQuestion = null;
    let requestSequence = 0;

    const setStatus = function (texto, visible) {
      if (!status) return;

      status.textContent = texto || "";
      status.hidden = !visible;
    };

    const appendInlineMarkdown = function (parent, text) {
      const pattern = /(\*\*[^*]+\*\*|__[^_]+__)/g;
      let cursor = 0;
      let match;

      while ((match = pattern.exec(text)) !== null) {
        if (match.index > cursor) {
          parent.appendChild(document.createTextNode(text.slice(cursor, match.index)));
        }

        const strong = document.createElement("strong");
        strong.textContent = match[0].slice(2, -2);
        parent.appendChild(strong);
        cursor = pattern.lastIndex;
      }

      if (cursor < text.length) {
        parent.appendChild(document.createTextNode(text.slice(cursor)));
      }
    };

    const renderMarkdown = function (container, markdown) {
      container.replaceChildren();

      const lines = String(markdown || "").replace(/\r\n?/g, "\n").split("\n");
      let index = 0;

      while (index < lines.length) {
        if (!lines[index].trim()) {
          index += 1;
          continue;
        }

        const headingMatch = lines[index].match(/^(#{1,3})\s+(.+?)\s*#*$/);
        if (headingMatch) {
          const heading = document.createElement("h" + (headingMatch[1].length + 1));
          heading.className = "rs-chatbot__markdown-heading";
          appendInlineMarkdown(heading, headingMatch[2]);
          container.appendChild(heading);
          index += 1;
          continue;
        }

        const orderedMatch = lines[index].match(/^\s*\d+[.)]\s+(.+)$/);
        const unorderedMatch = lines[index].match(/^\s*[-*•]\s+(.+)$/);
        if (orderedMatch || unorderedMatch) {
          const ordered = Boolean(orderedMatch);
          const list = document.createElement(ordered ? "ol" : "ul");
          list.className = "rs-chatbot__markdown-list";

          while (index < lines.length) {
            const itemMatch = ordered
              ? lines[index].match(/^\s*\d+[.)]\s+(.+)$/)
              : lines[index].match(/^\s*[-*•]\s+(.+)$/);
            if (!itemMatch) break;

            const item = document.createElement("li");
            appendInlineMarkdown(item, itemMatch[1]);
            list.appendChild(item);
            index += 1;
          }

          container.appendChild(list);
          continue;
        }

        const paragraph = document.createElement("p");
        paragraph.className = "rs-chatbot__markdown-paragraph";

        while (index < lines.length && lines[index].trim()) {
          if (paragraph.childNodes.length > 0) {
            paragraph.appendChild(document.createElement("br"));
          }
          appendInlineMarkdown(paragraph, lines[index]);
          index += 1;
        }

        container.appendChild(paragraph);
      }
    };

    const appendMessage = function (texto, autor) {
      const wrapper = document.createElement("div");
      const bubble = document.createElement("div");

      wrapper.className = "rs-chatbot__message rs-chatbot__message--" + autor;
      bubble.className = "rs-chatbot__bubble";
      if (autor === "bot") {
        const content = document.createElement("div");
        const name = document.createElement("span");
        content.className = "rs-chatbot__message-content";
        name.className = "rs-chatbot__message-name";
        name.textContent = "RESCAUTA IA";
        content.appendChild(name);
        content.appendChild(bubble);
        wrapper.appendChild(content);
        renderMarkdown(bubble, texto);
      } else {
        bubble.textContent = texto;
        wrapper.appendChild(bubble);
      }
      messages.appendChild(wrapper);
      scrollToEnd();
      return wrapper;
    };

    const renderSources = function (wrapper, fuentes) {
      if (!Array.isArray(fuentes) || fuentes.length === 0) return;

      const content = select(".rs-chatbot__message-content", wrapper);
      if (!content) return;

      const section = document.createElement("section");
      section.className = "rs-chatbot__sources-block";
      section.setAttribute("aria-label", "Fuentes de la respuesta");

      const heading = document.createElement("h3");
      heading.className = "rs-chatbot__sources-heading";
      heading.textContent = "Fuentes consultadas";

      const list = document.createElement("ul");
      list.className = "rs-chatbot__sources";

      fuentes.forEach(function (fuente) {
        const item = document.createElement("li");
        const title = fuente.titulo || fuente.fuente || "Fuente documental";
        item.textContent = fuente.id ? fuente.id + ": " + title : title;
        list.appendChild(item);
      });

      section.appendChild(heading);
      section.appendChild(list);
      content.appendChild(section);
    };

    const renderResponse = function (wrapper, data) {
      const bubble = select(".rs-chatbot__bubble", wrapper);
      if (!bubble) return;

      const answer = data && data.respuesta
        ? data.respuesta
        : "No se encontró información suficiente para responder.";
      renderMarkdown(bubble, answer);

      const oldSources = select(".rs-chatbot__sources-block", wrapper);
      if (oldSources) oldSources.remove();

      renderSources(wrapper, data ? data.fuentes : []);
    };

    const setThinking = function (bubble) {
      if (!bubble) return;

      bubble.replaceChildren();
      const label = document.createElement("span");
      label.className = "rs-chatbot__sr-only";
      label.textContent = "RESCAUTA IA está preparando una respuesta";

      const dots = document.createElement("span");
      dots.className = "rs-chatbot__typing";
      dots.setAttribute("aria-hidden", "true");
      for (let index = 0; index < 3; index += 1) {
        dots.appendChild(document.createElement("i"));
      }

      bubble.appendChild(label);
      bubble.appendChild(dots);
    };

    const setBusy = function (busy) {
      input.disabled = busy;
      messages.setAttribute("aria-busy", String(busy));
      if (send) send.disabled = busy;
      if (cancel) cancel.hidden = !busy;
      if (retry && busy) retry.hidden = true;
      suggestions.forEach(function (button) {
        button.disabled = busy;
      });
    };

    const errorMessage = function (response, payload) {
      if (payload && payload.mensaje) return payload.mensaje;

      if (response.status === 429) {
        return "RESCAUTA IA está recibiendo muchas consultas. Intenta nuevamente en unos segundos.";
      }

      if (response.status === 504) {
        return "RESCAUTA IA tardó demasiado en responder. Intenta nuevamente.";
      }

      return "RESCAUTA IA no está disponible en este momento. Intenta nuevamente.";
    };

    const executeQuestion = async function (question, userMessage, botMessage) {
      if (!endpoint || activeRequest) return;

      const controller = new AbortController();
      const requestId = ++requestSequence;
      activeRequest = { controller: controller, requestId: requestId };
      lastFailedQuestion = null;
      setBusy(true);
      setStatus("RESCAUTA IA está preparando una respuesta...", true);

      const botBubble = select(".rs-chatbot__bubble", botMessage);
      setThinking(botBubble);

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

    suggestions.forEach(function (button) {
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
    initDirectory();
    initChatbot();
    initReveal();
    initPendingActions();
  });
})();
