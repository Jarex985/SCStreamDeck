//// ****************************************************************
// * SC Common Utilities
// * Shared utilities for Star Citizen StreamDeck Plugin
//// ****************************************************************

(function () {
  const root = globalThis;
  const SCPI = root.SCPI = root.SCPI || {};
  const util = SCPI.util = SCPI.util || {};

  function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
      const later = () => {
        clearTimeout(timeout);
        func(...args);
      };
      clearTimeout(timeout);
      timeout = setTimeout(later, wait);
    };
  }

  function onDocumentReady(callback) {
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', callback);
    } else {
      callback();
    }
  }

  function sendToPlugin(event, payload = {}) {
    try {
      if (!root.SDPIComponents?.streamDeckClient?.send) {
        console.warn('[sc-common] SDPIComponents.streamDeckClient.send not available');
        return;
      }
      root.SDPIComponents.streamDeckClient.send('sendToPlugin', {event, ...payload});
    } catch (err) {
      console.error('[sc-common] sendToPlugin failed', err);
    }
  }

  util.debounce = debounce;
  util.onDocumentReady = onDocumentReady;
  util.sendToPlugin = sendToPlugin;

  // Back-compat globals (older pages/scripts)
  if (typeof root.debounce !== 'function') {
    root.debounce = debounce;
  }
  if (typeof root.onDocumentReady !== 'function') {
    root.onDocumentReady = onDocumentReady;
  }
  if (typeof root.sendToPlugin !== 'function') {
    root.sendToPlugin = sendToPlugin;
  }
})();

(function () {
  const root = globalThis;
  const SCPI = root.SCPI = root.SCPI || {};
  const functionPicker = SCPI.functionPicker = SCPI.functionPicker || {};

  /**
   * Flatten grouped function payload data into a dropdown-friendly list.
   *
   * Supports two key filters:
   * - `requireToggleCandidates`: keep only functions that can be used by Toggle Key.
   * - `excludeBindingTypes`: hide binding types that a PI context cannot execute.
   *
   * @param {Array} groups
   * @param {{ requireToggleCandidates?: boolean, excludeBindingTypes?: string[] }} options
   * @returns {Array}
   */
  function flattenFunctionsData(groups, options = {}) {
    const flat = [];
    const requireToggleCandidates = options.requireToggleCandidates === true;
    const excluded = new Set((options.excludeBindingTypes || []).map((t) => String(t || '').toLowerCase()));

    if (!Array.isArray(groups)) {
      return flat;
    }

    groups.forEach((group) => {
      const groupName = group?.label || 'Other';
      const entries = Array.isArray(group?.options) ? group.options : [];

      entries.forEach((opt) => {
        const bindingType = String(opt?.bindingType || '').toLowerCase();

        if (requireToggleCandidates) {
          const isToggleCandidate = opt && opt.details && opt.details.isToggleCandidate === true;
          if (!isToggleCandidate) {
            return;
          }
        }

        // Filter out unsupported options.
        // TODO: When implementing full axis support, stop hiding axis-only options in the PI.
        if (excluded.has(bindingType)) {
          return;
        }

        const disabledReason = String(opt?.disabledReason || '');
        const isUnbound = bindingType === 'unbound';

        // Keep the original category; unbound actions are shown with a warning indicator.
        // Unbound actions are selectable (so users can bind them later), but still visually flagged.
        const isDisabled = !!opt?.disabled && !isUnbound;

        flat.push({
          value: opt?.value,
          legacyValue: opt?.legacyValue,
          text: opt?.text,
          group: groupName,
          details: opt?.details,
          bindingType,
          disabledReason,
          unbound: isUnbound,
          disabled: isDisabled
        });
      });
    });

    return flat;
  }

  /**
   * Render function metadata and per-device bindings into a details panel.
   *
   * The caller controls whether the details container is hidden for empty
   * state and shown again when an option is selected.
   *
   * @param {{
   *   containerEl?: HTMLElement | null,
   *   titleEl?: HTMLElement | null,
   *   descriptionEl?: HTMLElement | null,
   *   bindingElements?: Object,
   *   hideContainerWhenEmpty?: boolean,
   *   showContainerWhenFilled?: boolean,
   *   selectedOption?: Object | null,
   *   slotLabel?: string
   * }} options
   */
  function updateFunctionDetails(options = {}) {
    const containerEl = options.containerEl || null;
    const titleEl = options.titleEl || null;
    const descriptionEl = options.descriptionEl || null;
    const bindingElements = options.bindingElements || {};
    const hideContainerWhenEmpty = options.hideContainerWhenEmpty === true;
    const showContainerWhenFilled = options.showContainerWhenFilled === true;
    const selectedOption = options.selectedOption || null;
    const slotLabel = String(options.slotLabel || '').trim();

    const devices = ['keyboard', 'mouse', 'gamepad', 'joystick'];

    // Empty state: clear all displayed values to avoid stale details.
    if (!selectedOption || !selectedOption.details) {
      if (hideContainerWhenEmpty && containerEl) {
        containerEl.style.display = 'none';
      }

      if (titleEl) {
        titleEl.textContent = '';
      }

      if (descriptionEl) {
        descriptionEl.textContent = '';
      }

      devices.forEach((device) => {
        const el = bindingElements[device] || null;
        if (el) {
          el.textContent = '';
        }
      });

      return;
    }

    if (showContainerWhenFilled && containerEl) {
      containerEl.style.display = 'block';
    }

    const details = selectedOption.details;
    const title = String(details.label || selectedOption.text || '');
    const description = String(details.description || '');
    const detailDevices = Array.isArray(details.devices) ? details.devices : [];

    if (titleEl) {
      titleEl.textContent = slotLabel.length > 0 ? `${slotLabel}: ${title}` : title;
    }

    // Update binding values for all four device types.
    devices.forEach((deviceType) => {
      const deviceData = detailDevices.find((d) => d.device && d.device.toLowerCase() === deviceType);
      const bindingEl = bindingElements[deviceType] || null;

      let bindingValue = 'Unbound';
      if (deviceData && Array.isArray(deviceData.bindings) && deviceData.bindings.length > 0) {
        const bindingLines = deviceData.bindings
          .map((binding) => String(binding.display || binding.raw || ''))
          .filter((text) => text);

        if (bindingLines.length > 0) {
          bindingValue = bindingLines.join(', ');
        }
      }

      if (bindingEl) {
        bindingEl.textContent = bindingValue;
      }
    });

    // Update Description content.
    if (descriptionEl) {
      descriptionEl.textContent = description || 'No description available.';
    }
  }

  functionPicker.flattenFunctionsData = flattenFunctionsData;
  functionPicker.updateFunctionDetails = updateFunctionDetails;
})();
