(function () {
  const SCPI = globalThis.SCPI;
  SCPI?.bus?.start?.();
  SCPI?.theme?.initThemeDropdown?.();

  const slotConfigs = [
    {rootId: 'rotateLeftDropdown', settingsKey: 'rotateLeftFunction', slotLabel: 'Rotate Left', idPrefix: 'rotateLeft'},
    {rootId: 'rotateRightDropdown', settingsKey: 'rotateRightFunction', slotLabel: 'Rotate Right', idPrefix: 'rotateRight'},
    {rootId: 'pressDropdown', settingsKey: 'pressFunction', slotLabel: 'Push', idPrefix: 'press'}
  ];

  let allOptions = [];
  let isSelectingOption = false;

  const slotStates = slotConfigs.map((config) => {
    const dropdown = SCPI?.ui?.dropdown?.initDropdown?.({
      rootId: config.rootId,
      searchEnabled: true,
      minLoadingMs: 0,
      successFlashMs: 100,
      emptyText: 'No matching functions found',
      maxResults: 50,
      getText: (opt) => String(opt?.text ?? ''),
      getValue: (opt) => String(opt?.value ?? ''),
      getGroup: (opt) => String(opt?.group ?? ''),
      isDisabled: (opt) => !!opt?.disabled,
      onSelect: (opt) => selectOption(config.settingsKey, opt, {persist: true})
    });

    dropdown?.setLoading?.(true, 'Loading functions');

    const [getValue, setValue] = globalThis.SDPIComponents.useSettings(
      config.settingsKey,
      (value) => {
        if (!isSelectingOption) {
          syncSelection(config.settingsKey, value);
        }
      }
    );

    return {
      ...config,
      dropdown,
      getValue,
      setValue,
      currentValue: ''
    };
  });

  SCPI?.ui?.filePicker?.createFilePicker?.({
    rootId: 'audioFilePicker',
    placeholderText: 'No file selected',
    settingsKey: 'clickSoundPath'
  });

  function flattenFunctionsData(groups) {
    const flatten = SCPI?.functionPicker?.flattenFunctionsData;
    if (typeof flatten !== 'function') {
      return [];
    }

    return flatten(groups, {
      requireToggleCandidates: false,
      excludeBindingTypes: ['mouseaxis', 'joystick', 'gamepad']
    });
  }

  function populateFunctionsDropdowns(functionsData) {
    allOptions = flattenFunctionsData(functionsData);

    slotStates.forEach((slot) => {
      slot.dropdown?.setItems?.(allOptions);
      slot.dropdown?.setSelectedValue?.(slot.currentValue, {rerender: false});

      if (slot.currentValue) {
        syncSelection(slot.settingsKey, slot.currentValue);
      }
    });
  }

  function syncSelection(settingsKey, value) {
    const slot = slotStates.find((entry) => entry.settingsKey === settingsKey);
    if (!slot) {
      return;
    }

    slot.currentValue = value || '';

    const opt = allOptions.find((entry) => entry.value === value || entry.legacyValue === value);
    if (!opt) {
      if (!value) {
        updateFunctionDetails(slot, null);
      }
      return;
    }

    const isLegacyMatch = opt.legacyValue === value && opt.value !== value;
    selectOption(settingsKey, opt, {persist: isLegacyMatch});
  }

  function selectOption(settingsKey, opt, opts = {}) {
    const slot = slotStates.find((entry) => entry.settingsKey === settingsKey);
    if (!slot) {
      return;
    }

    const persist = opts.persist !== false;

    isSelectingOption = true;
    slot.currentValue = opt.value;
    slot.dropdown?.setSelectedValue?.(opt.value, {rerender: true});
    updateFunctionDetails(slot, opt);

    if (persist) {
      slot.setValue(opt.value);
    }

    setTimeout(() => {
      isSelectingOption = false;
    }, 200);
  }

  function updateFunctionDetails(slot, opt) {
    if (!slot || !slot.idPrefix) {
      return;
    }

    const updateDetails = SCPI?.functionPicker?.updateFunctionDetails;
    if (typeof updateDetails !== 'function') {
      return;
    }

    const prefix = slot.idPrefix;
    updateDetails({
      containerEl: document.getElementById(`${prefix}-details-container`),
      titleEl: document.getElementById(`${prefix}-details-title`),
      descriptionEl: document.getElementById(`${prefix}-description`),
      bindingElements: {
        keyboard: document.getElementById(`${prefix}-binding-keyboard`),
        mouse: document.getElementById(`${prefix}-binding-mouse`),
        gamepad: document.getElementById(`${prefix}-binding-gamepad`),
        joystick: document.getElementById(`${prefix}-binding-joystick`)
      },
      hideContainerWhenEmpty: true,
      showContainerWhenFilled: true,
      slotLabel: slot.slotLabel,
      selectedOption: opt
    });
  }

  SCPI?.bus?.on?.((payload) => {
    const loaded = payload?.functionsLoaded;

    if (loaded === true) {
      slotStates.forEach((slot) => {
        slot.dropdown?.setLoading?.(false);
        document.getElementById(slot.rootId)?.classList.remove('pi-dropdown--error');
      });

      populateFunctionsDropdowns(payload.functions || []);
    }

    if (loaded === false) {
      slotStates.forEach((slot) => {
        document.getElementById(slot.rootId)?.classList.add('pi-dropdown--error');
        slot.dropdown?.setLoading?.(true, 'No installation detected. Set custom path.');
        slot.dropdown?.setItems?.([]);
        updateFunctionDetails(slot, null);
      });
    }
  });

  SCPI?.util?.onDocumentReady?.(() => {
    slotStates.forEach((slot) => {
      const savedValue = slot.getValue();
      slot.currentValue = savedValue || '';
      slot.dropdown?.setSelectedValue?.(slot.currentValue, {rerender: false});
      updateFunctionDetails(slot, null);
    });

    SCPI?.bus?.sendOnce?.('pi.connected', 'propertyInspectorConnected');
  });
})();
