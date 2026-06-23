// Notebook and query page interop helpers.
// scrollNotebookToCell: called by NotebookProgressGutter.razor.
// getItemNodePref / setItemNodePref: called by NotebookPage.razor and QueryPage.razor.

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

window.getItemNodePref = function (itemId) {
  return localStorage.getItem('datateal-node:' + itemId) || '';
};

window.setItemNodePref = function (itemId, nodeName) {
  localStorage.setItem('datateal-node:' + itemId, nodeName);
};
