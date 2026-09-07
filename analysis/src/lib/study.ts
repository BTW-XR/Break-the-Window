import type {
  EventRecord,
  LoadedSession,
  LoadedStudy,
  ObjectEventRecord,
  ObjectHistorySummary,
  Occurrence,
  OccurrenceTypeSummary,
  ReplayFrame,
  SampleRecord,
  SessionMeta,
  TrialSegment,
} from "../types";

const REQUIRED_SESSION_FILES = [
  "session_meta.json",
  "events.ndjson",
  "objects.ndjson",
  "samples.ndjson",
] as const;

type DirectoryHandle = FileSystemDirectoryHandle;

interface SessionFileBundle {
  key: string;
  label: string;
  sessionMeta: File;
  events: File;
  objects: File;
  samples: File;
}

function stripBom(text: string): string {
  return text.charCodeAt(0) === 0xfeff ? text.slice(1) : text;
}

async function readText(file: File): Promise<string> {
  return stripBom(await file.text());
}

function parseJson<T>(text: string): T {
  return JSON.parse(stripBom(text)) as T;
}

function parseNdjson<T>(text: string): T[] {
  return stripBom(text)
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .map((line) => JSON.parse(line) as T);
}

function compareByElapsed<T extends { elapsed_seconds: number }>(
  a: T,
  b: T,
): number {
  return a.elapsed_seconds - b.elapsed_seconds;
}

function compareByTimestamp<T extends { timestamp: string }>(
  a: T,
  b: T,
): number {
  return Date.parse(a.timestamp) - Date.parse(b.timestamp);
}

function buildTrials(
  sessionId: string,
  events: EventRecord[],
): TrialSegment[] {
  const trials: TrialSegment[] = [];
  const openTrials = new Map<string, TrialSegment>();

  for (const event of events) {
    if (event.event_name === "trial_started") {
      const trialId =
        event.trial_id || `trial@${event.elapsed_seconds}`;
      const segment: TrialSegment = {
        sessionId,
        trialId,
        startedAtElapsedSeconds: event.elapsed_seconds,
        endedAtElapsedSeconds: null,
        startedAtUtcMs: Date.parse(event.timestamp),
        endedAtUtcMs: null,
      };
      openTrials.set(trialId, segment);
      trials.push(segment);
    } else if (event.event_name === "trial_ended") {
      const trialId =
        event.trial_id || `trial@${event.elapsed_seconds}`;
      const segment = openTrials.get(trialId);
      if (segment) {
        segment.endedAtElapsedSeconds = event.elapsed_seconds;
        segment.endedAtUtcMs = Date.parse(event.timestamp);
        openTrials.delete(trialId);
      }
    }
  }

  return trials;
}

function buildOccurrences(
  meta: SessionMeta,
  events: EventRecord[],
  objects: ObjectEventRecord[],
): Occurrence[] {
  const semanticOccurrences = events.map<Occurrence>(
    (event, index) => ({
      id: `${meta.session_id}:semantic:${index}`,
      sessionId: meta.session_id,
      absoluteUtcMs: Date.parse(event.timestamp),
      absoluteUtcIso: event.timestamp,
      elapsedSeconds: event.elapsed_seconds,
      kind: "semantic",
      typeKey: event.event_name,
      label: event.event_name,
      objectId: event.object_id || undefined,
      moduleObjectId: event.module_object_id || undefined,
      sessionFolder: meta.session_folder,
      raw: event,
    }),
  );

  const lifecycleOccurrences = objects.map<Occurrence>(
    (objectEvent, index) => ({
      id: `${meta.session_id}:lifecycle:${index}`,
      sessionId: meta.session_id,
      absoluteUtcMs: Date.parse(objectEvent.timestamp),
      absoluteUtcIso: objectEvent.timestamp,
      elapsedSeconds: objectEvent.elapsed_seconds,
      kind: "lifecycle",
      typeKey: `${objectEvent.tag}.${objectEvent.event_type}`,
      label: `${objectEvent.tag}.${objectEvent.event_type}`,
      objectId: objectEvent.object_id || undefined,
      moduleObjectId: objectEvent.module_object_id || undefined,
      sessionFolder: meta.session_folder,
      raw: objectEvent,
    }),
  );

  return [...semanticOccurrences, ...lifecycleOccurrences].sort(
    (a, b) => a.absoluteUtcMs - b.absoluteUtcMs,
  );
}

