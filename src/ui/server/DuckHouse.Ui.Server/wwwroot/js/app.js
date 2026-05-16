// Theme interop helpers called by ThemeService and CodeCell.

// ── Monaco readiness promise ──
// Resolves when custom themes, DuckDB language, and Python language are all registered.
window.duckhouseMonacoReady = new Promise(function (resolve) {
    function check() {
        if (window._duckhouseThemesReady && window._duckhouseLanguageReady && window._duckhousePythonReady) {
            resolve();
        }
    }
    // If already ready (race-safe)
    check();
    // Hook into the resolve callbacks set by the individual scripts
    window._duckhouseThemesResolve = check;
    window._duckhouseLanguageResolve = check;
    window._duckhousePythonResolve = check;
});

// Waits for Monaco themes/language to be ready, then applies the correct
// theme and re-sets the model language (in case the editor was created before
// the custom language was registered). Called from CodeCell.razor OnEditorInitAsync.
window.applyDuckhouseMonacoTheme = async function (editorId, language) {
    await window.duckhouseMonacoReady;
    var theme = window.getDuckhouseMonacoTheme();
    if (typeof monaco !== 'undefined') {
        monaco.editor.setTheme(theme);
        // Always re-apply language to ensure correct tokenization after registration
        if (language) {
            var holder = window.blazorMonaco?.editors?.find(function (h) { return h.id === editorId; });
            if (holder) {
                var model = holder.editor.getModel();
                if (model) {
                    monaco.editor.setModelLanguage(model, language);
                }
            }
        }
    }
};

window.setDuckhouseTheme = function (theme) {
    localStorage.setItem('duckhouse-theme', theme);
    var prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    var effective = theme === 'dark' ? 'dark' : theme === 'light' ? 'light' : (prefersDark ? 'dark' : 'light');
    var link = document.getElementById('ant-theme-css');
    if (link) {
        link.href = effective === 'dark'
            ? '_content/AntDesign/css/ant-design-blazor.dark.css'
            : '_content/AntDesign/css/ant-design-blazor.css';
    }
    document.documentElement.classList.toggle('ant-dark', effective === 'dark');
    var monacoTheme = effective === 'dark' ? 'duckhouse-dark' : 'duckhouse-light';
    window.duckhouseMonacoReady.then(function () {
        if (typeof monaco !== 'undefined') {
            monaco.editor.setTheme(monacoTheme);
        }
    });
};
window.getStoredDuckhouseTheme = function () {
    return localStorage.getItem('duckhouse-theme') || 'auto';
};
window.getDuckhouseMonacoTheme = function () {
    var link = document.getElementById('ant-theme-css');
    return link && link.href.includes('dark') ? 'duckhouse-dark' : 'duckhouse-light';
};
window.setMonacoEditorLanguage = function (editorId, language) {
    const holder = window.blazorMonaco?.editors?.find(h => h.id === editorId);
    if (holder) {
        const model = holder.editor.getModel();
        if (model) monaco.editor.setModelLanguage(model, language);
    }
};

window.openFileAsText = function (inputElement) {
    return new Promise((resolve, reject) => {
        const file = inputElement.files[0];
        if (!file) { reject('No file selected'); return; }
        const reader = new FileReader();
        reader.onload = e => resolve(e.target.result);
        reader.onerror = () => reject('Failed to read file');
        reader.readAsText(file);
    });
};

