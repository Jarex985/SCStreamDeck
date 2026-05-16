//// ****************************************************************
// * SC File Picker
// * Reusable file picker wiring for Property Inspector pages
//// ****************************************************************

(function () {
  const root = globalThis;

  function safeDecodeURIComponent(value) {
    try {
      return decodeURIComponent(value);
    } catch (_) {
      return value;
    }
  }

  function sanitizeFilePath(path) {
    if (typeof path !== 'string') {
      return '';
    }
    return safeDecodeURIComponent(String(path).replace(/^C:\\fakepath\\/i, ''));
  }

  function getFileName(path) {
    if (typeof path !== 'string' || path.length === 0) {
      return '';
    }
    return path.split('\\').pop().split('/').pop();
  }

  function ensureFilePickerMarkup(rootEl, opts = {}) {
    const existing = rootEl.querySelector('.file-picker-container');
    if (existing) {
      return;
    }

    const accept = typeof opts.accept === 'string' ? opts.accept : (rootEl.getAttribute('data-accept') || '');
    const placeholderText = typeof opts.placeholderText === 'string'
      ? opts.placeholderText
      : (rootEl.getAttribute('data-placeholder') || 'No file selected');
    const buttonText = typeof opts.buttonText === 'string'
      ? opts.buttonText
      : (rootEl.getAttribute('data-button-text') || 'FILE');
    const selectTitle = typeof opts.selectTitle === 'string'
      ? opts.selectTitle
      : (rootEl.getAttribute('data-select-title') || 'Select file');
    const clearTitle = typeof opts.clearTitle === 'string'
      ? opts.clearTitle
      : (rootEl.getAttribute('data-clear-title') || 'Clear');

    const container = document.createElement('div');
    container.className = 'file-picker-container';

    const input = document.createElement('input');
    input.type = 'file';
    input.style.display = 'none';
    if (accept) {
      input.setAttribute('accept', accept);
    }

    const display = document.createElement('div');
    display.className = 'file-picker-display';

    const filename = document.createElement('span');
    filename.className = 'filename-text';
    filename.textContent = placeholderText;
    display.appendChild(filename);

    const selectButton = document.createElement('button');
    selectButton.className = 'file-picker-button';
    selectButton.type = 'button';
    selectButton.title = selectTitle;

    const buttonIcon = document.createElement('span');
    buttonIcon.className = 'button-icon';
    buttonIcon.textContent = buttonText;
    selectButton.appendChild(buttonIcon);

    const clearButton = document.createElement('button');
    clearButton.className = 'file-picker-clear';
    clearButton.type = 'button';
    clearButton.title = clearTitle;
    clearButton.disabled = true;
    clearButton.textContent = 'X';

    container.appendChild(input);
    container.appendChild(display);
    container.appendChild(selectButton);
    container.appendChild(clearButton);

    rootEl.replaceChildren(container);
  }

  function createFilePicker(options = {}) {
    const rootId = options.rootId;
    const rootEl = rootId ? document.getElementById(rootId) : null;
    if (!rootEl) {
      return null;
    }

    ensureFilePickerMarkup(rootEl, options);

    const filenameSelector = options.filenameSelector || '.filename-text';
    const placeholderText = options.placeholderText || 'No file selected';
    const settingsKey = options.settingsKey;
    const displayMode = options.displayMode || 'basename'; // 'basename' | 'full'
    const onValueChanged = typeof options.onValueChanged === 'function' ? options.onValueChanged : null;
    const initialValue = typeof options.initialValue === 'string' ? options.initialValue : '';

    const inputEl = rootEl.querySelector('input[type="file"]');
    const buttonEl = rootEl.querySelector('.file-picker-button');
    const clearEl = rootEl.querySelector('.file-picker-clear');
    const displayEl = rootEl.querySelector('.file-picker-display');
    const filenameEl = displayEl ? displayEl.querySelector(filenameSelector) : null;

    if (!inputEl || !buttonEl || !clearEl || !displayEl || !filenameEl) {
      return null;
    }

    let currentValue = '';
    let isWritingSetting = false;
    let setSetting = null;
    let getSetting = null;

    function render(value) {
      const hasValue = typeof value === 'string' && value.length > 0;
      const text = hasValue
        ? (displayMode === 'full' ? value : getFileName(value))
        : placeholderText;

      filenameEl.textContent = text;
      filenameEl.title = hasValue ? value : '';
      clearEl.disabled = !hasValue;
    }

    function setValue(value, opts = {}) {
      const persist = opts.persist !== false;
      const silent = !!opts.silent;

      const next = typeof value === 'string' ? value : '';
      currentValue = next;
      render(currentValue);

      if (!silent && onValueChanged) {
        try {
          onValueChanged(currentValue);
        } catch (_) {
          // Ignore.
        }
      }

      if (!persist || !setSetting) {
        return;
      }

      isWritingSetting = true;
      try {
        setSetting(next.length > 0 ? next : null);
      } finally {
        setTimeout(() => {
          isWritingSetting = false;
        }, 50);
      }
    }

    function clear() {
      inputEl.value = '';
      setValue('', {persist: true});
    }

    // Build a Win32-style filter string from an HTML `accept` attribute.
    // ".p4k" → "Data files (*.p4k)|*.p4k|All files|*.*"
    // ".mp3,.wav,.ogg" → "Audio (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|All files|*.*"
    function buildWinFilter(acceptValue) {
      if (typeof acceptValue !== 'string' || acceptValue.length === 0) {
        return 'All files|*.*';
      }
      const exts = [];
      acceptValue.split(',').forEach(tok => {
        const t = tok.trim();
        if (t.startsWith('.')) {
          exts.push('*' + t.toLowerCase());
        }
      });
      if (exts.length === 0) {
        return 'All files|*.*';
      }
      const list = exts.join(';');
      return 'Allowed (' + list + ')|' + list + '|All files|*.*';
    }

    // The PI runs inside OpenDeck's Tauri WebView which doesn't grant the
    // dialog plugin permission to plugin-served pages, AND <input type=file>
    // only exposes the filename. To get a real full path on Linux/Wine we
    // ask the plugin (running under Wine) to spawn a Win32 file dialog via
    // GetOpenFileNameW — Wine renders it as a native GTK chooser and the
    // returned path is Wine-resolvable.
    const send = () =>
      globalThis.SCPI?.bus?.send
      || globalThis.SCPI?.util?.sendToPlugin
      || globalThis.sendToPlugin;

    // Subscribe once to filePickerResult messages from the plugin and
    // dispatch to whichever picker owns the result (matched by rootId).
    if (globalThis.SCPI?.bus) {
      globalThis.SCPI.bus.start?.();
      globalThis.SCPI.bus.on?.((payload) => {
        if (payload?.event !== 'filePickerResult') return;
        if (payload.pickerId !== rootId) return;
        const path = typeof payload.path === 'string' ? payload.path : '';
        if (path.length === 0) {
          // Cancelled — no-op so the previous value stays visible.
          return;
        }
        setValue(path, {persist: true});
      });
    }

    buttonEl.addEventListener('click', () => {
      const accept = inputEl.getAttribute('accept') || '';
      const filter = buildWinFilter(accept);
      const title = rootEl.getAttribute('data-select-title') || 'Select file';
      const s = send();
      if (s) {
        s('requestFilePicker', {pickerId: rootId, filter, title});
      } else {
        // Last-resort fallback if the bus isn't ready.
        inputEl.click();
      }
    });

    inputEl.addEventListener('change', (event) => {
      const file = event?.target?.files?.[0];
      const rawValue = event?.target?.value || '';
      const sanitizedValue = sanitizeFilePath(rawValue);
      const selectedPath = file?.path || sanitizedValue;

      if (selectedPath) {
        setValue(selectedPath, {persist: true});
        return;
      }

      // Fallback: show the file name even if the sandbox hides the path.
      if (file?.name) {
        render(file.name);
      }
    });

    clearEl.addEventListener('click', () => {
      clear();
    });

    // Hook into SDPI settings if requested.
    if (typeof settingsKey === 'string' && settingsKey.length > 0 && root.SDPIComponents?.useSettings) {
      [getSetting, setSetting] = root.SDPIComponents.useSettings(settingsKey, (value) => {
        if (isWritingSetting) {
          return;
        }
        setValue(typeof value === 'string' ? value : '', {persist: false});
      });

      Promise
        .resolve(getSetting())
        .then((value) => {
          setValue(typeof value === 'string' ? value : '', {persist: false});
        })
        .catch(() => {
          // Ignore.
        });
    }

    // Initial UI
    render('');
    if (initialValue) {
      setValue(initialValue, {persist: false, silent: true});
    }

    return {
      setValue,
      clear,
      getValue: () => currentValue
    };
  }

  function initFilePicker(options = {}) {
    // New (preferred): single root container that owns all internal nodes.
    if (typeof options.rootId === 'string' && options.rootId.length > 0) {
      return createFilePicker(options);
    }

    // Legacy: explicit element ids.
    const inputId = options.inputId;
    const buttonId = options.buttonId;
    const clearId = options.clearId;
    const displayId = options.displayId;
    const filenameSelector = options.filenameSelector || '.filename-text';
    const placeholderText = options.placeholderText || 'No file selected';
    const settingsKey = options.settingsKey;
    const displayMode = options.displayMode || 'basename'; // 'basename' | 'full'
    const onValueChanged = typeof options.onValueChanged === 'function' ? options.onValueChanged : null;
    const initialValue = typeof options.initialValue === 'string' ? options.initialValue : '';

    const inputEl = document.getElementById(inputId);
    const buttonEl = document.getElementById(buttonId);
    const clearEl = document.getElementById(clearId);
    const displayEl = document.getElementById(displayId);
    const filenameEl = displayEl ? displayEl.querySelector(filenameSelector) : null;

    if (!inputEl || !buttonEl || !clearEl || !displayEl || !filenameEl) {
      return null;
    }

    // Minimal wrapper: synthesize a fake root so we can reuse the same wiring.
    const tempRoot = document.createElement('div');
    const container = displayEl.closest('.file-picker-container');
    if (container) {
      tempRoot.appendChild(container.cloneNode(true));
    }

    // Wire using the existing elements (legacy paths keep behavior).
    let currentValue = '';
    let isWritingSetting = false;
    let setSetting = null;
    let getSetting = null;

    function render(value) {
      const hasValue = typeof value === 'string' && value.length > 0;
      const text = hasValue
        ? (displayMode === 'full' ? value : getFileName(value))
        : placeholderText;

      filenameEl.textContent = text;
      filenameEl.title = hasValue ? value : '';
      clearEl.disabled = !hasValue;
    }

    function setValue(value, opts = {}) {
      const persist = opts.persist !== false;
      const silent = !!opts.silent;

      const next = typeof value === 'string' ? value : '';
      currentValue = next;
      render(currentValue);

      if (!silent && onValueChanged) {
        try {
          onValueChanged(currentValue);
        } catch (_) {
          // Ignore.
        }
      }

      if (!persist || !setSetting) {
        return;
      }

      isWritingSetting = true;
      try {
        setSetting(next.length > 0 ? next : null);
      } finally {
        setTimeout(() => {
          isWritingSetting = false;
        }, 50);
      }
    }

    function clear() {
      inputEl.value = '';
      setValue('', {persist: true});
    }

    buttonEl.addEventListener('click', () => {
      inputEl.click();
    });

    inputEl.addEventListener('change', (event) => {
      const file = event?.target?.files?.[0];
      const rawValue = event?.target?.value || '';
      const sanitizedValue = sanitizeFilePath(rawValue);
      const selectedPath = file?.path || sanitizedValue;

      if (selectedPath) {
        setValue(selectedPath, {persist: true});
        return;
      }

      // Fallback: show the file name even if the sandbox hides the path.
      if (file?.name) {
        render(file.name);
      }
    });

    clearEl.addEventListener('click', () => {
      clear();
    });

    if (typeof settingsKey === 'string' && settingsKey.length > 0 && root.SDPIComponents?.useSettings) {
      [getSetting, setSetting] = root.SDPIComponents.useSettings(settingsKey, (value) => {
        if (isWritingSetting) {
          return;
        }
        setValue(typeof value === 'string' ? value : '', {persist: false});
      });

      Promise
        .resolve(getSetting())
        .then((value) => {
          setValue(typeof value === 'string' ? value : '', {persist: false});
        })
        .catch(() => {
          // Ignore.
        });
    }

    render('');
    if (initialValue) {
      setValue(initialValue, {persist: false, silent: true});
    }

    return {
      setValue,
      clear,
      getValue: () => currentValue
    };
  }

  const SCPI = root.SCPI = root.SCPI || {};
  SCPI.ui = SCPI.ui || {};
  SCPI.ui.filePicker = {
    createFilePicker,
    initFilePicker
  };

  // Back-compat
  root.SCFilePicker = root.SCFilePicker || {
    initFilePicker
  };
})();
