# TMS.Z.Blazor.Diagrams

## Changes from Original Z.Blazor.Diagrams

This fork contains additional features and fixes developed for the TMS (Transportation Management System). Below are the main changes:

### рџљЂ New Features

#### 1. Node Rotation Support - v3.0.3.1
- **Description**: Added full support for node rotation with configurable pivot point
- **Model Changes**:
  - `NodeModel`: added `Rotation`, `RotationPivotX`, `RotationPivotY` properties
  - Events: `RotationChanged`, `RotationPivotChanged`
  - Automatic AABB (Axis-Aligned Bounding Box) recalculation for correct bounds
- **Visualization**:
  - `NodeRenderer`: container rotation with pivot point consideration (CSS `transform-origin` / `translate+rotate`)
  - Support for both HTML and SVG rendering
- **Compatibility**: No breaking changes - new properties are optional
- **Version**: 3.0.3.1 (TMS.Z.Blazor.Diagrams, TMS.Z.Blazor.Diagrams.Core)

#### 2. Rendering and Performance Optimization - v3.0.3.7
- **Performance Improvements**: Rendering optimization and pointer event handling
- **Virtualization**: Enhanced node visibility handling with size change subscriptions

#### 3. Non-Interactive Nodes Support - v3.0.3.8
- **Description**: Added ability to make individual nodes non-interactive without JavaScript interop
- **Use Case**: Disable user interaction with specific nodes while keeping them visible
- **Model Changes**:
  - `NodeModel`: added `Interactable` property (default: `true`)
  - `DiagramOptions`: added `NonInteractableNodeCssClass` property (default: `"non-interactable"`)
- **Visualization**:
  - `NodeRenderer`: automatically applies CSS class when `Interactable = false`
  - CSS class blocks pointer events via project-configurable class name
  - No JavaScript required - changes take effect on next render
- **Benefits**:
  - Zero JS interop overhead for dynamic interaction state changes
  - Visual feedback through CSS (can be customized per project)
  - Maintains node visibility while preventing selection/drag operations
- **Compatibility**: Fully backward compatible - new properties are optional
- **Version**: 3.0.3.10 (all packages)

#### 4. Interactive Controls Pointer-Capture Guard - v3.0.3.10
- Restores clicks/inputs inside node controls by skipping pointer-capture for interactive elements in `wwwroot/script.js`.

### рџ›  Technical Fixes

#### 3. CSS-hiding Virtualization Mode
- **Problem**: Standard virtualization caused catastrophic delays during pan/zoom (up to 1700ms) due to heavy component recreation
- **Solution**: New mode where invisible nodes are hidden via `display:none` instead of DOM removal
- **Benefits**:
  - Eliminates mount/unmount overhead during pan/zoom
  - Components remain mounted in memory
  - SignalR updates work normally without additional caching
  - Drag latency reduced from ~1700ms to ~150ms
- **Changed Files**:
  - `DiagramVirtualizationOptions.cs`: new `CssHiding` property
  - `NodeRenderer.cs`: CSS-hiding logic for HTML and SVG nodes
  - Support in consuming application via `SchemeVirtualizationOptions`

#### 4. Drag Release Fix (Pointer Capture)
- **Problem**: Diagram remained in drag mode when mouse button was released outside `DiagramCanvas`
- **Solution**: Implementation of pointer capture and document-level fallback handlers
- **Technical Details**:
  - `script.js`: Pointer capture on `.diagram-canvas`, document-level handlers
  - `DiagramCanvas.razor.cs`: `OnPointerUpOutside` method for drag completion
  - Support for both WASM and Server-side Blazor
- **Result**: Proper drag completion regardless of mouse release location

#### 5. Dependencies Update
- **Description**: Updated ASP.NET Core package versions for improved security and stability
- **Changes**:
  - .NET 6.0: ASP.NET Core packages updated to 6.0.36
  - .NET 7.0: ASP.NET Core packages updated to 7.0.20
  - .NET 8.0: ASP.NET Core packages updated to 8.0.24
  - .NET 9.0: ASP.NET Core packages updated to 9.0.13
- **Benefits**: Latest security patches and performance improvements
- **Compatibility**: No breaking changes - all updates are patch/minor versions

#### 6. Circuit Disconnection Handling
- **Problem**: Exceptions thrown when trying to cleanup ResizeObserver after Blazor circuit disconnection
- **Solution**: Added exception handling in `JSRuntimeExtensions.UnobserveResizes()`
- **Technical Details**:
  - Catches `JSDisconnectedException` when circuit is already disconnected
  - Catches `ObjectDisposedException` when DotNetObjectReference is disposed
  - Graceful degradation - cleanup is skipped when impossible
- **Benefits**: Prevents application crashes during navigation or tab closure
- **Compatibility**: Backward compatible - no API changes

#### 7. Interactive Controls Pointer-Capture Guard
- **Problem**: Global pointer-capture on `diagram-canvas` could intercept interactions inside node UI (buttons, switches, inputs), resulting in visual click feedback without actual `OnClick`/input handling.
- **Solution**: Added interactive-target guard in `wwwroot/script.js` before calling `setPointerCapture`.
- **Technical Details**:
  - Added helper `isInteractiveTarget(target)` with selector-based checks (`button`, `input`, `textarea`, `select`, `a`, `[contenteditable]`, MudBlazor control classes, etc.)
  - In pointerdown capture handler: skip `setPointerCapture` for interactive descendants
  - Kept pointer-capture behavior unchanged for non-interactive canvas area (drag/release logic still works)
- **Benefits**:
  - Restores correct click/input behavior in embedded node controls
  - Removes need for per-control `pointerdown:stopPropagation` workarounds in app code
  - Preserves drag robustness fixed by pointer-capture release handling
