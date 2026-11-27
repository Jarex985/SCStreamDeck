//// ****************************************************************
// * Config PI Entrypoint
// * Flags and format templates shared by the telemetry keys (kept in the plugin's global settings)
//// ****************************************************************

(function () {
  const SCPI = globalThis.SCPI;

  const {el, row, textInput, selectInput, flashTargetSelect, colors} = SCPI.ui.form;

  const state = {flags: [], templates: []};
  const stateKeys = {flags: 'flags', formatTemplates: 'templates'};
  const setters = {};
  let selectedFlagId = '';
  let selectedTemplateId = '';

  function newId() {
    return globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  }

  function saveList(key) {
    setters[key]?.(JSON.parse(JSON.stringify(state[stateKeys[key]])));
  }

  // Reads one list of the plugin's global settings; later changes are applied unless they are our own edits.
  function useList(key, stateKey, render) {
    if (!globalThis.SDPIComponents?.useGlobalSettings) {
      return;
    }

    const apply = (value) => {
      const next = Array.isArray(value) ? value : [];
      if (JSON.stringify(next) !== JSON.stringify(state[stateKey])) {
        state[stateKey] = next;
        render();
      }
    };

    const [get, set] = globalThis.SDPIComponents.useGlobalSettings(key, apply);
    setters[key] = set;
    Promise.resolve(get()).then(apply).catch(() => {
    });
  }

  function displayName(item) {
    return String(item?.name || '').trim() || '(unnamed)';
  }

  // Toolbar shared by both lists: pick one, create a new one, delete the picked one.
  function renderToolbar(items, selectedId, labels, handlers) {
    const toolbar = el('div', {class: 'pi-rule__condition'}, []);
    if (items.length > 0) {
      toolbar.appendChild(selectInput(items.map((i) => [i.id, displayName(i)]), selectedId, handlers.select,
        'pi-rule__select pi-rule__template'));
    }
    toolbar.appendChild(el('button', {class: 'pi-button', type: 'button', text: labels.add, onClick: handlers.add}));
    if (items.length > 0) {
      toolbar.appendChild(el('button', {
        class: 'file-picker-clear', type: 'button', title: labels.remove, text: 'X', onClick: handlers.remove
      }));
    }
    return toolbar;
  }

  // #region Flags

  function renderFlags() {
    const host = document.getElementById('flags');
    if (!host) {
      return;
    }

    if (!state.flags.some((f) => f.id === selectedFlagId)) {
      selectedFlagId = state.flags[0]?.id || '';
    }
    const selected = state.flags.find((f) => f.id === selectedFlagId);

    const children = [renderToolbar(state.flags, selectedFlagId, {add: 'New flag', remove: 'Delete this flag'}, {
      select: (id) => {
        selectedFlagId = id;
        renderFlags();
      },
      add: () => {
        const flag = {id: newId(), name: `Flag ${state.flags.length + 1}`, defaultValue: false};
        state.flags.push(flag);
        selectedFlagId = flag.id;
        saveList('flags');
        renderFlags();
      },
      remove: () => {
        state.flags = state.flags.filter((f) => f.id !== selectedFlagId);
        saveList('flags');
        renderFlags();
      }
    })];

    if (selected) {
      children.push(row('Name', textInput(selected.name, 'Flag name', (v) => {
        selected.name = v;
        saveList('flags');
      })));
      children.push(row('Starts', selectInput([['false', 'Off'], ['true', 'On']], String(!!selected.defaultValue), (v) => {
        selected.defaultValue = v === 'true';
        saveList('flags');
      }), 'Value of the flag when the plugin starts. Flag values are not saved: they return to this value when the plugin restarts.'));
    } else {
      children.push(el('span', {
        class: 'pi-rule__hint',
        text: 'No flags yet. A flag is an on/off switch that rules can set and test, for example a master warning or "preflight done".'
      }));
    }

    host.replaceChildren(...children);
  }

  // Name edits are applied when the field loses focus so the list is not rebuilt on every keystroke.
  document.getElementById('flags')?.addEventListener('change', (event) => {
    if (event.target?.matches?.('input[type="text"]')) {
      renderFlags();
    }
  });

  // #endregion

  // #region Format templates

  function renderTemplates() {
    const host = document.getElementById('templates');
    if (!host) {
      return;
    }

    if (!state.templates.some((t) => t.id === selectedTemplateId)) {
      selectedTemplateId = state.templates[0]?.id || '';
    }
    const selected = state.templates.find((t) => t.id === selectedTemplateId);

    const children = [renderToolbar(state.templates, selectedTemplateId, {add: 'New template', remove: 'Delete this template'}, {
      select: (id) => {
        selectedTemplateId = id;
        renderTemplates();
      },
      add: () => {
        const template = {id: newId(), name: `Template ${state.templates.length + 1}`};
        state.templates.push(template);
        selectedTemplateId = template.id;
        saveList('formatTemplates');
        renderTemplates();
      },
      remove: () => {
        state.templates = state.templates.filter((t) => t.id !== selectedTemplateId);
        saveList('formatTemplates');
        renderTemplates();
      }
    })];

    if (selected) {
      const save = () => saveList('formatTemplates');

      children.push(row('Name', textInput(selected.name, 'Template name', (v) => {
        selected.name = v;
        save();
      })));
      children.push(row('Colors', colors(selected, save)));
      children.push(row('Image', el('div', {
        class: 'pi-file-picker', id: 'template-image', 'data-accept': '.png,.jpg,.jpeg,.bmp,.gif',
        'data-placeholder': 'No image', 'data-select-title': 'Select image', 'data-clear-title': 'Clear image'
      })));
      children.push(row('Flash', textInput(selected.flashMs, 'off (ms)', (v) => {
        const parsed = parseInt(v, 10);
        selected.flashMs = Number.isFinite(parsed) && parsed > 0 ? parsed : null;
        save();
      }, 'number'), 'Show then hide for this many ms each (minimum 250). Empty = no flashing.'));
      children.push(row('Flashes', flashTargetSelect(selected, save),
        'Which part blinks while flashing. Background and Everything switch to black while off.'));
    } else {
      children.push(el('span', {
        class: 'pi-rule__hint', text: 'No templates yet. A template bundles colors, image and flash so rules can share one look.'
      }));
    }

    host.replaceChildren(...children);

    if (selected) {
      queueMicrotask(() => {
        SCPI?.ui?.filePicker?.createFilePicker?.({
          rootId: 'template-image',
          placeholderText: 'No image',
          initialValue: selected.imagePath || '',
          onValueChanged: (path) => {
            selected.imagePath = path || null;
            saveList('formatTemplates');
          }
        });
      });
    }
  }

  document.getElementById('templates')?.addEventListener('change', (event) => {
    if (event.target?.matches?.('input[type="text"]')) {
      renderTemplates();
    }
  });

  // #endregion

  // #region Startup

  useList('flags', 'flags', renderFlags);
  useList('formatTemplates', 'templates', renderTemplates);

  SCPI?.util?.onDocumentReady?.(() => {
    renderFlags();
    renderTemplates();
  });

  // #endregion
})();