function buildOccurrenceTypeSummaries(
  occurrences: Occurrence[],
): OccurrenceTypeSummary[] {
  const counts = new Map<string, OccurrenceTypeSummary>();

  for (const occurrence of occurrences) {
    const current = counts.get(occurrence.typeKey);
    if (current) {
      current.count += 1;
      continue;
    }

    counts.set(occurrence.typeKey, {
      typeKey: occurrence.typeKey,
      kind: occurrence.kind,
      label: occurrence.label,
      count: 1,
    });
  }

  return [...counts.values()].sort((a, b) => {
    if (a.kind !== b.kind) {
      return a.kind.localeCompare(b.kind);
    }
    return a.label.localeCompare(b.label);
  });
}

function buildObjectHistory(
  events: EventRecord[],
  objects: ObjectEventRecord[],
): Record<string, ObjectHistorySummary> {
  const history = new Map<string, ObjectHistorySummary>();

  for (const objectEvent of objects) {
    if (!objectEvent.object_id) {
      continue;
    }

    if (!history.has(objectEvent.object_id)) {
      history.set(objectEvent.object_id, {
        objectId: objectEvent.object_id,
        lifecycle: [],
        semanticEvents: [],
      });
    }

    history.get(objectEvent.object_id)!.lifecycle.push(objectEvent);
  }

  for (const event of events) {
    for (const objectId of [
      event.object_id,
      event.related_object_id,
      event.secondary_object_id,
      event.module_object_id,
    ]) {
      if (!objectId) {
        continue;
      }

      if (!history.has(objectId)) {
        history.set(objectId, {
          objectId,
          lifecycle: [],
          semanticEvents: [],
        });
      }

      history.get(objectId)!.semanticEvents.push(event);
    }
  }

  return Object.fromEntries(history.entries());
}

async function loadSessionFromBundle(
  bundle: SessionFileBundle,
): Promise<LoadedSession> {
  const [metaText, eventsText, objectsText, samplesText] =
    await Promise.all([
      readText(bundle.sessionMeta),
      readText(bundle.events),
      readText(bundle.objects),
      readText(bundle.samples),
    ]);

  const meta = parseJson<SessionMeta>(metaText);
  const events = parseNdjson<EventRecord>(eventsText).sort(
    compareByTimestamp,
  );
  const objects = parseNdjson<ObjectEventRecord>(objectsText).sort(
    compareByTimestamp,
  );
  const samples =
    parseNdjson<SampleRecord>(samplesText).sort(compareByElapsed);
  const trials = buildTrials(meta.session_id, events);
  const occurrences = buildOccurrences(meta, events, objects);
  const objectHistory = buildObjectHistory(events, objects);
  const timePoints = [
    Date.parse(meta.started_at_utc_iso8601),
    ...events.map((event) => Date.parse(event.timestamp)),
    ...objects.map((objectEvent) =>
      Date.parse(objectEvent.timestamp),
    ),
    ...samples.map((sample) =>
      Date.parse(sample.wall_clock_utc_iso8601),
    ),
  ].filter((value) => !Number.isNaN(value));
  const elapsedPoints = [
    0,
    ...events.map((event) => event.elapsed_seconds),
    ...samples.map((sample) => sample.elapsed_seconds),
  ];

  return {
    meta,
    samples,
    events,
    objects,
    trials,
    occurrences,
    objectHistory,
    timeRangeUtcMs: {
      start: Math.min(...timePoints),
      end: Math.max(...timePoints),
    },
    elapsedRangeSeconds: {
      start: Math.min(...elapsedPoints),
      end: Math.max(...elapsedPoints),
    },
  };
}

async function getFileIfExists(
  directoryHandle: DirectoryHandle,
  filename: (typeof REQUIRED_SESSION_FILES)[number],
): Promise<File | null> {
  try {
    const fileHandle = await directoryHandle.getFileHandle(filename);
    return fileHandle.getFile();
  } catch {
    return null;
  }
}