- **Changed Files**:
  - `src/Blazor.Diagrams/wwwroot/script.js`

### рџ“Љ Performance Comparison

| Virtualization Mode | Drag Latency | Scripting Overhead | DOM Memory |
|---------------------|-------------|-------------------|------------|
| Disabled | ~150ms | low | all nodes |
| Standard | ~1700ms | ~725ms | viewport only |
| CSS-hiding | ~150ms | low | all nodes |

### рџ”§ Configuration

To use new features in your application:

```json
{
  "Virtualization": {
    "VirtualizationEnabled": true,
    "CssHidingEnabled": true,
    "VirtualizationPaddingPx": 200,
    "ProgressiveOnViewport": false
  }
}
```

### рџ“ќ Change Documentation

Detailed technical documentation is available in `src/Documents/`:
- `CSS_HIDING_VIRTUALIZATION.md` - Complete CSS-hiding mode description
- `POINTER_CAPTURE_DRAG_RELEASE_FIX.md` - Technical details of drag release fix

---

# Blazor.Diagrams

![](ZBD.png)

Z.Blazor.Diagrams is a fully customizable and extensible all-purpose diagrams library for Blazor (both Server Side and WASM). It was first inspired by the popular React library [react-diagrams](https://github.com/projectstorm/react-diagrams), but then evolved into something much bigger. ZBD can be used to make advanced diagrams with a custom design. Even the behavior of the library is "hackable" and can be changed to suit your needs. 

| NuGet Package                | Version                                                                                                                                  | Download                                                                                                                                  |
| ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Z.Blazor.Diagrams.Core       | [![NuGet](https://img.shields.io/nuget/v/Z.Blazor.Diagrams.Core.svg)](https://www.nuget.org/packages/Z.Blazor.Diagrams.Core)             | [![Nuget](https://img.shields.io/nuget/dt/Z.Blazor.Diagrams.Core.svg)](https://www.nuget.org/packages/Z.Blazor.Diagrams.Core)             |
| Z.Blazor.Diagrams            | [![NuGet](https://img.shields.io/nuget/v/Z.Blazor.Diagrams.svg)](https://www.nuget.org/packages/Z.Blazor.Diagrams)                       | [![Nuget](https://img.shields.io/nuget/dt/Z.Blazor.Diagrams.svg)](https://www.nuget.org/packages/Z.Blazor.Diagrams)                       |
| Z.Blazor.Diagrams.Algorithms | [![NuGet](https://img.shields.io/nuget/v/Z.Blazor.Diagrams.Algorithms.svg)](https://www.nuget.org/packages/Z.Blazor.Diagrams.Algorithms) | [![Nuget](https://img.shields.io/nuget/dt/Z.Blazor.Diagrams.Algorithms.svg)](https://www.nuget.org/packages/Z.Blazor.Diagrams.Algorithms) |

| Badges     |                                                                                                                                                    |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| JavaScript | ![GitHub file size in bytes](https://img.shields.io/github/size/Blazor-Diagrams/Blazor.Diagrams/src/Blazor.Diagrams/wwwroot/script.min.js)         |
| CSS        | ![GitHub file size in bytes](https://img.shields.io/github/size/Blazor-Diagrams/Blazor.Diagrams/src/Blazor.Diagrams/wwwroot/style.css)             |
| Activity   | [![GitHub](https://img.shields.io/github/last-commit/Blazor-Diagrams/Blazor.Diagrams/develop)](https://github.com/Blazor-Diagrams/Blazor.Diagrams) |
| License    | [![GitHub](https://img.shields.io/github/license/Blazor-Diagrams/Blazor.Diagrams.svg)](https://github.com/Blazor-Diagrams/Blazor.Diagrams)         |

## Mindset/Goals

- **Be multi purpose and useful for most diagramming use cases**. ZBD started as a diagramming library for specific use cases, but it is now expanding to be more generic and more useful.
- **Performance** is very important, especially in WebAssembly.
- **Separate the data layer (models) and the UI layer (widgets)**. Representing diagrams as a model has a lot of benefits, and the separation makes things easier, such as saving snapshots or mutating models, regardless of how/where it's gonna be rendered.
- **Be fully customizable, either in how things look or how things behave**. All of the UI can be customized by either providing Blazor components or using CSS. All of the default behaviors are customizable by replacing them with your own custom behaviors.
- **Avoid JavaScript**. 95% of ZBD is made using C#/Blazor, JS is only used when absolutely necessary (e.g. bounds and observers). JS interop calls are costly, in the future, we strive to have most of them batched and/or replaced.

## Features

- Multi purpose
- Touch support
- SVG layer for links/nodes and HTML layer for nodes for maximum customizability
- Links between nodes, ports and even other links
- Link routers, path generators, markers and labels
- Panning, Zooming and Zooming to fit a set of nodes
- Multi selection, deletion and region selection
- Groups as first class citizen, with all the features of nodes
- Custom nodes, links and groups
- Replaceable ("Hackable") behaviors (e.g. link dragging, model deletion, etc..)
- Customizable Diagram overview/navigator for large diagrams
- Snap to Grid
- Virtualization, only draw nodes that are visible to the users
- Locking mechanism (read-only)
- Algorithms

## Getting Started

You can get started very easily & quickly using:

- [Documentation](https://blazor-diagrams.zhaytam.com/)
- [Installation](https://blazor-diagrams.zhaytam.com/documentation/installation)

### Sample project

Repository: https://github.com/Blazor-Diagrams/Blazor.DatabaseDesigner

![](DBDesigner.png)

### Contributing

All kinds of contributions are welcome!  
If you're interested in helping, please create an issue or comment on an existing one to explain what you will be doing. This is because multiple people can be working on the same problem.

## Feedback

If you find a bug or you want to see a functionality in this library, feel free to open an issue.
