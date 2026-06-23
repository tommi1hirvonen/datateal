// JS initializer for the Datateal.Ui.Server assembly.
// Progress is driven by --blazor-load-percentage / --blazor-load-percentage-text CSS variables
// that Blazor sets automatically during WASM resource loading — no custom tracking needed.

export function beforeWebStart() {
  // Expose a removal function that MainLayout calls on its first render.
  window.removeWasmLoadingOverlay = function () {
    const el = document.getElementById('wasm-loading-overlay');
    if (!el) return;
    el.style.transition = 'opacity 0.35s ease';
    el.style.opacity = '0';
    setTimeout(() => el.remove(), 350);
  };
}

export function afterWebAssemblyStarted() {
  // Fallback: remove overlay after 1.5 s in case MainLayout never fires
  // (e.g. auth redirect before first render).
  setTimeout(() => window.removeWasmLoadingOverlay?.(), 1500);
}
