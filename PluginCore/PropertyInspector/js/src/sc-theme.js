//// ****************************************************************
// * SC Theme Picker
// * Shared theme dropdown logic for Property Inspector pages
//// ****************************************************************

(function () {
  const root = globalThis;
  const SCPI = root.SCPI = root.SCPI || {};
  const THEME_STORAGE_KEY = 'scsd.selectedTheme';

  function applyTheme(themeFile, linkEl) {
    if (!themeFile || typeof themeFile !== 'string') {
      return;
    }

    if (linkEl) {
      linkEl.href = `../css/themes/${themeFile}`;
    }

    try {
      localStorage.setItem(THEME_STORAGE_KEY, themeFile);
    } catch (_) {
      // Ignore storage errors.
    }
  }

  function initThemeDropdown(options = {}) {
    const rootId = options.rootId || 'themeDropdown';
    const linkId = options.linkId || 'pi-theme-styles';

    const themeLinkEl = document.getElementById(linkId);
    if (!themeLinkEl) {
      return;
    }

    let availableThemes = [];

    SCPI.bus?.start?.();

    const dropdown = SCPI.ui?.dropdown?.initDropdown?.({
      rootId,
      searchEnabled: false,
      displaySelectedInInput: true,
      minLoadingMs: 500,
      successFlashMs: 220,
      emptyText: 'No themes found',
      getText: (t) => String(t?.name ?? ''),
      getValue: (t) => String(t?.file ?? ''),
      onSelect: (t) => {
        const file = String(t?.file ?? '');
        if (!file) {
          return;
        }

        applyTheme(file, themeLinkEl);
        (SCPI.bus?.send || SCPI.util?.sendToPlugin || root.sendToPlugin)?.('setTheme', {themeFile: file});
      }
    });

    dropdown?.setLoading?.(true, 'Loading themes');

    // Receive themes + selected theme from plugin (via shared bus)
    SCPI.bus?.on?.((payload) => {
      if (!payload) {
        return;
      }

      if (payload.themesLoaded) {
        availableThemes = payload.themes || [];
        dropdown?.setItems?.(availableThemes);
        dropdown?.setLoading?.(false);
      }

      if (typeof payload.selectedTheme === 'string' && payload.selectedTheme.length > 0) {
        const file = payload.selectedTheme;
        applyTheme(file, themeLinkEl);
        dropdown?.setSelectedValue?.(file, {rerender: true});
      }
    });

    // Ensure dropdown reflects the currently loaded theme (from preload script).
    try {
      const href = themeLinkEl.getAttribute('href') || '';
      const file = href.split('/').pop().split('?')[0];
      if (file) {
        applyTheme(file, themeLinkEl);
        dropdown?.setSelectedValue?.(file, {rerender: true});
      }
    } catch (_) {
      // Ignore.
    }
  }

  SCPI.theme = {
    initThemeDropdown
  };

  // Back-compat
  root.SCTheme = root.SCTheme || SCPI.theme;
})();
