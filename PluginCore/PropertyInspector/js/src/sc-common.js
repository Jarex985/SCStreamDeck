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
