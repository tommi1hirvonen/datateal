// Custom Python Monarch grammar for DuckHouse.
// Registers 'duckhouse-python' with differentiated keyword categories,
// built-in type/function recognition, and function call detection.
// Token colors align with VS Code Light Modern / Dark Modern themes.
require(['vs/editor/editor.main'], function () {
    'use strict';

    monaco.languages.register({
        id: 'duckhouse-python',
        extensions: ['.py'],
        aliases: ['Python'],
    });

    monaco.languages.setLanguageConfiguration('duckhouse-python', {
        comments: { lineComment: '#', blockComment: ["'''", "'''"] },
        brackets: [['{', '}'], ['[', ']'], ['(', ')']],
        autoClosingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"', notIn: ['string'] },
            { open: "'", close: "'", notIn: ['string', 'comment'] },
        ],
        surroundingPairs: [
            { open: '{', close: '}' },
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: "'", close: "'" },
        ],
        onEnterRules: [
            {
                beforeText: new RegExp(
                    '^\\s*(?:def|class|for|if|elif|else|while|try|with|finally|except|async|match|case).*?:\\s*$'
                ),
                action: { indentAction: monaco.languages.IndentAction.Indent },
            },
        ],
        folding: {
            offSide: true,
            markers: {
                start: new RegExp('^\\s*#region\\b'),
                end: new RegExp('^\\s*#endregion\\b'),
            },
        },
    });

    monaco.languages.setMonarchTokensProvider('duckhouse-python', {
        defaultToken: '',
        tokenPostfix: '.python',

        // ── Control-flow & import keywords → magenta ──
        controlKeywords: [
            'if', 'elif', 'else', 'for', 'while', 'try', 'except', 'finally',
            'with', 'break', 'continue', 'pass', 'return', 'raise', 'yield',
            'assert', 'import', 'from', 'as', 'async', 'await', 'match', 'case',
        ],

        // ── Definition / operator / constant keywords → blue ──
        keywords: [
            'def', 'class', 'lambda', 'type',
            'and', 'or', 'not', 'in', 'is', 'del', 'global', 'nonlocal',
            'True', 'False', 'None',
            'self', 'cls',
        ],

        // ── Built-in types & exceptions → teal ──
        builtinTypes: [
            'int', 'float', 'complex', 'str', 'bytes', 'bytearray',
            'bool', 'list', 'tuple', 'dict', 'set', 'frozenset',
            'range', 'memoryview', 'object',
            'Exception', 'BaseException', 'ArithmeticError', 'AssertionError',
            'AttributeError', 'BlockingIOError', 'BrokenPipeError', 'BufferError',
            'ChildProcessError', 'ConnectionAbortedError', 'ConnectionError',
            'ConnectionRefusedError', 'ConnectionResetError', 'EOFError',
            'FileExistsError', 'FileNotFoundError', 'FloatingPointError',
            'GeneratorExit', 'IOError', 'ImportError', 'IndentationError',
            'IndexError', 'InterruptedError', 'IsADirectoryError', 'KeyError',
            'KeyboardInterrupt', 'LookupError', 'MemoryError',
            'ModuleNotFoundError', 'NameError', 'NotADirectoryError',
            'NotImplementedError', 'OSError', 'OverflowError',
            'PermissionError', 'ProcessLookupError', 'RecursionError',
            'ReferenceError', 'RuntimeError', 'StopAsyncIteration',
            'StopIteration', 'SyntaxError', 'SystemError', 'SystemExit',
            'TabError', 'TimeoutError', 'TypeError', 'UnboundLocalError',
            'UnicodeDecodeError', 'UnicodeEncodeError', 'UnicodeError',
            'UnicodeTranslateError', 'ValueError', 'Warning', 'ZeroDivisionError',
            'DeprecationWarning', 'FutureWarning', 'ImportWarning',
            'PendingDeprecationWarning', 'ResourceWarning', 'RuntimeWarning',
            'SyntaxWarning', 'UnicodeWarning', 'UserWarning', 'BytesWarning',
        ],

        // ── Built-in functions → gold ──
        builtinFunctions: [
            'abs', 'aiter', 'all', 'any', 'anext', 'ascii',
            'bin', 'breakpoint', 'callable', 'chr', 'classmethod', 'compile',
            'delattr', 'dir', 'divmod', 'enumerate', 'eval', 'exec',
            'filter', 'format', 'getattr', 'globals', 'hasattr', 'hash',
            'help', 'hex', 'id', 'input', 'isinstance', 'issubclass', 'iter',
            'len', 'locals', 'map', 'max', 'min', 'next',
            'oct', 'open', 'ord', 'pow', 'print', 'property',
            'repr', 'reversed', 'round', 'setattr', 'slice', 'sorted',
            'staticmethod', 'sum', 'super', 'vars', 'zip', '__import__',
        ],

        brackets: [
            { open: '{', close: '}', token: 'delimiter.curly' },
            { open: '[', close: ']', token: 'delimiter.bracket' },
            { open: '(', close: ')', token: 'delimiter.parenthesis' },
        ],

        tokenizer: {
            root: [
                // Whitespace & comments
                [/\s+/, 'white'],
                [/#.*$/, 'comment'],

                // Numbers
                { include: '@numbers' },

                // Triple-quoted strings (before single-line strings)
                [/(?:[rR][fF]|[fF][rR]?)'''/, 'string', '@fTripleSingleString'],
                [/(?:[rR][fF]|[fF][rR]?)"""/, 'string', '@fTripleDoubleString'],
                [/[rRbBuU]{0,2}'''/, 'string', '@tripleSingleString'],
                [/[rRbBuU]{0,2}"""/, 'string', '@tripleDoubleString'],

                // F-strings (single-line)
                [/(?:[rR][fF]|[fF][rR]?)'/, 'string.escape', '@fStringBody'],
                [/(?:[rR][fF]|[fF][rR]?)"/, 'string.escape', '@fDblStringBody'],

                // Regular strings with optional prefix
                [/[rRbBuU]{0,2}'/, 'string.escape', '@stringBody'],
                [/[rRbBuU]{0,2}"/, 'string.escape', '@dblStringBody'],

                // Delimiters & brackets
                [/[,.:;]/, 'delimiter'],
                [/[{}[\]()]/, '@brackets'],

                // Operators
                [/->|:=|\*\*=?|\/\/=?|<<=?|>>=?|[+\-*/%&|^~<>!=]=?/, 'operator'],

                // Decorators
                [/@[a-zA-Z_]\w*/, 'decorator'],

                // def → highlight following name as function definition
                [/\bdef\b/, { token: 'keyword', next: '@functionDef' }],
                // class → highlight following name as type definition
                [/\bclass\b/, { token: 'keyword', next: '@classDef' }],

                // Identifier followed by ( → function call (or keyword/type/builtin)
                [/[a-zA-Z_]\w*(?=\s*\()/, {
                    cases: {
                        '@controlKeywords': 'keyword.control',
                        '@keywords': 'keyword',
                        '@builtinTypes': 'type',
                        '@builtinFunctions': 'builtin',
                        '@default': 'function.call',
                    },
                }],

                // Regular identifier
                [/[a-zA-Z_]\w*/, {
                    cases: {
                        '@controlKeywords': 'keyword.control',
                        '@keywords': 'keyword',
                        '@builtinTypes': 'type',
                        '@builtinFunctions': 'builtin',
                        '@default': 'identifier',
                    },
                }],
            ],

            // ── def name state ──
            functionDef: [
                [/\s+/, 'white'],
                [/[a-zA-Z_]\w*/, 'function', '@pop'],
                [/./, 'delimiter', '@pop'],
            ],

            // ── class name state ──
            classDef: [
                [/\s+/, 'white'],
                [/[a-zA-Z_]\w*/, 'type', '@pop'],
                [/./, 'delimiter', '@pop'],
            ],

            // ── Numbers ──
            numbers: [
                [/-?0[xX][\da-fA-F][_\da-fA-F]*[lL]?/, 'number.hex'],
                [/-?0[oO][0-7][_0-7]*[lL]?/, 'number.octal'],
                [/-?0[bB][01][_01]*[lL]?/, 'number.binary'],
                [/-?(\d[\d_]*\.)?(\d[\d_]*)([eE][+-]?\d[\d_]*)?[jJ]?[lL]?/, 'number'],
            ],

            // ── Triple-quoted strings ──
            tripleSingleString: [
                [/[^'\\]+/, 'string'],
                [/\\./, 'string.escape'],
                [/'''/, 'string', '@popall'],
                [/'/, 'string'],
            ],
            tripleDoubleString: [
                [/[^"\\]+/, 'string'],
                [/\\./, 'string.escape'],
                [/"""/, 'string', '@popall'],
                [/"/, 'string'],
            ],

            // ── Triple-quoted f-strings (with interpolation) ──
            fTripleSingleString: [
                [/[^'\\{]+/, 'string'],
                [/\\./, 'string.escape'],
                [/\{[^}':!=]+/, 'identifier', '@fStringDetail'],
                [/'''/, 'string', '@popall'],
                [/['{]/, 'string'],
            ],
            fTripleDoubleString: [
                [/[^"\\{]+/, 'string'],
                [/\\./, 'string.escape'],
                [/\{[^}':!=]+/, 'identifier', '@fStringDetail'],
                [/"""/, 'string', '@popall'],
                [/["{]/, 'string'],
            ],

            // ── Single-line strings ──
            stringBody: [
                [/[^\\']+$/, 'string', '@popall'],
                [/[^\\']+/, 'string'],
                [/\\./, 'string.escape'],
                [/'/, 'string.escape', '@popall'],
                [/\\$/, 'string'],
            ],
            dblStringBody: [
                [/[^\\"]+$/, 'string', '@popall'],
                [/[^\\"]+/, 'string'],
                [/\\./, 'string.escape'],
                [/"/, 'string.escape', '@popall'],
                [/\\$/, 'string'],
            ],

            // ── F-strings (single-line, with interpolation) ──
            fStringBody: [
                [/[^\\'{]+$/, 'string', '@popall'],
                [/[^\\'{]+/, 'string'],
                [/\{[^}':!=]+/, 'identifier', '@fStringDetail'],
                [/\\./, 'string.escape'],
                [/'/, 'string.escape', '@popall'],
                [/\\$/, 'string'],
            ],
            fDblStringBody: [
                [/[^\\"{]+$/, 'string', '@popall'],
                [/[^\\"{]+/, 'string'],
                [/\{[^}':!=]+/, 'identifier', '@fStringDetail'],
                [/\\./, 'string.escape'],
                [/"/, 'string.escape', '@popall'],
                [/\\$/, 'string'],
            ],

            // ── f-string interpolation detail ──
            fStringDetail: [
                [/[:][^}]+/, 'string'],
                [/[!][ars]/, 'string'],
                [/=/, 'string'],
                [/\}/, 'identifier', '@pop'],
            ],
        },
    });

    // Signal readiness.
    window._duckhousePythonReady = true;
    if (window._duckhousePythonResolve) window._duckhousePythonResolve();
});
