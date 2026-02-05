//// ****************************************************************
// * Function Key PI Entrypoint
//// ****************************************************************

(function () {
  const SCPI = globalThis.SCPI;
  SCPI?.bus?.start?.();

  // #region Global State

  const functionDropdown = SCPI?.ui?.dropdown?.initDropdown?.({
    rootId: 'functionDropdown',
    searchEnabled: true,
    minLoadingMs: 0,
    successFlashMs: 100,
    emptyText: 'No matching functions found',
    maxResults: 50,
    getText: (opt) => String(opt?.text ?? ''),
    getValue: (opt) => String(opt?.value ?? ''),
    getGroup: (opt) => String(opt?.group ?? ''),
    isDisabled: (opt) => !!opt?.disabled,
    onSelect: (opt) => selectOption(opt, {persist: true})
  });

  functionDropdown?.setLoading?.(true, 'Loading functions');

  /**
   * Flattened list of all options for searching
   * @type {Array}
   */
  let allOptions = [];

  /**
   * Currently selected function value
   * @type {string}
   */
  let currentFunctionValue = '';

  /**
   * Flag to avoid feedback loops when writing settings
   * @type {boolean}
   */
  let isSelectingOption = false;

  // #endregion

  // #region SDK Settings Integration

  /**
   * Get and set function value using SDK Settings API
   */
  const [getFunctionSetting, setFunctionSetting] = globalThis.SDPIComponents.useSettings(
    'function',
    (value) => {
      if (!isSelectingOption) {
        currentFunctionValue = value;
        selectFunctionByValue(value);
      }
    }
  );

  SCPI?.ui?.filePicker?.createFilePicker?.({
    rootId: 'audioFilePicker',
    placeholderText: 'No file selected',
    settingsKey: 'clickSoundPath'
  });

  const resetHoldSecondsInput = document.getElementById('resetHoldSeconds');
  if (resetHoldSecondsInput) {
    const defaultSeconds = 1.0;
    const minSeconds = 0.2;
    const maxSeconds = 10.0;

    const resetHoldSecondsClearBtn = document.getElementById('resetHoldSecondsClear');

    function clampResetHoldSeconds(raw) {
      const value = (typeof raw === 'number') ? raw : parseFloat(String(raw));
      if (!Number.isFinite(value)) {
        return defaultSeconds;
      }

      return Math.min(maxSeconds, Math.max(minSeconds, value));
    }

    function updateResetHoldClearButton() {
      if (!resetHoldSecondsClearBtn) {
        return;
      }

      const normalized = clampResetHoldSeconds(resetHoldSecondsInput.value);
      resetHoldSecondsClearBtn.disabled = Math.abs(normalized - defaultSeconds) < 0.0001;
    }

    const [getResetHoldSecondsSetting, setResetHoldSecondsSetting] = globalThis.SDPIComponents.useSettings(
      'resetHoldSeconds',
      (value) => {
        const normalized = clampResetHoldSeconds(value);
        resetHoldSecondsInput.value = normalized.toFixed(1);
        updateResetHoldClearButton();
      }
    );

    if (resetHoldSecondsClearBtn) {
      resetHoldSecondsClearBtn.addEventListener('click', () => {
        resetHoldSecondsInput.value = defaultSeconds.toFixed(1);
        setResetHoldSecondsSetting(defaultSeconds);
        updateResetHoldClearButton();
      });
    }

    resetHoldSecondsInput.addEventListener('input', () => {
      updateResetHoldClearButton();
    });

    resetHoldSecondsInput.addEventListener('change', () => {
      const normalized = clampResetHoldSeconds(resetHoldSecondsInput.value);
      resetHoldSecondsInput.value = normalized.toFixed(1);
      setResetHoldSecondsSetting(normalized);
      updateResetHoldClearButton();
    });

    // Ensure the initial UI reflects current settings (or defaults).
    resetHoldSecondsInput.value = clampResetHoldSeconds(getResetHoldSecondsSetting()).toFixed(1);
    updateResetHoldClearButton();
  }

  // #endregion

  // #region Dropdown Rendering

  /**
   * Flatten grouped functions data into a searchable array
   * @param {Array} groups - Array of grouped function data
   * @returns {Array} - Flattened array of options
   */
  function flattenFunctionsData(groups) {
    const flat = [];

    const requireToggleCandidates = globalThis.SCPI_REQUIRE_TOGGLE_CANDIDATES === true;

    if (Array.isArray(groups)) {
      groups.forEach(group => {
        const groupName = group.label || 'Other';
        const options = group.options || [];

        options.forEach(opt => {
          const bindingType = String(opt.bindingType || '').toLowerCase();

          if (requireToggleCandidates) {
            const isToggleCandidate = opt && opt.details && opt.details.isToggleCandidate === true;
            if (!isToggleCandidate) {
              return;
            }
          }

          // Filter out unsupported options
          // TODO: When implementing full axis support, stop hiding axis-only options in the PI.
          const isUnsupported = bindingType === 'mouseaxis' || bindingType === 'joystick' || bindingType === 'gamepad';
          if (isUnsupported) {
            return;
          }

          // Keep the original category; unbound actions are shown with a warning indicator.
          const category = groupName;

          const disabledReason = String(opt.disabledReason || '');
          const isUnbound = bindingType === 'unbound';

          // Unbound actions are selectable (so users can bind them later), but still visually flagged.
          const isDisabled = !!opt.disabled && !isUnbound;

          flat.push(
            {
              value: opt.value,
              legacyValue: opt.legacyValue,
              text: opt.text,
              group: category,
              details: opt.details,
              bindingType,
              disabledReason,
              unbound: isUnbound,
              disabled: isDisabled
            });
        });
      });
    }

    return flat;
  }

  /**
   * Populate the dropdown with functions data
   * @param {Array} functionsData - Array of grouped function data
   */
  function populateFunctionsDropdown(functionsData) {
    // Flatten data for easier searching
    allOptions = flattenFunctionsData(functionsData);
    functionDropdown?.setItems?.(allOptions);
    functionDropdown?.setSelectedValue?.(currentFunctionValue, {rerender: false});

    if (currentFunctionValue) {
      selectFunctionByValue(currentFunctionValue);
    }
  }

  // #endregion

  // #region Option Selection

  /**
   * Select an option and update the display
   * @param {Object} opt - The option object to select
   */
  function selectOption(opt, opts = {}) {
    const persist = opts.persist !== false;

    isSelectingOption = true;
    currentFunctionValue = opt.value;
    functionDropdown?.setSelectedValue?.(opt.value, {rerender: true});
    updateFunctionDetails(opt);

    if (persist) {
      setFunctionSetting(opt.value);
    }

    // Reset flag after a longer delay to allow loadConfiguration to complete
    setTimeout(() => {
      isSelectingOption = false;
    }, 200);
  }

  /**
   * Select a function by its value
   * @param {string} value - The function value to select
   */
  function selectFunctionByValue(value) {
    const opt = allOptions.find(o => o.value === value || o.legacyValue === value);
    if (!opt) {
      return;
    }

    const isLegacyMatch = opt.legacyValue === value && opt.value !== value;

    // Only persist when we are migrating a legacy id to a v2 id.
    // Otherwise we just update the UI without re-writing the same value.
    selectOption(opt, {persist: isLegacyMatch});
  }

  // #endregion

  // #region Details Rendering

  /**
   * Render function details panel with binding information
   * @param {Object|null} opt - The option object (contains details), null for empty state
   */
  function updateFunctionDetails(opt) {
    const detailsEl = document.querySelector('.pi-details');
    if (!detailsEl) {
      return;
    }

    // Show empty state if no option selected
    if (!opt || !opt.details) {
      const titleEl = detailsEl.querySelector('.pi-details__title');
      if (titleEl) {
        titleEl.textContent = '';
      }

      const scContentEl = document.querySelector('.pi-description__content');
      if (scContentEl) {
        scContentEl.textContent = '';
      }

      // Clear all binding values
      const kbEl = document.getElementById('pi-details__binding-keyboard');
      const mouseEl = document.getElementById('pi-details__binding-mouse');
      const gpEl = document.getElementById('pi-details__binding-gamepad');
      const jsEl = document.getElementById('pi-details__binding-joystick');

      [kbEl, mouseEl, gpEl, jsEl].forEach((el) => {
        if (!el) {
          return;
        }
        el.textContent = '';
      });
      return;
    }

    const details = opt.details;
    const title = String(details.label || opt.text || '');
    const desc = String(details.description || '');
    const devices = Array.isArray(details.devices) ? details.devices : [];

    const titleEl = detailsEl.querySelector('.pi-details__title');
    if (titleEl) {
      titleEl.textContent = title;
    }

    // Update binding values for all four device types
    const allDeviceTypes = ['keyboard', 'mouse', 'gamepad', 'joystick'];

    allDeviceTypes.forEach(deviceType => {
      const deviceData = devices.find(d => d.device && d.device.toLowerCase() === deviceType.toLowerCase());
      const bindingEl = document.getElementById(`pi-details__binding-${deviceType}`);

      let bindingValue = 'Unbound';

      if (deviceData && Array.isArray(deviceData.bindings) && deviceData.bindings.length > 0) {
        const bindingLines = deviceData.bindings
          .map(b => String(b.display || b.raw || ''))
          .filter(x => x);

        if (bindingLines.length > 0) {
          bindingValue = bindingLines.join(', ');
        }
      }

      if (bindingEl) {
        bindingEl.textContent = bindingValue;
      }
    });

    // Update Description content
    const scContentEl = document.querySelector('.pi-description__content');
    if (scContentEl) {
      scContentEl.textContent = desc || 'No description available.';
    }
  }

  // #endregion

  // #region WebSocket Communication

  SCPI?.bus?.on?.((payload) => {
    const loaded = payload?.functionsLoaded;

    if (loaded === true) {
      functionDropdown?.setLoading?.(false);
      document.getElementById('functionDropdown')?.classList.remove('pi-dropdown--error');
      populateFunctionsDropdown(payload.functions || []);
    }

    if (loaded === false) {
      document.getElementById('functionDropdown')?.classList.add('pi-dropdown--error');
      functionDropdown?.setLoading?.(true, 'No installation detected. Set custom path.');
      functionDropdown?.setItems?.([]);
      updateFunctionDetails(null);
    }
  });

  // #endregion

  // #region Event Listeners

  SCPI?.util?.onDocumentReady?.(() => {
    const savedFunctionValue = getFunctionSetting();

    if (savedFunctionValue) {
      currentFunctionValue = savedFunctionValue;
    } else {
      updateFunctionDetails(null);
    }

    functionDropdown?.setSelectedValue?.(currentFunctionValue, {rerender: false});
    SCPI?.bus?.sendOnce?.('pi.connected', 'propertyInspectorConnected');
  });

  // #endregion
})();
