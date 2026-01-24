//// ****************************************************************
// * SC PI Bus
// * Single sendToPropertyInspector subscription + sendToPlugin helpers
//// ****************************************************************

(function () {
  const root = globalThis;
  const SCPI = root.SCPI = root.SCPI || {};

  const listeners = new Set();
  const sendOnceKeys = new Set();
  let started = false;
  let attached = false;

  function getClient() {
    return root.SDPIComponents?.streamDeckClient || null;
  }

  function send(event, payload = {}) {
    const client = getClient();
    if (!client?.send) {
      console.warn('[sc-bus] SDPI streamDeckClient.send not available');
      return;
    }
    client.send('sendToPlugin', {event, ...payload});
  }

  function sendOnce(key, event, payload = {}) {
    const k = String(key || '').trim();
    if (!k) {
      send(event, payload);
      return;
    }
    if (sendOnceKeys.has(k)) {
      return;
    }
    sendOnceKeys.add(k);
    send(event, payload);
  }

  function on(listener) {
    if (typeof listener !== 'function') {
      return () => {
      };
    }
    listeners.add(listener);
    return () => listeners.delete(listener);
  }

  function start() {
    if (started) {
      return;
    }
    started = true;

    const tryAttach = () => {
      if (attached) {
        return;
      }

      const client = getClient();
      const sub = client?.sendToPropertyInspector?.subscribe;
      if (typeof sub !== 'function') {
        // sdpi-components.js is usually defer-loaded; wait a tick.
        setTimeout(tryAttach, 0);
        return;
      }

      attached = true;
      client.sendToPropertyInspector.subscribe((data) => {
        if (data?.event !== 'sendToPropertyInspector' || !data.payload) {
          return;
        }

        for (const fn of Array.from(listeners)) {
          try {
            fn(data.payload, data);
          } catch (err) {
            console.error('[sc-bus] listener failed', err);
          }
        }
      });
    };

    tryAttach();
  }

  SCPI.bus = {
    start,
    on,
    send,
    sendOnce
  };
})();
