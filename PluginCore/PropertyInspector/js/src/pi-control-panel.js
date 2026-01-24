//// ****************************************************************
// * Control Panel PI Entrypoint
//// ****************************************************************

(function () {
  const SCPI = globalThis.SCPI;
  const CHANNELS = ['Live', 'Hotfix', 'Ptu', 'Eptu'];

  function buildChannelItems(cp) {
    const channelMap = new Map();
    const arr = Array.isArray(cp?.channels) ? cp.channels : [];
    for (const c of arr) {
      if (c && typeof c.channel === 'string') {
        channelMap.set(c.channel, c);
      }
    }

    const items = [];
    for (const ch of CHANNELS) {
      const info = channelMap.get(ch);
      const valid = !!info?.valid;
      if (!valid) {
        continue;
      }

      const custom = !!info?.isCustomPath;
      items.push({
        value: ch,
        text: `${ch.toUpperCase()} - ${custom ? 'Custom' : 'Auto'}`
      });
    }

    return items;
  }

  function setInlineError(elementId, text) {
    const el = document.getElementById(elementId);
    if (!el) {
      return;
    }

    const frame = el.parentElement && el.parentElement.classList.contains('pi-inline-banner')
      ? el.parentElement
      : null;

    const msg = String(text || '').trim();
    if (msg.length === 0) {
      el.textContent = '';
      el.style.display = 'none';
      if (frame) {
        frame.style.display = 'none';
      }
      return;
    }

    el.textContent = msg;
    el.style.display = 'block';
    if (frame) {
      frame.style.display = 'flex';
    }
  }

  function updateInstallWarning(cp) {
    const list = Array.isArray(cp?.channels) ? cp.channels : [];
    const anyConfigured = list.some((r) => !!r?.configured);
    const anyValid = list.some((r) => !!r?.valid);

    if (anyValid) {
      setInlineError('pi-install-warning', '');
      return;
    }

    if (!anyConfigured) {
      setInlineError('pi-install-warning', "No installation detected. Set custom path.");
      return;
    }

    setInlineError('pi-install-warning', 'No installation detected. Set custom path.');
  }

  function initPickers() {
    const pickers = new Map();

    const send = SCPI?.bus?.send || SCPI?.util?.sendToPlugin || globalThis.sendToPlugin;

    function wire(channel, rootId) {
      const picker = SCPI?.ui?.filePicker?.createFilePicker?.({
        rootId,
        displayMode: 'full',
        onValueChanged: (value) => {
          send?.('setDataP4KOverride', {
            channel,
            dataP4KPath: value || ''
          });
        }
      });

      pickers.set(channel, picker);
    }

    wire('Live', 'liveP4KPicker');
    wire('Hotfix', 'hotfixP4KPicker');
    wire('Ptu', 'ptuP4KPicker');
    wire('Eptu', 'eptuP4KPicker');

    return pickers;
  }

  function applyOverridePickerValues(cp, pickers) {
    const list = Array.isArray(cp?.channels) ? cp.channels : [];
    const byChannel = new Map();
    for (const row of list) {
      if (row && typeof row.channel === 'string') {
        byChannel.set(row.channel, row);
      }
    }

    for (const ch of CHANNELS) {
      const picker = pickers.get(ch);
      if (!picker) {
        continue;
      }
      const row = byChannel.get(ch);
      const value = row?.isCustomPath ? String(row?.dataP4KPath || '') : '';
      picker.setValue(value, {persist: false, silent: true});
    }
  }

  SCPI?.util?.onDocumentReady?.(() => {
    SCPI?.bus?.start?.();

    SCPI?.theme?.initThemeDropdown?.({
      rootId: 'themeDropdown',
      linkId: 'pi-theme-styles'
    });

    const pickers = initPickers();

    const send = SCPI?.bus?.send || SCPI?.util?.sendToPlugin || globalThis.sendToPlugin;

    const channelDropdown = SCPI?.ui?.dropdown?.initDropdown?.({
      rootId: 'channelDropdown',
      searchEnabled: false,
      displaySelectedInInput: true,
      minLoadingMs: 500,
      successFlashMs: 220,
      emptyText: 'No valid channels',
      getText: (t) => String(t?.text ?? ''),
      getValue: (t) => String(t?.value ?? ''),
      onSelect: (t) => {
        const value = String(t?.value || '');
        if (!value) {
          return;
        }
        send?.('setChannel', {channel: value});
      }
    });

    channelDropdown?.setLoading?.(true, 'Loading status');

    const factoryResetBtn = document.getElementById('factoryResetBtn');
    const redetectBtn = document.getElementById('forceRedetectBtn');

    factoryResetBtn?.addEventListener('click', () => {
      const ok = globalThis.confirm?.(
        'Factory Reset will remove cached installations, clear all custom Data.p4k overrides, reset theme selection, and rebuild keybindings from scratch.\n\nContinue?'
      );
      if (!ok) {
        return;
      }
      send?.('factoryReset');
    });

    redetectBtn?.addEventListener('click', () => {
      send?.('forceRedetection');
    });

    SCPI?.bus?.on?.((payload) => {
      if (!payload?.controlPanelLoaded) {
        return;
      }

      const cp = payload.controlPanel || {};
      const items = buildChannelItems(cp);
      channelDropdown?.setItems?.(items);
      channelDropdown?.setLoading?.(false);

      const preferred = String(cp?.preferredChannel || '');
      const current = String(cp?.currentChannel || '');
      const desired = current || preferred;

      const hasDesired = desired && items.some((it) => String(it?.value || '') === desired);
      if (hasDesired) {
        channelDropdown?.setSelectedValue?.(desired, {rerender: true});
      } else if (items.length > 0) {
        channelDropdown?.setSelectedValue?.(String(items[0].value || ''), {rerender: true});
      } else {
        // Clear any stale selection text when no valid channels exist.
        channelDropdown?.setSelectedValue?.('', {rerender: true});
      }

      updateInstallWarning(cp);
      applyOverridePickerValues(cp, pickers);
    });

    // Ask plugin for Control Panel status (themes + channel state).
    SCPI?.bus?.sendOnce?.('pi.connected', 'propertyInspectorConnected');
  });
})();
