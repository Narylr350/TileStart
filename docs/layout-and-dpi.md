# Layout and DPI rules

TileStart uses WPF for content layout and Win32 for screen and HWND placement. Repeated fixes around centering,
clipping, background bounds, and resolution changes came from mixing those coordinate spaces or copying a native
metric into a different container hierarchy.

## Why the same class of bug kept returning

The Git history shows several local fixes that corrected a visible symptom without first naming the layout
invariant:

| Commit | Visible symptom | Underlying cause |
| --- | --- | --- |
| `74451a1` | About footer border was clipped | A fixed row was enlarged from 58 to 60 instead of making the middle region yield space. |
| `7a55d8d` | The all-apps list was clipped at the bottom | A native 54-DIP padding value was copied into a flatter TileStart container hierarchy. |
| `1da8c58` | Acrylic/background bounds were stale | Composition material was applied before the HWND reached its final rectangle. |
| `3feaa43` | A resolution change permanently changed the saved size | A temporary work-area clamp was persisted as the user's preference. |
| `998325b` | Tiles overlapped the scrollbar | Logical workspace width and the scrollbar's visual clearance were treated as one measurement. |
| `233141f` | Empty/content-filled workspaces used the wrong width | Window width was treated as a fixed value instead of an effective result of preference, content, and work area. |

The common root cause was not “the number was slightly wrong.” It was that ownership was unclear: which container
owns a spacing metric, which coordinate system owns a rectangle, and whether a value is a user preference or a
temporary effective result.

## Coordinate spaces

- WPF layout, tile metrics, padding, and user-preferred sizes use device-independent pixels (DIP).
- Monitor rectangles, work areas, taskbars, and `SetWindowPos` use physical pixels.
- Conversion between DIP and physical pixels belongs at the top-level window boundary. Child controls must not
  query monitor DPI to position ordinary content.
- A native StartUI metric is only valid in the container layer where it was observed. For example, the native
  `AllAppsListPadding.Bottom` belongs to a deeper nested scrolling surface and must not be copied onto TileStart's
  flat `ListBox` viewport.

## Fixed and flexible dimensions

Fixed dimensions are appropriate for atomic visual specifications such as tile cells, icons, title bars, control
heights, separators, and verified design spacing.

Top-level windows, main content regions, dynamic forms, lists, and preview panes must remain flexible:

- use `Auto` for content-sized headers and footers;
- use `*` for the main content row or column;
- keep important actions outside the scrolling region;
- use a `ScrollViewer` when the content cannot reflow below its usable minimum;
- treat a window's declared `Width` and `Height` as desired dimensions, not a guarantee that the monitor can fit them.

All windows using `TileStartDialogWindowStyle` are fitted to the owner monitor's work area by
`DialogWindowManager`. The manager lowers impossible minimums, preserves the desired size for larger monitors,
centers over the owner, and clamps the final physical rectangle to the work area.

## Main window preferences

The persisted start-window size is the user's preferred workspace column count and preferred DIP height. The
effective window size may be smaller on a low-resolution display or during a temporary game display mode, but that
temporary clamp must never overwrite the preference. Only an explicit resize gesture updates it.

## Display-change ordering

1. Stop resize animations and coordinate-dependent drag interactions.
2. On `WM_DPICHANGED`, apply the system-suggested physical rectangle immediately and let WPF process the message.
3. Re-query the event's target monitor, work area, DPI, and taskbar edge.
4. Apply the final physical HWND rectangle.
5. Update WPF layout.
6. Reapply Acrylic or other size-dependent composition material.
7. Resume visual animation and interaction.

`WM_DISPLAYCHANGE` and `WM_SETTINGCHANGE / SPI_SETWORKAREA` may be debounced while display topology stabilizes.
`WM_DPICHANGED` must not be reduced to the same delayed path because its suggested rectangle and target monitor are
part of the message contract.

## Review checklist

- Does a new dialog use `TileStartDialogWindowStyle`?
- Can its middle row shrink while the title and primary actions remain visible?
- Are large columns proportional or scrollable rather than protected by an oversized window `MinWidth`?
- Is a native metric documented with its owning container hierarchy?
- Does a display constraint change only the effective size, not the stored preference?
- Are final visual positions produced by layout, with render transforms returning to zero after animation?
- Do tests assert the layout invariant rather than only the current magic number?

## Regression controls

Documentation and comments reduce rework only when each has a specific role:

- this document records cross-file invariants and the reason behind them;
- comments sit at dangerous coordinate-system or ordering boundaries and explain why an apparently simpler change
  is incorrect;
- pure layout helpers make fitting and clamping testable without a desktop session;
- guard tests lock down important ordering and ownership rules, rather than only checking one current pixel value;
- real desktop checks still cover composition, mixed-DPI movement, taskbar placement, and visual clipping that unit
  tests cannot observe.

Useful external baselines:

- [Microsoft WPF layout documentation](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/layout): WPF content
  uses device-independent units and dynamic measure/arrange layout.
- [Microsoft `WM_DPICHANGED` contract](https://learn.microsoft.com/windows/win32/hidpi/wm-dpichanged): the message
  supplies a suggested physical rectangle for the new DPI.
- [Microsoft `MONITORINFO` contract](https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-monitorinfo):
  monitor and work-area rectangles use physical virtual-screen coordinates.
- [Microsoft PowerToys Shortcut Guide](https://github.com/microsoft/PowerToys/blob/main/src/modules/ShortcutGuide/ShortcutGuide.Ui/ShortcutGuideXAML/OverlayWindow.xaml.cs)
  follows the same boundary in current window code: XAML owns child layout, while top-level monitor placement uses
  physical rectangles and native move/resize APIs.