async function loadBundleFromDirectoryHandle(
  directoryHandle: DirectoryHandle,
): Promise<SessionFileBundle | null> {
  const files = await Promise.all(
    REQUIRED_SESSION_FILES.map((filename) =>
      getFileIfExists(directoryHandle, filename),
    ),
  );

  if (files.some((file) => file === null)) {
    return null;
  }

  const [sessionMeta, events, objects, samples] = files as [
    File,
    File,
    File,
    File,
  ];
  return {
    key: directoryHandle.name,
    label: directoryHandle.name,
    sessionMeta,
    events,
    objects,
    samples,
  };
}

async function collectSessionBundlesFromRoot(
  rootHandle: DirectoryHandle,
): Promise<SessionFileBundle[]> {
  const directSession =
    await loadBundleFromDirectoryHandle(rootHandle);
  if (directSession) {
    return [directSession];
  }

  const bundles: SessionFileBundle[] = [];
  const iterableHandle = rootHandle as unknown as {
    entries: () => AsyncIterableIterator<[string, FileSystemHandle]>;
  };
  for await (const [, handle] of iterableHandle.entries()) {
    if (handle.kind !== "directory") {
      continue;
    }

    const bundle = await loadBundleFromDirectoryHandle(
      handle as DirectoryHandle,
    );
    if (bundle) {
      bundles.push(bundle);
    }
  }

  return bundles;
}

function groupFilesIntoBundles(files: File[]): SessionFileBundle[] {
  const groups = new Map<
    string,
    Partial<Record<(typeof REQUIRED_SESSION_FILES)[number], File>> & {
      label: string;
    }
  >();

  for (const file of files) {
    if (
      !REQUIRED_SESSION_FILES.includes(
        file.name as (typeof REQUIRED_SESSION_FILES)[number],
      )
    ) {
      continue;
    }

    const rawPath =
      "webkitRelativePath" in file && file.webkitRelativePath
        ? file.webkitRelativePath
        : file.name;
    const folderKey = rawPath.includes("/")
      ? rawPath.split("/").slice(0, -1).join("/")
      : "__single__";
    if (!groups.has(folderKey)) {
      groups.set(folderKey, {
        label:
          folderKey === "__single__" ? "single-session" : folderKey,
      });
    }

    groups.get(folderKey)![
      file.name as (typeof REQUIRED_SESSION_FILES)[number]
    ] = file;
  }

  const bundles: SessionFileBundle[] = [];
  for (const [key, group] of groups.entries()) {
    const sessionMeta = group["session_meta.json"];
    const events = group["events.ndjson"];
    const objects = group["objects.ndjson"];
    const samples = group["samples.ndjson"];
    if (!sessionMeta || !events || !objects || !samples) {
      continue;
    }

    bundles.push({
      key,
      label: group.label,
      sessionMeta,
      events,
      objects,
      samples,
    });
  }

  return bundles;
}

export async function loadStudyFromDirectoryHandle(
  rootHandle: DirectoryHandle,
): Promise<LoadedStudy> {
  const bundles = await collectSessionBundlesFromRoot(rootHandle);
  const sessions = await Promise.all(
    bundles.map(loadSessionFromBundle),
  );
  return buildStudy(sessions);
}

export async function loadStudyFromFiles(
  files: File[],
): Promise<LoadedStudy> {
  const bundles = groupFilesIntoBundles(files);
  const sessions = await Promise.all(
    bundles.map(loadSessionFromBundle),
  );
  return buildStudy(sessions);
}

export function buildStudy(sessions: LoadedSession[]): LoadedStudy {
  const orderedSessions = [...sessions].sort(
    (a, b) => a.timeRangeUtcMs.start - b.timeRangeUtcMs.start,
  );
  const occurrenceIndex = orderedSessions.flatMap(
    (session) => session.occurrences,
  );
  const occurrenceTypes =
    buildOccurrenceTypeSummaries(occurrenceIndex);

  return {
    sessions: orderedSessions,
    occurrenceTypes,
    occurrenceIndex,
    timeRangeUtcMs: {
      start: Math.min(
        ...orderedSessions.map(
          (session) => session.timeRangeUtcMs.start,
        ),
      ),
      end: Math.max(
        ...orderedSessions.map(
          (session) => session.timeRangeUtcMs.end,
        ),
      ),
    },
  };
}

