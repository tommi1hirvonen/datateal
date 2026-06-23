// Drag-splitter helpers for resizable panel dividers.
// initCatalogPanelSplitter: called by CatalogSidePanel.razor and CatalogsPage.razor.
// initQueryPageSplitter: called by QueryPage.razor.

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
