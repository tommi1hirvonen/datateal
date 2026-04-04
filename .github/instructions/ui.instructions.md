---
applyTo: src/ui/**
---

# DuckHouse UI

Blazor Web App (`DuckHouse.Ui.proj`) with a server host and an interactive WebAssembly client. Uses Bootstrap (compiled from SCSS with SASS), BlazorBootstrap for ready-made components, and Lucide icons via a custom Roslyn source generator.

## Project structure

| Project | SDK / Target | Role |
|---|---|---|
| `DuckHouse.Ui` | `Microsoft.NET.Sdk.Web` / net10.0 | ASP.NET Core server host; serves static assets and bootstraps WASM |
| `DuckHouse.Ui.Client` | `Microsoft.NET.Sdk.BlazorWebAssembly` / net10.0 | Interactive WASM client; pages, layouts, routing |
| `DuckHouse.Ui.Components` | `Microsoft.NET.Sdk.Razor` / browser | Razor Class Library; shared components, icon generation target |
| `DuckHouse.Ui.Icons` | `Microsoft.NET.Sdk` / netstandard2.0 | `GenerateIconsAttribute` and `Svg` wrapper — no dependencies |
| `DuckHouse.Ui.SourceGeneration` | `Microsoft.NET.Sdk` / netstandard2.0 | Roslyn `IIncrementalGenerator`; generates typed icon properties at compile time |

## Hosting model

`DuckHouse.Ui` is the HTTP host. Its `App.razor` renders `<Routes>` and `<HeadOutlet>` with `@rendermode="InteractiveWebAssembly"`. All interactive pages live in `DuckHouse.Ui.Client`. Routing is defined in `DuckHouse.Ui.Client/Components/Routes.razor` using `<Router AppAssembly="typeof(Program).Assembly">`.

## Styling — Bootstrap + SASS

The app stylesheet is generated from SCSS. **Never edit `bootstrap.custom.css` directly** — always edit the SCSS source and recompile.

- **Source**: `DuckHouse.Ui/wwwroot/css/bootstrap.custom.scss`
  - Imports Bootstrap from `../lib/bootstrap/scss/bootstrap`
  - Contains Lucide icon sizing rules (`.lucide`, `.lucide-sm`) and `.btn .lucide` alignment tweaks
  - Contains Blazor error UI (`#blazor-error-ui`) positioning
- **Compiled output**: `DuckHouse.Ui/wwwroot/css/bootstrap.custom.css`
- **Compile command** (from the `css/` folder):
  ```
  sass bootstrap.custom.scss:bootstrap.custom.css
  ```

## BlazorBootstrap

`Blazor.Bootstrap` (v3.4.0) is referenced from `DuckHouse.Ui.Client`. It is registered in `DuckHouse.Ui.Client/Program.cs`:

```csharp
builder.Services.AddBlazorBootstrap();
```

Its CSS and JS are loaded from `_content/Blazor.Bootstrap/` in `App.razor`. Use BlazorBootstrap components (e.g. `<Button>`, `<Modal>`, `<Grid>`) in WASM client pages and components.

## Lucide icon source generation

Icons are Lucide SVG files. At compile time the source generator reads the SVG files and generates a static `partial class` with one property per icon. **Do not add icons by hand** — drop `.svg` files into the `Icons/lucide/` folder in `DuckHouse.Ui.Components` and rebuild.

### How it works

1. **`DuckHouse.Ui.Icons`** defines two types:
   - `GenerateIconsAttribute(string? cssClass, params string[] iconsLocationPathSegments)` — marks a `public static partial class` for generation.
   - `Svg(string svg)` — wraps raw SVG markup.

2. **`DuckHouse.Ui.SourceGeneration`** contains `IconSourceGenerator : IIncrementalGenerator`:
   - Finds classes annotated with `[GenerateIcons]` (must be `public static partial`).
   - Reads `.svg` files registered as `<AdditionalFiles>` whose path contains the declared path segments.
   - Injects the `cssClass` into each SVG's `class` attribute (merging if already present).
   - Emits `{ClassName}.g.cs` with a property per icon; property names are the kebab-case filename converted to PascalCase.

3. **`DuckHouse.Ui.Components`** is the consumer. `DuckHouse.Ui.Icons` and `DuckHouse.Ui.SourceGeneration` are both referenced as `OutputItemType="Analyzer"`. The `Icons/lucide/` folder is declared as `<AdditionalFiles>`.

4. **`LucideIcon`** is the generated class:
   ```csharp
   [GenerateIcons(cssClass: "lucide", "Icons", "lucide")]
   public static partial class LucideIcon;
   // Generated:
   // public static Svg Info => new Svg("<svg class=\"lucide\" .../>"); 
   // public static Svg OctagonX => new Svg("<svg class=\"lucide\" .../>"); 
   ```

5. **`SvgIcon`** is a Blazor component that renders an `Svg` value:
   ```razor
   <SvgIcon Icon="LucideIcon.Info" />
   ```

## Key files

```
src/ui/
  DuckHouse.Ui/
    Program.cs                         — server startup; MapRazorComponents, MapStaticAssets
    Components/
      App.razor                        — HTML shell; loads Bootstrap CSS/JS, BlazorBootstrap assets
      Pages/Error.razor                — server-side error page
    wwwroot/css/
      bootstrap.custom.scss            — SCSS source (edit this)
      bootstrap.custom.css             — compiled output (do not edit)
  DuckHouse.Ui.Client/
    Program.cs                         — AddBlazorBootstrap(), WebAssemblyHostBuilder
    Components/
      Routes.razor                     — client-side router
      Layout/MainLayout.razor          — default layout
      Pages/
        Home.razor                     — home page
        NotFound.razor                 — 404 page
  DuckHouse.Ui.Components/
    LucideIcon.cs                      — [GenerateIcons] partial class (generation target)
    SvgIcon.cs                         — ComponentBase that renders Svg.Text as markup
    Icons/lucide/                      — Lucide SVG source files (AdditionalFiles for generator)
    wwwroot/defaults.css               — component-scoped CSS
  DuckHouse.Ui.Icons/
    GenerateIconsAttribute.cs          — attribute definition
    Svg.cs                             — SVG wrapper record
  DuckHouse.Ui.SourceGeneration/
    IconSourceGenerator.cs             — IIncrementalGenerator implementation
    Extensions.cs                      — kebab-case → PascalCase helper
    IconData.cs                        — SVG file path + text
    IconsClassData.cs                  — decorated class metadata
    DiagnosticDescriptors.cs           — ICON001: incorrect class modifiers
```