export function findSessionById(
  study: LoadedStudy | null,
  sessionId: string | null,
): LoadedSession | null {
  if (!study || !sessionId) {
    return null;
  }

  return (
    study.sessions.find(
      (session) => session.meta.session_id === sessionId,
    ) ?? null
  );
}

export function getNearestSampleIndex(
  samples: SampleRecord[],
  elapsedSeconds: number,
): number {
  if (samples.length === 0) {
    return -1;
  }

  let left = 0;
  let right = samples.length - 1;
  while (left <= right) {
    const mid = Math.floor((left + right) / 2);
    const value = samples[mid].elapsed_seconds;
    if (value < elapsedSeconds) {
      left = mid + 1;
    } else if (value > elapsedSeconds) {
      right = mid - 1;
    } else {
      return mid;
    }
  }

  const next = Math.min(left, samples.length - 1);
  const prev = Math.max(next - 1, 0);
  const nextDistance = Math.abs(
    samples[next].elapsed_seconds - elapsedSeconds,
  );
  const prevDistance = Math.abs(
    samples[prev].elapsed_seconds - elapsedSeconds,
  );
  return nextDistance < prevDistance ? next : prev;
}

export function getReplayFrame(
  session: LoadedSession | null,
  elapsedSeconds: number,
): ReplayFrame | null {
  if (!session) {
    return null;
  }

  const sampleIndex = getNearestSampleIndex(
    session.samples,
    elapsedSeconds,
  );
  const sample =
    sampleIndex >= 0 ? session.samples[sampleIndex] : null;
  const currentUtcMs = sample
    ? Date.parse(sample.wall_clock_utc_iso8601)
    : null;
  const eventWindow = session.meta.sample_interval_seconds * 0.5;
  const activeEvents = session.events.filter(
    (event) =>
      Math.abs(event.elapsed_seconds - elapsedSeconds) <= eventWindow,
  );
  const activeObjectEvents = session.objects.filter(
    (objectEvent) =>
      Math.abs(objectEvent.elapsed_seconds - elapsedSeconds) <=
      eventWindow,
  );

  return {
    session,
    sample,
    sampleIndex,
    currentUtcMs,
    activeEvents,
    activeObjectEvents,
  };
}

export async function showStudyDirectoryPicker(): Promise<LoadedStudy | null> {
  if (!("showDirectoryPicker" in window)) {
    return null;
  }

  const pickerWindow = window as Window & {
    showDirectoryPicker?: () => Promise<DirectoryHandle>;
  };
  const handle = await pickerWindow.showDirectoryPicker?.();
  if (!handle) {
    return null;
  }

  return loadStudyFromDirectoryHandle(handle);
}

export function getDefaultSelectedOccurrenceTypes(
  study: LoadedStudy | null,
): string[] {
  if (!study) {
    return [];
  }

  return study.occurrenceTypes.map((type) => type.typeKey);
}

export function formatElapsed(seconds: number): string {
  const sign = seconds < 0 ? "-" : "";
  const absolute = Math.abs(seconds);
  const hours = Math.floor(absolute / 3600);
  const minutes = Math.floor((absolute % 3600) / 60);
  const secs = absolute % 60;
  return `${sign}${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${secs
    .toFixed(1)
    .padStart(4, "0")}`;
}

export function formatUtc(ms: number | null): string {
  if (ms === null || Number.isNaN(ms)) {
    return "n/a";
  }

  return new Date(ms).toISOString();
}

export function clamp(
  value: number,
  min: number,
  max: number,
): number {
  return Math.min(Math.max(value, min), max);
}

export function getNextOccurrence(
  session: LoadedSession,
  elapsedSeconds: number,
  selectedTypeKeys: string[],
  direction: 1 | -1,
): Occurrence | null {
  const selected = new Set(selectedTypeKeys);
  const pool = session.occurrences.filter((occurrence) =>
    selected.has(occurrence.typeKey),
  );
  if (pool.length === 0) {
    return null;
  }

  if (direction > 0) {
    return (
      pool.find(
        (occurrence) => occurrence.elapsedSeconds > elapsedSeconds,
      ) ?? pool[0]
    );
  }

  for (let index = pool.length - 1; index >= 0; index -= 1) {
    if (pool[index].elapsedSeconds < elapsedSeconds) {
      return pool[index];
    }
  }

  return pool[pool.length - 1];
}
