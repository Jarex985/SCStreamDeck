//// ****************************************************************
// * SC PI Preload
// * Runs before paint to apply last-used theme (prevents flash)
//// ****************************************************************

(function () {
  try {
    const key = 'scsd.selectedTheme';
    const cached = localStorage.getItem(key);
    const file = (typeof cached === 'string' && cached.trim().endsWith('.css'))
      ? cached.trim()
      : 'default.css';

    const link = document.getElementById('pi-theme-styles');
    if (link) {
      link.href = `../css/themes/${file}`;
    }
  } catch (_) {
    // Ignore storage errors.
  }
})();
