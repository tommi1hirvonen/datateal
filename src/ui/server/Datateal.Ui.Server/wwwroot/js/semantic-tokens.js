// Semantic token helpers for Python code cells, called by KernelCodeCell.razor.

// Token type legend — must match the order used in KernelCodeCell.razor and the theme rules.
window._datatealSemanticTokenLegend = {
  tokenTypes: [
    'function', // 0
    'class', // 1
    'parameter', // 2
    'variable', // 3
    'selfParameter', // 4
    'builtin', // 5
    'property', // 6
    'decorator', // 7
    'namespace', // 8
    'enumMember', // 9
  ],
  tokenModifiers: [],
};

// Global singleton registry: one provider dispatches to all cells by model URI.
window._datatealSemanticTokensRegistry = {
  cells: {}, // modelUri → dotNetRef
  registered: false,
  disposable: null,
};

// Register a cell's dotNetRef for semantic tokens. Lazily creates the single
// global provider on first call.
window.registerSemanticTokensCell = function (dotNetRef, modelUri) {
  var registry = window._datatealSemanticTokensRegistry;
  registry.cells[modelUri] = dotNetRef;

  if (!registry.registered) {
    var legend = window._datatealSemanticTokenLegend;
    registry.disposable = monaco.languages.registerDocumentSemanticTokensProvider(
      'datateal-python',
      {
        getLegend: function () {
          return legend;
        },
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
        releaseDocumentSemanticTokens: function () {},
      }
    );
    registry.registered = true;
  }
};

// Remove a cell from the registry when it is disposed.
window.unregisterSemanticTokensCell = function (modelUri) {
  delete window._datatealSemanticTokensRegistry.cells[modelUri];
};