window.downloadFileBytes = function (filename, bytes, mimeType) {
    const blob = new Blob([new Uint8Array(bytes)], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

window.downloadFile = function (filename, content) {
    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

window.clickElement = function (element) {
    element.click();
};

window.getMonacoEditorSelection = function (editorId) {
    const holder = window.blazorMonaco?.editors?.find(h => h.id === editorId);
    if (!holder) return '';
    const sel = holder.editor.getSelection();
    if (!sel || sel.isEmpty()) return '';
    return holder.editor.getModel().getValueInRange(sel);
};

window.registerMonacoExecuteCommand = function (editorId, dotNetRef) {
    const holder = window.blazorMonaco?.editors?.find(h => h.id === editorId);
    if (!holder) return;
    holder.editor.addAction({
        id: 'duckhouse-execute-' + editorId,
        label: 'Execute',
        keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter],
        run: function () { dotNetRef.invokeMethodAsync('TriggerExecuteAsync'); }
    });
};

window.getItemNodePref = function (itemId) {
    return localStorage.getItem('duckhouse-node:' + itemId) || '';
};

window.setItemNodePref = function (itemId, nodeName) {
    localStorage.setItem('duckhouse-node:' + itemId, nodeName);
};

// Scrolls the notebook cell container so that the top of the given cell
// is aligned with the top of the visible scroll area.
window.scrollNotebookToCell = function (cellId, containerId) {
    const container = document.getElementById(containerId);
    const cell = document.getElementById('nb-cell-' + cellId);
    if (!container || !cell) return;
    const containerRect = container.getBoundingClientRect();
    const cellRect = cell.getBoundingClientRect();
    container.scrollTop += cellRect.top - containerRect.top;
};

window.initCatalogPanelSplitter = function (panelId, handleId, dotNetRef) {
    const panel = document.getElementById(panelId);
    const handle = document.getElementById(handleId);
    if (!panel || !handle) return;

    let dragging = false;
    let startX = 0;
    let startWidth = 0;

    handle.addEventListener('mousedown', function (e) {
        dragging = true;
        startX = e.clientX;
        startWidth = panel.offsetWidth;
        document.body.style.cursor = 'ew-resize';
        document.body.style.userSelect = 'none';
        e.preventDefault();
    });

    document.addEventListener('mousemove', function (e) {
        if (!dragging) return;
        const delta = e.clientX - startX;
        const newWidth = Math.max(160, startWidth + delta);
        panel.style.width = newWidth + 'px';
    });

    document.addEventListener('mouseup', function (e) {
        if (!dragging) return;
        dragging = false;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        dotNetRef.invokeMethodAsync('OnCatalogPanelDragEnd', panel.offsetWidth);
    });
};

window.initQueryPageSplitter = function (topPaneId, handleId, dotNetRef) {
    const topPane = document.getElementById(topPaneId);
    const handle = document.getElementById(handleId);
    if (!topPane || !handle) return;

    let dragging = false;
    let startY = 0;
    let startHeight = 0;

    handle.addEventListener('mousedown', function (e) {
        dragging = true;
        startY = e.clientY;
        startHeight = topPane.offsetHeight;
        document.body.style.cursor = 'ns-resize';
        document.body.style.userSelect = 'none';
        e.preventDefault();
    });

    document.addEventListener('mousemove', function (e) {
        if (!dragging) return;
        const delta = e.clientY - startY;
        const newHeight = Math.max(80, startHeight + delta);
        topPane.style.height = newHeight + 'px';
    });

    document.addEventListener('mouseup', function (e) {
        if (!dragging) return;
        dragging = false;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        dotNetRef.invokeMethodAsync('OnSplitterDragEnd', topPane.offsetHeight);
    });
};

// ── Semantic tokens for Python ──
// Token type legend — must match the order used in KernelCodeCell.razor and the theme rules.
window._duckhouseSemanticTokenLegend = {
    tokenTypes: [
        'function',       // 0
        'class',          // 1
        'parameter',      // 2
        'variable',       // 3
        'selfParameter',  // 4
        'builtin',        // 5
        'property',       // 6
        'decorator',      // 7
        'namespace',      // 8
        'enumMember',     // 9
    ],
    tokenModifiers: [],
};

// Global singleton registry: one provider dispatches to all cells by model URI.
window._duckhouseSemanticTokensRegistry = {
    cells: {},           // modelUri → dotNetRef
    registered: false,
    disposable: null,
};

// Register a cell's dotNetRef for semantic tokens. Lazily creates the single
// global provider on first call.
window.registerSemanticTokensCell = function (dotNetRef, modelUri) {
    var registry = window._duckhouseSemanticTokensRegistry;
    registry.cells[modelUri] = dotNetRef;

    if (!registry.registered) {
        var legend = window._duckhouseSemanticTokenLegend;
        registry.disposable = monaco.languages.registerDocumentSemanticTokensProvider('duckhouse-python', {
            getLegend: function () { return legend; },
            provideDocumentSemanticTokens: async function (model, lastResultId, token) {
                var uri = model.uri.toString();
                var ref = registry.cells[uri];
                if (!ref) return { data: new Uint32Array(0) };
                try {
                    // Fetch fresh tokens from the runtime — no caching, no race conditions.
                    var encoded = await ref.invokeMethodAsync('ProvideSemanticTokensAsync');
                    if (!encoded || encoded.length === 0) return { data: new Uint32Array(0) };
                    return { data: new Uint32Array(encoded) };
                } catch {
                    return { data: new Uint32Array(0) };
                }
            },
            releaseDocumentSemanticTokens: function () { },
        });
        registry.registered = true;
    }
};

// Remove a cell from the registry when it is disposed.
window.unregisterSemanticTokensCell = function (modelUri) {
    delete window._duckhouseSemanticTokensRegistry.cells[modelUri];
};