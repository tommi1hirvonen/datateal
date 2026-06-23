// Theme interop helpers called by ThemeService.
// Also owns the Monaco readiness promise which other scripts depend on.

// ── Monaco readiness promise ──
// Resolves when custom themes, DuckDB language, and Python language are all registered.
window.datatealMonacoReady = new Promise(function (resolve) {
  function check() {
    if (
      window._datatealThemesReady &&
      window._datatealLanguageReady &&
      window._datatealPythonReady
    ) {
      resolve();
    }
  }
  // If already ready (race-safe)
  check();
  // Hook into the resolve callbacks set by the individual scripts
  window._datatealThemesResolve = check;
  window._datatealLanguageResolve = check;
  window._datatealPythonResolve = check;
});

window.setDatatealTheme = function (theme) {
  localStorage.setItem('datateal-theme', theme);
  var prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  var effective =
    theme === 'dark' ? 'dark' : theme === 'light' ? 'light' : prefersDark ? 'dark' : 'light';
  var link = document.getElementById('ant-theme-css');
  if (link) {
    link.href =
      effective === 'dark'
        ? '_content/AntDesign/css/ant-design-blazor.dark.css'
        : '_content/AntDesign/css/ant-design-blazor.css';
  }
  document.documentElement.classList.toggle('ant-dark', effective === 'dark');
  var monacoTheme = effective === 'dark' ? 'datateal-dark' : 'datateal-light';
  window.datatealMonacoReady.then(function () {
    if (typeof monaco !== 'undefined') {
      monaco.editor.setTheme(monacoTheme);
    }
  });
};

window.getStoredDatatealTheme = function () {
  return localStorage.getItem('datateal-theme') || 'auto';
};

window.getDatatealMonacoTheme = function () {
  var link = document.getElementById('ant-theme-css');
  return link && link.href.includes('dark') ? 'datateal-dark' : 'datateal-light';
};
