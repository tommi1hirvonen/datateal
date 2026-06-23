// Monaco editor interop helpers called by CodeCell.razor.
// Depends on window.datatealMonacoReady defined in theme.js.

// Waits for Monaco themes/language to be ready, then applies the correct
// theme and re-sets the model language (in case the editor was created before
// the custom language was registered). Called from CodeCell.razor OnEditorInitAsync.
window.applyDatatealMonacoTheme = async function (editorId, language) {
  await window.datatealMonacoReady;
  var theme = window.getDatatealMonacoTheme();
  if (typeof monaco !== 'undefined') {
    monaco.editor.setTheme(theme);
    // Always re-apply language to ensure correct tokenization after registration
    if (language) {
      var holder = window.blazorMonaco?.editors?.find(function (h) {
        return h.id === editorId;
      });
      if (holder) {
        var model = holder.editor.getModel();
        if (model) {
          monaco.editor.setModelLanguage(model, language);
        }
      }
    }
  }
};

window.setMonacoEditorLanguage = function (editorId, language) {
  const holder = window.blazorMonaco?.editors?.find((h) => h.id === editorId);
  if (holder) {
    const model = holder.editor.getModel();
    if (model) monaco.editor.setModelLanguage(model, language);
  }
};

window.getMonacoEditorSelection = function (editorId) {
  const holder = window.blazorMonaco?.editors?.find((h) => h.id === editorId);
  if (!holder) return '';
  const sel = holder.editor.getSelection();
  if (!sel || sel.isEmpty()) return '';
  return holder.editor.getModel().getValueInRange(sel);
};

window.registerMonacoExecuteCommand = function (editorId, dotNetRef) {
  const holder = window.blazorMonaco?.editors?.find((h) => h.id === editorId);
  if (!holder) return;
  holder.editor.addAction({
    id: 'datateal-execute-' + editorId,
    label: 'Execute',
    keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter],
    run: function () {
      dotNetRef.invokeMethodAsync('TriggerExecuteAsync');
    },
  });
};

// Forces Monaco to re-measure its container on the next animation frame.
// Called after editor init on SPA navigation where the flexbox container may not
// have settled to its final dimensions by the time Monaco first measures it.
window.relayoutMonacoEditor = function (editorId) {
  requestAnimationFrame(function () {
    const holder = window.blazorMonaco?.editors?.find((h) => h.id === editorId);
    if (holder) holder.editor.layout();
  });
};
