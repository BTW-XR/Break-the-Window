export type JsonValue =
  | string
  | number
  | boolean
  | null
  | JsonValue[]
  | { [key: string]: JsonValue };

export interface SessionMeta {
  schema_version: string;
  session_id: string;
  session_name: string;
  session_folder: string;
  participant_id: string;
  study_id: string;
  condition_id: string;
  task_id: string;
  started_at_utc_iso8601: string;
  sample_interval_seconds: number;
  coordinate_space: {
    frame: string;
    handedness: string;
    position_units: string;
    scale_units: string;
    positive_x: string;
    positive_y: string;
    positive_z: string;
  };
  device_info: {
    device_model: string;
    device_name: string;
    device_type: string;
    operating_system: string;
    graphics_device_name: string;
  };
}

export interface Vector3Record {
  x: number;
  y: number;
  z: number;
}

export interface QuaternionRecord {
  x: number;
  y: number;
  z: number;
  w: number;
}

export interface PoseRecord {
  position: Vector3Record;
  rotation: QuaternionRecord;
}

export interface HandSampleRecord extends PoseRecord {
  tracked: boolean;
}

export interface ModuleSampleRecord {
  object_id: string;
  realPosition: Vector3Record;
  realScale: Vector3Record;
  realRotation: QuaternionRecord;
}

export interface WebsiteSampleRecord {
  object_id: string;
  module_object_id: string;
  position: Vector3Record;
  scale: Vector3Record;
  rotation: QuaternionRecord;
  url: string;
}

export interface SampleRecord {
  session_id: string;
  trial_id: string;
  elapsed_seconds: number;
  wall_clock_utc_iso8601: string;
  head: PoseRecord;
  left_hand: HandSampleRecord;
  right_hand: HandSampleRecord;
  modules: ModuleSampleRecord[];
  websites: WebsiteSampleRecord[];
}

export interface EventRecord {
  session_id: string;
  trial_id: string;
  timestamp: string;
  elapsed_seconds: number;
  event_type: string;
  event_name: string;
  condition_id: string;
  task_id: string;
  notes: string;
  module_object_id: string;
  object_id: string;
  object_type: string;
  related_object_id: string;
  related_object_type: string;
  secondary_object_id: string;
  secondary_object_type: string;
  url: string;
  content_id: string;
  outcome: string;
  details: string;
  has_success: boolean;
  success: boolean;
  has_duration_seconds: boolean;
  duration_seconds: number;
  has_distance_meters: boolean;
  distance_meters: number;
}

export interface ObjectEventRecord {
  session_id: string;
  trial_id: string;
  timestamp: string;
  elapsed_seconds: number;
  event_type: string;
  object_id: string;
  module_object_id: string;
  tag: string;
  name: string;
  hierarchy_path: string;
}

export interface TrialSegment {
  sessionId: string;
  trialId: string;
  startedAtElapsedSeconds: number;
  endedAtElapsedSeconds: number | null;
  startedAtUtcMs: number;
  endedAtUtcMs: number | null;
}

export type OccurrenceKind = "semantic" | "lifecycle";

export interface Occurrence {
  id: string;
  sessionId: string;
  absoluteUtcMs: number;
  absoluteUtcIso: string;
  elapsedSeconds: number;
  kind: OccurrenceKind;
  typeKey: string;
  label: string;
  objectId?: string;
  moduleObjectId?: string;
  sessionFolder: string;
  raw: EventRecord | ObjectEventRecord;
}

export interface OccurrenceTypeSummary {
  typeKey: string;
  kind: OccurrenceKind;
  label: string;
  count: number;
}

export interface ObjectHistorySummary {
  objectId: string;
  lifecycle: ObjectEventRecord[];
  semanticEvents: EventRecord[];
}

export interface LoadedSession {
  meta: SessionMeta;
  samples: SampleRecord[];
  events: EventRecord[];
  objects: ObjectEventRecord[];
  trials: TrialSegment[];
  occurrences: Occurrence[];
  objectHistory: Record<string, ObjectHistorySummary>;
  timeRangeUtcMs: { start: number; end: number };
  elapsedRangeSeconds: { start: number; end: number };
}

export interface LoadedStudy {
  sessions: LoadedSession[];
  occurrenceTypes: OccurrenceTypeSummary[];
  occurrenceIndex: Occurrence[];
  timeRangeUtcMs: { start: number; end: number };
}

export interface ReplayFrame {
  session: LoadedSession;
  sample: SampleRecord | null;
  sampleIndex: number;
  currentUtcMs: number | null;
  activeEvents: EventRecord[];
  activeObjectEvents: ObjectEventRecord[];
}

export interface LayerState {
  showHead: boolean;
  showLeftHand: boolean;
  showRightHand: boolean;
  showModules: boolean;
  showWebsites: boolean;
  showTrails: boolean;
  showModuleLabels: boolean;
  showWebsiteLabels: boolean;
  showAxes: boolean;
}
