// Custom Monaco themes for Datateal.
// Mirrors VS Code's Light Modern and Dark Modern token colors.
// Uses require() to wait for Monaco AMD modules to load.
// Signals readiness via window._datatealThemesReady.
require(['vs/editor/editor.main'], function () {
  'use strict';

  monaco.editor.defineTheme('datateal-light', {
    base: 'vs',
    inherit: true,
    colors: {
      'editor.background': '#FFFFFF',
      'editor.foreground': '#3B3B3B',
    },
    rules: [
      // ── Python tokens (Monarch grammar) ──
      { token: 'keyword', foreground: '0000FF' },
      { token: 'keyword.control', foreground: 'AF00DB' },
      { token: 'string', foreground: 'A31515' },
      { token: 'string.escape', foreground: 'EE0000' },
      { token: 'comment', foreground: '008000' },
      { token: 'number', foreground: '098658' },
      { token: 'number.hex', foreground: '098658' },
      { token: 'decorator', foreground: '795E26' },
      { token: 'tag', foreground: '795E26' }, // decorator @ symbol
      { token: 'operator', foreground: '000000' },
      { token: 'delimiter', foreground: '000000' },
      { token: 'type', foreground: '267F99' },
      { token: 'builtin', foreground: '795E26' },
      { token: 'function', foreground: '795E26' },
      { token: 'function.call', foreground: '795E26' },
      { token: 'identifier.python', foreground: '001080' },

      // Python semantic token types (Workstream B)
      { token: 'class', foreground: '267F99' },
      { token: 'parameter', foreground: '001080' },
      { token: 'variable', foreground: '001080' },
      { token: 'selfParameter', foreground: '0000FF' },
      { token: 'property', foreground: '001080' },
      { token: 'namespace', foreground: '267F99' },
      { token: 'enumMember', foreground: '0070C1' },

      // ── DuckDB SQL tokens ──
      { token: 'keyword.sql', foreground: '0000FF' },
      { token: 'predefined.sql', foreground: '795E26' },
      { token: 'type.sql', foreground: '267F99' },
      { token: 'string.sql', foreground: 'A31515' },
      { token: 'string.escape.sql', foreground: 'EE0000' },
      { token: 'number.sql', foreground: '098658' },
      { token: 'number.hex.sql', foreground: '098658' },
      { token: 'comment.sql', foreground: '008000' },
      { token: 'operator.sql', foreground: '000000' },
      { token: 'identifier.quote.sql', foreground: 'A31515' },
      { token: 'variable.sql', foreground: '001080' },
      { token: 'delimiter.sql', foreground: '000000' },
      { token: 'delimiter.bracket.sql', foreground: '000000' },
    ],
  });

  // ── Datateal Dark theme (based on VS Code Dark Modern) ──
  monaco.editor.defineTheme('datateal-dark', {
    base: 'vs-dark',
    inherit: true,
    colors: {
      'editor.background': '#1F1F1F',
      'editor.foreground': '#CCCCCC',
    },
    rules: [
      // ── Python tokens (Monarch grammar) ──
      { token: 'keyword', foreground: '569CD6' },
      { token: 'keyword.control', foreground: 'C586C0' },
      { token: 'string', foreground: 'CE9178' },
      { token: 'string.escape', foreground: 'D7BA7D' },
      { token: 'comment', foreground: '6A9955' },
      { token: 'number', foreground: 'B5CEA8' },
      { token: 'number.hex', foreground: 'B5CEA8' },
      { token: 'decorator', foreground: 'DCDCAA' },
      { token: 'tag', foreground: 'DCDCAA' }, // decorator @ symbol
      { token: 'operator', foreground: 'D4D4D4' },
      { token: 'delimiter', foreground: 'D4D4D4' },
      { token: 'type', foreground: '4EC9B0' },
      { token: 'builtin', foreground: 'DCDCAA' },
      { token: 'function', foreground: 'DCDCAA' },
      { token: 'function.call', foreground: 'DCDCAA' },
      { token: 'identifier.python', foreground: '9CDCFE' },

      // Python semantic token types (Workstream B)
      { token: 'class', foreground: '4EC9B0' },
      { token: 'parameter', foreground: '9CDCFE' },
      { token: 'variable', foreground: '9CDCFE' },
      { token: 'selfParameter', foreground: '569CD6' },
      { token: 'property', foreground: '9CDCFE' },
      { token: 'namespace', foreground: '4EC9B0' },
      { token: 'enumMember', foreground: '4FC1FF' },

      // ── DuckDB SQL tokens ──
      { token: 'keyword.sql', foreground: '569CD6' },
      { token: 'predefined.sql', foreground: 'DCDCAA' },
      { token: 'type.sql', foreground: '4EC9B0' },
      { token: 'string.sql', foreground: 'CE9178' },
      { token: 'string.escape.sql', foreground: 'D7BA7D' },
      { token: 'number.sql', foreground: 'B5CEA8' },
      { token: 'number.hex.sql', foreground: 'B5CEA8' },
      { token: 'comment.sql', foreground: '6A9955' },
      { token: 'operator.sql', foreground: 'D4D4D4' },
      { token: 'identifier.quote.sql', foreground: 'CE9178' },
      { token: 'variable.sql', foreground: '9CDCFE' },
      { token: 'delimiter.sql', foreground: 'D4D4D4' },
      { token: 'delimiter.bracket.sql', foreground: 'D4D4D4' },
    ],
  });

  // Signal that themes are registered.
  window._datatealThemesReady = true;
  if (window._datatealThemesResolve) window._datatealThemesResolve();
});
