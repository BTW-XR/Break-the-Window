# Break-the-Window Study Replay Web App

Local browser app for loading and replaying `UserStudy` session logs written by `DataCollector`.

The app runs entirely client-side. It does not upload files or require a backend.

## Files It Reads

Each session is expected to contain:

- `session_meta.json`
- `samples.ndjson`
- `objects.ndjson`
- `events.ndjson`

You can load:

- a `UserStudy/` root folder containing multiple session folders, or
- one or more complete single-session file sets

## Current UI

The current app is replay-focused. The active surface includes:

- a left control rail for loading, session selection, occurrence filters, layer toggles, and trail length
- a central 3D replay viewport
- replay transport controls and time scrubber
- a compact legend

The older study overview, inspector, and time-series lane views have been removed from the current UI.

## Features

- multi-session loading
- session-relative replay with play/pause, frame stepping, and scrubber dragging
- previous / next occurrence jumping based on selected occurrence filters
- 3D replay of:
  - head pose
  - left hand
  - right hand
  - modules
  - websites
- click selection of replayed objects in the 3D viewport
- orbit-style camera controls in the replay viewport
- deterministic per-session object colors:
  - modules use warm colors
  - websites use cool colors
  - the same `(session_id, object_id)` gets the same color every time the same session is loaded
- separate label toggles for module labels and website labels
- adjustable trail length, including an infinite mode

## Run

```bash
npm install
npm run dev
```

To build a production bundle:

```bash
npm run build
```

To preview the built app locally:

```bash
npm run preview
```

## How To Use

1. Start the app with `npm run dev`.
2. Open the local Vite URL in your browser.
3. Load data using the file loader:
   - choose a `UserStudy/` root folder, or
   - provide the session log files directly
4. Pick the active session from the `Session` panel.
5. Use the replay controls:
   - `Play` / `Pause`
   - `Prev Frame` / `Next Frame`
   - `Prev Occurrence` / `Next Occurrence`
   - speed selector
   - elapsed-time scrubber
6. Use the 3D viewport:
   - drag to orbit
   - wheel to zoom
   - click an object to select it
7. Use the left rail to control visible layers and labels.

## Replay Semantics

- Replay time is session-relative and uses `elapsed_seconds`.
- The UTC readout is derived from the sampled timestamps in the active session.
- Occurrence jumping uses the currently enabled occurrence filters from:
  - `events.ndjson`
  - `objects.ndjson`

If no occurrence filters are enabled, the occurrence jump buttons are disabled.

## Spatial Mapping

The logs are recorded in Unity world space.

The replay converts Unity's logged transform data into the browser 3D scene so that:

- handedness is corrected for visualization
- modules render as planes
- websites render as planes
- head orientation is shown with a forward arrow

## Layers And Labels

Available layer toggles include:

- head
- left hand
- right hand
- modules
- websites
- trails
- module labels
- website labels
- lifecycle markers
- trial bands

Note: lifecycle markers and trial bands are retained in shared UI state for compatibility with the existing loader/model pipeline, even though the removed time-series/overview surfaces are no longer displayed.

## Trail Length

Trail rendering is configurable with the `Trail length` slider.

- left side: shorter recent-history trails
- right side: `Infinite`

`Infinite` means the replay draws the full history up to the current replay time instead of truncating to a fixed recent window.

## Logged Data Assumptions

- `module_unknown` is shown exactly as recorded.
- Website sample width and height come from `CanvasManager.GetWidthHeight()` in the logging pipeline.
- Website `module_object_id` identifies the module the website is currently in when the record was written.

## Limitations

- The app is intended for local desktop browsers.
- The current UI is optimized for replay and manual inspection, not presentation export.
- The removed overview / inspector / time-series panels are not currently available.
- If a session contains sparse website samples, replayed website geometry is limited to what was actually logged.

## Implementation Notes

- Stack: `Vite + React + TypeScript + Three.js`
- Parsing is done client-side
- No server storage
- No authentication layer

