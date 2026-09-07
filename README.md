# Break-the-Window

[![Visit the Project Website](https://img.shields.io/badge/Visit_the_Project_Website-btw--xr.github.io-2ea44f?style=for-the-badge&logo=githubpages&logoColor=white)](https://btw-xr.github.io/)

A mixed-reality research app (Meta Quest, OpenXR) for browsing the web with modular, resizable, splittable "windows" floating in space. Built in Unity 6 with [Vuplex WebView](https://www.vuplex.com) for in-headset web content, and ships with a session-logging pipeline plus a web-based replay tool for studying how people organize multiple windows in MR.

## What It Does

- **Spawn web panels** anywhere in your space using an in-headset URL bar (`UrlLauncher`) or a content list.
- **Treat each window as a "module"** you can grab, resize, split, and merge through an MR layout tree (`ModuleController` + `LayoutTree`).
- **Pop related content** out of a window via per-module pop buttons.
- **Derive related URLs** from a visiting URL using rule sets (`UrlParsing`), so related pages can be opened in one gesture.
- **Log user study sessions** (`DataCollector`) recording head pose, hands, modules, and websites, and replay them later in the companion `analysis/` web app.

## Repository Layout

```
Assets/
  Scenes/ModularWebsite.unity   main scene
  Scripts/
    PanelsManager.cs            spawning panels and modules
    UrlLauncher.cs              in-headset URL bar and derived-URL launching
    ModuleController/           per-window behaviors (move, resize, merge, split, destroy)
    LayoutTree/                 the window layout as a tree
    UrlParsing/                 rules that derive related URLs
    UserStudy/DataCollector.cs  study session logging
analysis/                       Vite + React + Three.js replay app (see analysis/README.md)
```

`Assets/` also contains third-party/asset content: MRMotifs, XR Interaction Toolkit samples, TextMesh Pro, and Vuplex WebView.

## Installing Vuplex WebView

Vuplex is a **paid Unity asset** and is intentionally **not included in this repository** — `Assets/Vuplex/` is gitignored. You must buy and import it yourself:

1. Buy a license at [vuplex.com](https://www.vuplex.com) (a license unlocks the download; it isn't on the Unity Asset Store).
2. Download the Vuplex WebView Unity package (pick the version that matches your Unity 6 install).
3. In Unity, go to **Assets → Import Package → Custom Package…** and select the downloaded `.unitypackage`, or copy the unpacked folder into `Assets/Vuplex/`.
4. The project's scene and `Packages/manifest.json` do **not** reference Vuplex, so install it before opening the project for the first time (or let Unity import it, then reopen the scene).

Without Vuplex, `Assets/Scripts/WebViewController.cs` and related panel code will not compile — the web panels are the core of the app.

## Requirements

- Unity **6000.3.13f1** (Unity 6)
- Meta Quest headset with developer mode, or Meta XR Simulator for desktop testing
- [Vuplex WebView](https://www.vuplex.com) (paid asset) — imported, not included in the repo
- The packages in `Packages/manifest.json` are fetched automatically (Meta XR SDK, OpenXR, AR Foundation, XR Interaction Toolkit, URP, etc.)

## Getting Started

1. Open the project with Unity `6000.3.13f1` (let it import all packages).
2. Open the scene `Assets/Scenes/ModularWebsite.unity`.
3. Build and run to the headset: **File → Build Settings → Android**, Meta OpenXR loader enabled.
   - If you don't have a device yet, enable **Meta XR Simulator** and run in the editor.
4. In the headset, use the URL bar to enter a site, or pick content from the list. Panels appear in front of you.

## Interacting With Windows

- **Grab** a window to move it.
- **Resize** with the resize handles.
- **Merge / split** to combine or divide content areas.
- Use a **pop button** to tear a linked piece of content out into its own window.
- URLs that match a rule in `UrlParsing` spawn their derived related pages automatically.

## Session Logging & Replay

While a study session runs, `DataCollector` writes session logs to a `UserStudy/` folder on the device:

- `session_meta.json`
- `samples.ndjson` — head/hand/object samples at a fixed interval
- `objects.ndjson` — module and website objects
- `events.ndjson` — lifecycle and interaction events

Pull those files off the device and load them into the replay app to inspect a session in 3D:

```bash
cd analysis
npm install
npm run dev
```

See `analysis/README.md` for full usage.

## Notes

- `TODO.md` tracks current known work items.

## Funding

This project is funded in part by a Google Research grant, Georgia Tech, and the U.S. National Science Foundation (NSF).

This material is based upon work supported in part by the National Science Foundation under Grant No. IIS-2441310.

<p align="center">
  <a href="https://research.google/"><img src="docs/assets/funding/google.png" alt="Google Research" height="54"></a>
  &nbsp;&nbsp;&nbsp;&nbsp;
  <a href="https://www.gatech.edu/"><img src="docs/assets/funding/georgia-tech.png" alt="Georgia Tech" height="54"></a>
  &nbsp;&nbsp;&nbsp;&nbsp;
  <a href="https://www.nsf.gov/"><img src="docs/assets/funding/nsf.png" alt="U.S. National Science Foundation" height="54"></a>
</p>