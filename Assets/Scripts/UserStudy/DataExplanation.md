# DataCollector Output Explanation

This document explains the study-ready session log output produced by [DataCollector.cs](/C:/Users/JerryWei/ProgrammingProjects/Break-the-Window/Assets/Scripts/UserStudy/DataCollector.cs).

## Output Files

Each recording session writes a folder under:

`UserStudy/<timestamp>_<sessionName>/`

The folder contains:

- `session_meta.json`
- `samples.ndjson`
- `objects.ndjson`
- `events.ndjson`

Examples:

- In the Unity Editor on Windows:
  `%USERPROFILE%\AppData\LocalLow\BreakTheWindow\Break-the-Window\UserStudy\<timestamp>_<sessionName>\`
- On a Meta Quest Android build:
  `/storage/emulated/0/Android/data/<package-name>/files/UserStudy/<timestamp>_<sessionName>/`

## session_meta.json

This file stores session-level metadata for the HCI study.

Current fields include:

- `schema_version`
- `session_id`
- `session_name`
- `session_folder`
- `participant_id`
- `study_id`
- `condition_id`
- `task_id`
- `started_at_utc_iso8601`
- `sample_interval_seconds`
- `coordinate_space`
- `device_info`

`session_id` is globally unique for each recording session.

## samples.ndjson

This file is newline-delimited JSON. Each line is one time-sampled snapshot.

Current top-level fields:

- `session_id`
- `trial_id`
- `elapsed_seconds`
- `wall_clock_utc_iso8601`
- `head`
- `left_hand`
- `right_hand`
- `modules`
- `websites`

`trial_id` is present when a trial is active and omitted or null otherwise.

### Head And Hand Data

`head` contains:

- `position`
- `rotation`

`left_hand` and `right_hand` contain:

- `tracked`
- `position`
- `rotation`

If a hand is not tracked, `tracked` is `false`. Consumers should ignore the pose fields when tracking is false.

### Module Data

`modules` is an array. Each entry contains:

- `object_id`
- `realPosition`
- `realScale`
- `realRotation`

These values come from `ModuleController.realPosition`, `realScale`, and `realRotation`.

### Website Data

`websites` is an array. Each entry contains:

- `object_id`
- `module_object_id`
- `position`
- `scale`
- `rotation`
- `url`

The URL is stored in full and comes from `CanvasManager.TryGetURL()`, which prefers the live webview URL and falls back to the serialized configured URL.
`module_object_id` stores the tracked module `object_id` for the module the website is currently in. If that link cannot be resolved when logging, the collector writes `module_unknown`.
`scale.x` and `scale.y` store the website's rendered width and height in world units from `CanvasManager.GetWidthHeight()`. `scale.z` is a fixed placeholder depth of `1` because the website is treated as a plane rather than a volumetric object.

## objects.ndjson

This file is newline-delimited JSON. Each line is one lifecycle event for a tracked `Module` or `Website`.

Current fields:

- `session_id`
- `trial_id`
- `timestamp`
- `elapsed_seconds`
- `event_type`
- `object_id`
- `module_object_id`
- `tag`
- `name`
- `hierarchy_path`

Current `event_type` values:

- `created`
- `updated_identity`
- `destroyed`

For `Website` rows, `updated_identity` also covers a change in `module_object_id`, meaning the website is now in a different module than before.

`object_id` values such as `module_1` and `website_2` are unique within a session. Use `(session_id, object_id)` as the stable join key.

## events.ndjson

This file is newline-delimited JSON. Each line is one semantic study event.

Current shared fields:

- `session_id`
- `trial_id`
- `timestamp`
- `elapsed_seconds`
- `event_type`
- `event_name`
- `condition_id`
- `task_id`
- `notes`

Optional event payload fields may also appear, such as:

- `module_object_id`
- `object_id`
- `object_type`
- `related_object_id`
- `related_object_type`
- `secondary_object_id`
- `secondary_object_type`
- `url`
- `content_id`
- `outcome`
- `details`
- `has_success`
- `success`
- `has_duration_seconds`
- `duration_seconds`
- `has_distance_meters`
- `distance_meters`

### Session And Trial Events

The collector supports:

- `session_started`
- `session_ended`
- `trial_started`
- `trial_ended`
- `task_started`
- `task_ended`

### Interaction Events

The current interaction taxonomy is limited to:

- `move_started`
- `move_ended`
- `resize_started`
- `resize_ended`
- `merge_confirmed`
- `split_created`
- `website_opened`
- `website_closed`

Website interaction events include `module_object_id` so each website event can be joined directly back to the module it was following when logged. If the link cannot be resolved, the collector writes `module_unknown`.

The current implementation wires:

- `move_started` / `move_ended`
- `resize_started` / `resize_ended`
- `merge_confirmed`
- `split_created`
- `website_opened`
- `website_closed`

The following event names are intentionally not part of the interaction taxonomy:

- `grab_started`
- `grab_ended`
- `error_committed`
- `url_changed`
- `selection_committed`

### System And Annotation Events

The public API also supports:

- `system` events through `LogSystemEvent(...)`
- `annotation` events through `LogAnnotation(...)`

These can be used for study notes, tracking interruptions, recentering, pauses, webview load status, or external task control.

## Session And Trial Control API

The recorder exposes explicit control methods:

- `StartSession(...)`
- `EndSession(...)`
- `StartTrial(...)`
- `EndTrial(...)`
- `LogInteraction(...)`
- `LogSystemEvent(...)`
- `LogAnnotation(...)`

Keyboard shortcuts remain available as a debug fallback:

- `F12` starts a session
- `Right Shift + F11` ends the session

## Coordinate Space

The recorded positions, scales, and rotations are in Unity world space.

- Position units are meters.
- Scale values are world-space scale values from Unity transforms.
- Unity uses a left-handed coordinate system.
- `+X` points right.
- `+Y` points up.
- `+Z` points forward.

The values are not stored relative to the headset rig root or controller parent.

## Sampling Behavior

- The script records data at 5 samples per second.
- One sample is written every 0.2 seconds.
- The collector rescans tagged objects before each sample.

This means:

- newly spawned `Module` and `Website` objects can appear mid-session
- deleted `Module` and `Website` objects stop appearing in later samples
- lifecycle changes are captured in `objects.ndjson`

## Recommended Analysis Flow

1. Use `session_meta.json` for participant, condition, task, scene, and device context.
2. Use `events.ndjson` to reconstruct session and trial boundaries.
3. Use `objects.ndjson` to track object creation, renaming, and removal.
4. Use `samples.ndjson` for continuous pose, module, and website state during each trial.

This format is intended to be the canonical raw log for downstream HCI analysis in Python, R, or custom scripts.
