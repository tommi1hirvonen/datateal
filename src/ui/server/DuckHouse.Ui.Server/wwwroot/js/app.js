// Theme interop helpers called by ThemeService and CodeCell.
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
    var monacoTheme = effective === 'dark' ? 'vs-dark' : 'vs';
    if (typeof monaco !== 'undefined') {
        monaco.editor.setTheme(monacoTheme);
    }
};
window.getStoredDuckhouseTheme = function () {
    return localStorage.getItem('duckhouse-theme') || 'auto';
};
window.getDuckhouseMonacoTheme = function () {
    var link = document.getElementById('ant-theme-css');
    return link && link.href.includes('dark') ? 'vs-dark' : 'vs';
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