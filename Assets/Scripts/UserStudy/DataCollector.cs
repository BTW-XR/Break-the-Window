using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DataCollector : MonoBehaviour
{
    private const string SchemaVersion = "2.0.0";
    private const float SampleIntervalSeconds = 0.2f;
    private const string DefaultSessionName = "session";
    private const string RootFolderName = "UserStudy";
    private const string SessionMetaFileName = "session_meta.json";
    private const string SamplesFileName = "samples.ndjson";
    private const string ObjectsFileName = "objects.ndjson";
    private const string EventsFileName = "events.ndjson";
    private const string UnknownModuleObjectId = "module_unknown";

    public static DataCollector Active { get; private set; }

    [SerializeField]
    private RawImage statusIndicator;

    [SerializeField]
    private Transform head;

    [SerializeField]
    private Transform leftHand;

    [SerializeField]
    private Transform rightHand;

    [SerializeField]
    private SkinnedMeshRenderer leftVisual;

    [SerializeField]
    private SkinnedMeshRenderer rightVisual;

    [SerializeField]
    private string dataFolderName;

    [SerializeField]
    private string sessionName;

    [Header("Study Defaults")]
    [SerializeField]
    private string participantId;

    [SerializeField]
    private string studyId;

    [SerializeField]
    private string currentConditionId;

    [SerializeField]
    private string currentTaskId;

    private bool isCollecting;
    private StreamWriter samplesWriter;
    private StreamWriter objectsWriter;
    private StreamWriter eventsWriter;
    private float sampleTimer;
    private float sessionStartTime;
    private DateTime sessionStartUtc;
    private string sessionFolderPath;
    private string sessionId;
    private string activeParticipantId;
    private string activeStudyId;
    private string activeConditionId;
    private string activeTaskId;
    private string activeTrialId;
    private float activeTrialStartElapsedSeconds;
    private int nextModuleId = 1;
    private int nextWebsiteId = 1;
    private int nextTrialIndex = 1;

    private readonly Dictionary<int, TrackedModuleInfo> trackedModules = new Dictionary<int, TrackedModuleInfo>();
    private readonly Dictionary<int, TrackedWebsiteInfo> trackedWebsites = new Dictionary<int, TrackedWebsiteInfo>();

    private void Awake()
    {
        Active = this;
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12) && !isCollecting)
        {
            StartSession();
        }
        else if (Input.GetKeyDown(KeyCode.F11) && Input.GetKey(KeyCode.RightShift) && isCollecting)
        {
            EndSession();
        }

        if (!isCollecting)
        {
            return;
        }

        sampleTimer += Time.deltaTime;
        while (sampleTimer >= SampleIntervalSeconds)
        {
            sampleTimer -= SampleIntervalSeconds;
            RecordSample();
        }
    }

    private void OnDisable()
    {
        EndSession();
        if (Active == this)
        {
            Active = null;
        }
    }

    private void OnDestroy()
    {
        EndSession();
        if (Active == this)
        {
            Active = null;
        }
    }

    private void OnApplicationQuit()
    {
        EndSession();
    }

    public void StartSession(
        string participantIdOverride = null,
        string studyIdOverride = null,
        string conditionIdOverride = null,
        string taskIdOverride = null,
        string sessionNameOverride = null)
    {
        if (isCollecting)
        {
            return;
        }

        if (head == null && Camera.main != null)
        {
            head = Camera.main.transform;
        }

        if (head == null || leftHand == null || rightHand == null || leftVisual == null || rightVisual == null)
        {
            Debug.LogError("DataCollector: One or more required references are not set.");
            isCollecting = false;
            SetIndicatorColor(Color.red);
            return;
        }

        activeParticipantId = Coalesce(participantIdOverride, participantId);
        activeStudyId = Coalesce(studyIdOverride, studyId);
        activeConditionId = Coalesce(conditionIdOverride, currentConditionId);
        activeTaskId = Coalesce(taskIdOverride, currentTaskId);

        string safeSessionName = BuildSafeSessionName(sessionNameOverride);
        dataFolderName =
            DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
            + "_"
            + safeSessionName;

        string rootFolderPath = Path.Combine(Application.persistentDataPath, RootFolderName);
        sessionFolderPath = Path.Combine(rootFolderPath, dataFolderName);
        Directory.CreateDirectory(sessionFolderPath);

        sessionId = Guid.NewGuid().ToString("D");
        sessionStartUtc = DateTime.UtcNow;
        sessionStartTime = Time.time;
        sampleTimer = 0f;
        nextModuleId = 1;
        nextWebsiteId = 1;
        nextTrialIndex = 1;
        activeTrialId = null;
        trackedModules.Clear();
        trackedWebsites.Clear();

        samplesWriter = new StreamWriter(Path.Combine(sessionFolderPath, SamplesFileName), false, Encoding.UTF8);
        objectsWriter = new StreamWriter(Path.Combine(sessionFolderPath, ObjectsFileName), false, Encoding.UTF8);
        eventsWriter = new StreamWriter(Path.Combine(sessionFolderPath, EventsFileName), false, Encoding.UTF8);

        isCollecting = true;

        WriteSessionMetadata(safeSessionName);
        WriteEvent(
            CreateBaseEvent("session", "session_started", 0f, sessionStartUtc));
        RefreshTrackedObjects(0f, sessionStartUtc);

        SetIndicatorColor(Color.green);
        Debug.Log($"DataCollector: Recording session data to {sessionFolderPath}");
    }

    public void EndSession(string notes = null)
    {
        if (!isCollecting)
        {
            DisposeWriters();
            return;
        }

        if (!string.IsNullOrEmpty(activeTrialId))
        {
            EndTrial(false, "session_ended", Coalesce(notes, "Session ended while a trial was active."));
        }

        float elapsedSeconds = GetElapsedSeconds();
        DateTime utcNow = DateTime.UtcNow;
        EventRecord sessionEnded = CreateBaseEvent("session", "session_ended", elapsedSeconds, utcNow);
        sessionEnded.notes = notes;
        WriteEvent(sessionEnded);

        DisposeWriters();

        isCollecting = false;
        sessionFolderPath = null;
        sessionId = null;
        trackedModules.Clear();
        trackedWebsites.Clear();
        SetIndicatorColor(Color.red);
    }

    public void StartTrial(
        string conditionIdOverride = null,
        string taskIdOverride = null)
    {
        if (!isCollecting)
        {
            Debug.LogWarning("DataCollector: Cannot start trial without an active session.");
            return;
        }

        if (!string.IsNullOrEmpty(activeTrialId))
        {
            Debug.LogWarning("DataCollector: A trial is already active.");
            return;
        }

        activeConditionId = Coalesce(conditionIdOverride, activeConditionId, currentConditionId);
        activeTaskId = Coalesce(taskIdOverride, activeTaskId, currentTaskId);
        activeTrialId = $"trial_{nextTrialIndex++}";
        activeTrialStartElapsedSeconds = GetElapsedSeconds();

        DateTime utcNow = DateTime.UtcNow;
        float elapsedSeconds = GetElapsedSeconds();

        EventRecord trialStarted = CreateBaseEvent("trial", "trial_started", elapsedSeconds, utcNow);
        trialStarted.condition_id = activeConditionId;
        trialStarted.task_id = activeTaskId;
        WriteEvent(trialStarted);

        EventRecord taskStarted = CreateBaseEvent("task", "task_started", elapsedSeconds, utcNow);
        taskStarted.condition_id = activeConditionId;
        taskStarted.task_id = activeTaskId;
        WriteEvent(taskStarted);
    }

    public void EndTrial(bool success = true, string outcome = null, string notes = null)
    {
        if (!isCollecting || string.IsNullOrEmpty(activeTrialId))
        {
            return;
        }

        DateTime utcNow = DateTime.UtcNow;
        float elapsedSeconds = GetElapsedSeconds();
        float completionTimeSeconds = Mathf.Max(0f, elapsedSeconds - activeTrialStartElapsedSeconds);

        EventRecord taskEnded = CreateBaseEvent("task", "task_ended", elapsedSeconds, utcNow);
        taskEnded.condition_id = activeConditionId;
        taskEnded.task_id = activeTaskId;
        taskEnded.outcome = outcome;
        taskEnded.notes = notes;
        taskEnded.has_success = true;
        taskEnded.success = success;
        taskEnded.has_duration_seconds = true;
        taskEnded.duration_seconds = completionTimeSeconds;
        WriteEvent(taskEnded);

        EventRecord trialEnded = CreateBaseEvent("trial", "trial_ended", elapsedSeconds, utcNow);
        trialEnded.condition_id = activeConditionId;
        trialEnded.task_id = activeTaskId;
        trialEnded.outcome = outcome;
        trialEnded.notes = notes;
        trialEnded.has_success = true;
        trialEnded.success = success;
        trialEnded.has_duration_seconds = true;
        trialEnded.duration_seconds = completionTimeSeconds;
        WriteEvent(trialEnded);

        activeTrialId = null;
    }

    public void LogInteraction(
        string eventName,
        string objectId = null,
        string objectType = null,
        string moduleObjectId = null,
        string relatedObjectId = null,
        string relatedObjectType = null,
        string secondaryObjectId = null,
        string secondaryObjectType = null,
        string url = null,
        string contentId = null,
        bool hasSuccess = false,
        bool success = false,
        bool hasDurationSeconds = false,
        float durationSeconds = 0f,
        bool hasDistanceMeters = false,
        float distanceMeters = 0f,
        string notes = null)
    {
        if (!isCollecting)
        {
            return;
        }

        EventRecord record = CreateBaseEvent("interaction", eventName, GetElapsedSeconds(), DateTime.UtcNow);
        record.object_id = objectId;
        record.object_type = objectType;
        record.module_object_id = moduleObjectId;
        record.related_object_id = relatedObjectId;
        record.related_object_type = relatedObjectType;
        record.secondary_object_id = secondaryObjectId;
        record.secondary_object_type = secondaryObjectType;
        record.url = url;
        record.content_id = contentId;
        record.notes = notes;
        record.has_success = hasSuccess;
        record.success = success;
        record.has_duration_seconds = hasDurationSeconds;
        record.duration_seconds = durationSeconds;
        record.has_distance_meters = hasDistanceMeters;
        record.distance_meters = distanceMeters;
        WriteEvent(record);
    }

    public void LogSystemEvent(
        string eventName,
        string details = null,
        bool hasSuccess = false,
        bool success = false,
        string notes = null)
    {
        if (!isCollecting)
        {
            return;
        }

        EventRecord record = CreateBaseEvent("system", eventName, GetElapsedSeconds(), DateTime.UtcNow);
        record.details = details;
        record.notes = notes;
        record.has_success = hasSuccess;
        record.success = success;
        WriteEvent(record);
    }

    public void LogAnnotation(string eventName, string notes)
    {
        if (!isCollecting)
        {
            return;
        }

        EventRecord record = CreateBaseEvent("annotation", eventName, GetElapsedSeconds(), DateTime.UtcNow);
        record.notes = notes;
        WriteEvent(record);
    }

    public bool TryGetTrackedModuleObjectId(ModuleController controller, out string objectId)
    {
        return TryGetTrackedModuleObjectId(controller, out objectId, true);
    }

    public bool TryGetTrackedWebsiteModuleObjectId(CanvasManager canvasManager, out string moduleObjectId)
    {
        moduleObjectId = null;
        if (canvasManager == null || !isCollecting)
        {
            return false;
        }

        moduleObjectId = ResolveWebsiteModuleObjectId(canvasManager, true);
        return true;
    }

    private bool TryGetTrackedModuleObjectId(ModuleController controller, out string objectId, bool allowRefresh)
    {
        objectId = null;
        if (controller == null || !isCollecting)
        {
            return false;
        }

        int instanceId = controller.gameObject.GetInstanceID();
        if (!trackedModules.TryGetValue(instanceId, out TrackedModuleInfo info))
        {
            if (!allowRefresh)
            {
                return false;
            }

            RefreshTrackedObjects(GetElapsedSeconds(), DateTime.UtcNow);
        }

        if (!trackedModules.TryGetValue(instanceId, out info))
        {
            return false;
        }

        objectId = info.object_id;
        return true;
    }

    public bool TryGetTrackedWebsiteObjectId(CanvasManager canvasManager, out string objectId)
    {
        objectId = null;
        if (canvasManager == null || !isCollecting)
        {
            return false;
        }

        GameObject taggedRoot = FindTaggedWebsiteRoot(canvasManager.transform);
        if (taggedRoot == null)
        {
            return false;
        }

        return TryGetTrackedWebsiteObjectId(taggedRoot, out objectId);
    }

    public bool TryGetTrackedWebsiteObjectId(Transform target, out string objectId)
    {
        objectId = null;
        if (target == null || !isCollecting)
        {
            return false;
        }

        GameObject taggedRoot = FindTaggedWebsiteRoot(target);
        if (taggedRoot == null)
        {
            return false;
        }

        return TryGetTrackedWebsiteObjectId(taggedRoot, out objectId);
    }

    public bool TryGetTrackedWebsiteObjectId(GameObject target, out string objectId)
    {
        objectId = null;
        if (target == null || !isCollecting)
        {
            return false;
        }

        int instanceId = target.GetInstanceID();
        if (!trackedWebsites.TryGetValue(instanceId, out TrackedWebsiteInfo info))
        {
            RefreshTrackedObjects(GetElapsedSeconds(), DateTime.UtcNow);
        }

        if (!trackedWebsites.TryGetValue(instanceId, out info))
        {
            return false;
        }

        objectId = info.object_id;
        return true;
    }

    private void RecordSample()
    {
        if (samplesWriter == null || objectsWriter == null || eventsWriter == null || head == null)
        {
            EndSession();
            return;
        }

        DateTime utcNow = DateTime.UtcNow;
        float elapsedSeconds = GetElapsedSeconds();
        RefreshTrackedObjects(elapsedSeconds, utcNow);

        SampleRecord sample = new SampleRecord
        {
            session_id = sessionId,
            trial_id = activeTrialId,
            elapsed_seconds = elapsedSeconds,
            wall_clock_utc_iso8601 = utcNow.ToString("o", CultureInfo.InvariantCulture),
            head = CreatePosePayload(head),
            left_hand = CreateTrackedHandPayload(leftVisual.enabled, leftHand),
            right_hand = CreateTrackedHandPayload(rightVisual.enabled, rightHand),
            modules = BuildModuleSamples(),
            websites = BuildWebsiteSamples(),
        };

        samplesWriter.WriteLine(JsonUtility.ToJson(sample));

        if (sampleTimer < SampleIntervalSeconds * 0.5f)
        {
            FlushWriters();
        }
    }

    private void WriteSessionMetadata(string safeSessionName)
    {
        SessionMetaRecord meta = new SessionMetaRecord
        {
            schema_version = SchemaVersion,
            session_id = sessionId,
            session_name = safeSessionName,
            session_folder = dataFolderName,
            participant_id = activeParticipantId,
            study_id = activeStudyId,
            condition_id = activeConditionId,
            task_id = activeTaskId,
            started_at_utc_iso8601 = sessionStartUtc.ToString("o", CultureInfo.InvariantCulture),
            sample_interval_seconds = SampleIntervalSeconds,
            coordinate_space = new CoordinateSpaceRecord
            {
                frame = "Unity world space",
                handedness = "left-handed",
                position_units = "meters",
                scale_units = "meters",
                positive_x = "right",
                positive_y = "up",
                positive_z = "forward",
            },
            device_info = new DeviceInfoRecord
            {
                device_model = SystemInfo.deviceModel,
                device_name = SystemInfo.deviceName,
                device_type = SystemInfo.deviceType.ToString(),
                operating_system = SystemInfo.operatingSystem,
                graphics_device_name = SystemInfo.graphicsDeviceName,
            },
        };

        string metaPath = Path.Combine(sessionFolderPath, SessionMetaFileName);
        File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true), Encoding.UTF8);
    }

    private void RefreshTrackedObjects(float elapsedSeconds, DateTime utcNow)
    {
        RefreshModules(elapsedSeconds, utcNow);
        RefreshWebsites(elapsedSeconds, utcNow);
    }

    private void RefreshModules(float elapsedSeconds, DateTime utcNow)
    {
        HashSet<int> seen = new HashSet<int>();

        foreach (GameObject taggedObject in GetTaggedObjects("Module"))
        {
            if (!taggedObject.TryGetComponent(out ModuleController moduleController))
            {
                continue;
            }

            int instanceId = taggedObject.GetInstanceID();
            seen.Add(instanceId);

            string name = taggedObject.name;
            string hierarchyPath = GetHierarchyPath(taggedObject);

            if (!trackedModules.TryGetValue(instanceId, out TrackedModuleInfo info))
            {
                int assignedId = nextModuleId++;
                info = new TrackedModuleInfo
                {
                    instance_id = instanceId,
                    object_id = $"module_{assignedId}",
                    sequence = assignedId,
                    controller = moduleController,
                    game_object = taggedObject,
                    name = name,
                    hierarchy_path = hierarchyPath,
                };

                trackedModules.Add(instanceId, info);
                WriteObjectEvent("created", elapsedSeconds, utcNow, info.object_id, "Module", name, hierarchyPath);
                continue;
            }

            info.controller = moduleController;
            info.game_object = taggedObject;

            if (!StringEquals(info.name, name) || !StringEquals(info.hierarchy_path, hierarchyPath))
            {
                info.name = name;
                info.hierarchy_path = hierarchyPath;
                WriteObjectEvent("updated_identity", elapsedSeconds, utcNow, info.object_id, "Module", name, hierarchyPath);
            }
        }

        RemoveMissingModules(seen, elapsedSeconds, utcNow);
    }

    private void RefreshWebsites(float elapsedSeconds, DateTime utcNow)
    {
        HashSet<int> seen = new HashSet<int>();

        foreach (GameObject taggedObject in GetTaggedObjects("Website"))
        {
            int instanceId = taggedObject.GetInstanceID();
            seen.Add(instanceId);

            string name = taggedObject.name;
            string hierarchyPath = GetHierarchyPath(taggedObject);
            CanvasManager canvasManager = taggedObject.GetComponentInChildren<CanvasManager>(true);
            string moduleObjectId = ResolveWebsiteModuleObjectId(canvasManager, false);

            if (!trackedWebsites.TryGetValue(instanceId, out TrackedWebsiteInfo info))
            {
                int assignedId = nextWebsiteId++;
                info = new TrackedWebsiteInfo
                {
                    instance_id = instanceId,
                    object_id = $"website_{assignedId}",
                    sequence = assignedId,
                    transform = taggedObject.transform,
                    canvas_manager = canvasManager,
                    game_object = taggedObject,
                    name = name,
                    hierarchy_path = hierarchyPath,
                    module_object_id = moduleObjectId,
                };

                trackedWebsites.Add(instanceId, info);
                WriteObjectEvent("created", elapsedSeconds, utcNow, info.object_id, "Website", name, hierarchyPath, info.module_object_id);
                continue;
            }

            info.transform = taggedObject.transform;
            info.canvas_manager = canvasManager;
            info.game_object = taggedObject;

            string resolvedModuleObjectId = moduleObjectId;
            if (resolvedModuleObjectId == UnknownModuleObjectId && !string.IsNullOrEmpty(info.module_object_id))
            {
                resolvedModuleObjectId = info.module_object_id;
            }

            bool moduleChanged = !StringEquals(info.module_object_id, resolvedModuleObjectId);
            info.module_object_id = resolvedModuleObjectId;

            if (!StringEquals(info.name, name) || !StringEquals(info.hierarchy_path, hierarchyPath) || moduleChanged)
            {
                info.name = name;
                info.hierarchy_path = hierarchyPath;
                WriteObjectEvent("updated_identity", elapsedSeconds, utcNow, info.object_id, "Website", name, hierarchyPath, info.module_object_id);
            }
        }

        RemoveMissingWebsites(seen, elapsedSeconds, utcNow);
    }

    private void RemoveMissingModules(HashSet<int> seen, float elapsedSeconds, DateTime utcNow)
    {
        List<int> missing = new List<int>();

        foreach (KeyValuePair<int, TrackedModuleInfo> pair in trackedModules)
        {
            if (!seen.Contains(pair.Key))
            {
                missing.Add(pair.Key);
            }
        }

        for (int i = 0; i < missing.Count; i++)
        {
            TrackedModuleInfo removed = trackedModules[missing[i]];
            WriteObjectEvent("destroyed", elapsedSeconds, utcNow, removed.object_id, "Module", removed.name, removed.hierarchy_path);
            trackedModules.Remove(missing[i]);
        }
    }

    private void RemoveMissingWebsites(HashSet<int> seen, float elapsedSeconds, DateTime utcNow)
    {
        List<int> missing = new List<int>();

        foreach (KeyValuePair<int, TrackedWebsiteInfo> pair in trackedWebsites)
        {
            if (!seen.Contains(pair.Key))
            {
                missing.Add(pair.Key);
            }
        }

        for (int i = 0; i < missing.Count; i++)
        {
            TrackedWebsiteInfo removed = trackedWebsites[missing[i]];
            WriteObjectEvent("destroyed", elapsedSeconds, utcNow, removed.object_id, "Website", removed.name, removed.hierarchy_path, removed.module_object_id);
            trackedWebsites.Remove(missing[i]);
        }
    }

    private void WriteObjectEvent(
        string eventType,
        float elapsedSeconds,
        DateTime utcNow,
        string objectId,
        string tag,
        string name,
        string hierarchyPath,
        string moduleObjectId = null)
    {
        if (objectsWriter == null)
        {
            return;
        }

        ObjectEventRecord record = new ObjectEventRecord
        {
            session_id = sessionId,
            trial_id = activeTrialId,
            timestamp = utcNow.ToString("o", CultureInfo.InvariantCulture),
            elapsed_seconds = elapsedSeconds,
            event_type = eventType,
            object_id = objectId,
            module_object_id = moduleObjectId,
            tag = tag,
            name = name,
            hierarchy_path = hierarchyPath,
        };

        objectsWriter.WriteLine(JsonUtility.ToJson(record));
    }

    private void WriteEvent(EventRecord record)
    {
        if (eventsWriter == null || record == null)
        {
            return;
        }

        eventsWriter.WriteLine(JsonUtility.ToJson(record));
    }

    private EventRecord CreateBaseEvent(string eventType, string eventName, float elapsedSeconds, DateTime utcNow)
    {
        return new EventRecord
        {
            session_id = sessionId,
            trial_id = activeTrialId,
            timestamp = utcNow.ToString("o", CultureInfo.InvariantCulture),
            elapsed_seconds = elapsedSeconds,
            event_type = eventType,
            event_name = eventName,
            condition_id = activeConditionId,
            task_id = activeTaskId,
        };
    }

    private ModuleSampleRecord[] BuildModuleSamples()
    {
        return trackedModules.Values
            .Where(info => info.controller != null)
            .OrderBy(info => info.sequence)
            .Select(info => new ModuleSampleRecord
            {
                object_id = info.object_id,
                realPosition = CreateVector3Payload(info.controller.realPosition),
                realScale = CreateVector3Payload(info.controller.realScale),
                realRotation = CreateQuaternionPayload(info.controller.realRotation),
            })
            .ToArray();
    }

    private WebsiteSampleRecord[] BuildWebsiteSamples()
    {
        return trackedWebsites.Values
            .Where(info => info.transform != null)
            .OrderBy(info => info.sequence)
            .Select(info => new WebsiteSampleRecord
            {
                object_id = info.object_id,
                module_object_id = info.module_object_id,
                position = CreateVector3Payload(info.transform.position),
                scale = CreateVector3Payload(GetWebsiteDimensions(info)),
                rotation = CreateQuaternionPayload(info.transform.rotation),
                url = GetWebsiteUrl(info),
            })
            .ToArray();
    }

    private void DisposeWriters()
    {
        FlushWriters();
        samplesWriter?.Dispose();
        objectsWriter?.Dispose();
        eventsWriter?.Dispose();
        samplesWriter = null;
        objectsWriter = null;
        eventsWriter = null;
    }

    private void FlushWriters()
    {
        samplesWriter?.Flush();
        objectsWriter?.Flush();
        eventsWriter?.Flush();
    }

    private float GetElapsedSeconds()
    {
        return Time.time - sessionStartTime;
    }

    private string BuildSafeSessionName(string sessionNameOverride)
    {
        string source = string.IsNullOrWhiteSpace(sessionNameOverride) ? sessionName : sessionNameOverride;
        return string.IsNullOrWhiteSpace(source) ? DefaultSessionName : MakeFileNameSafe(source.Trim());
    }

    private void SetIndicatorColor(Color color)
    {
        if (statusIndicator != null)
        {
            statusIndicator.color = color;
        }
    }

    private static IEnumerable<GameObject> GetTaggedObjects(string tag)
    {
        return GameObject.FindGameObjectsWithTag(tag)
            .OrderBy(GetHierarchyPath, StringComparer.Ordinal);
    }

    private static PoseRecord CreatePosePayload(Transform target)
    {
        return new PoseRecord
        {
            position = CreateVector3Payload(target.position),
            rotation = CreateQuaternionPayload(target.rotation),
        };
    }

    private static HandSampleRecord CreateTrackedHandPayload(bool tracked, Transform target)
    {
        return new HandSampleRecord
        {
            tracked = tracked,
            position = CreateVector3Payload(tracked && target != null ? target.position : Vector3.zero),
            rotation = CreateQuaternionPayload(tracked && target != null ? target.rotation : Quaternion.identity),
        };
    }

    private static Vector3Record CreateVector3Payload(Vector3 value)
    {
        return new Vector3Record
        {
            x = value.x,
            y = value.y,
            z = value.z,
        };
    }

    private static QuaternionRecord CreateQuaternionPayload(Quaternion value)
    {
        return new QuaternionRecord
        {
            x = value.x,
            y = value.y,
            z = value.z,
            w = value.w,
        };
    }

    private static string GetWebsiteUrl(TrackedWebsiteInfo info)
    {
        if (info.canvas_manager != null && info.canvas_manager.TryGetURL(out string resolvedUrl))
        {
            return resolvedUrl;
        }

        return string.Empty;
    }

    private static Vector3 GetWebsiteDimensions(TrackedWebsiteInfo info)
    {
        if (info.canvas_manager != null)
        {
            Vector2 dimensions = info.canvas_manager.GetWidthHeight();
            return new Vector3(
                Mathf.Max(dimensions.x, 0.001f),
                Mathf.Max(dimensions.y, 0.001f),
                1f);
        }

        if (info.transform != null)
        {
            Vector3 fallbackScale = info.transform.lossyScale;
            return new Vector3(
                Mathf.Max(fallbackScale.x, 0.001f),
                Mathf.Max(fallbackScale.y, 0.001f),
                1f);
        }

        return Vector3.one;
    }

    private string ResolveWebsiteModuleObjectId(CanvasManager canvasManager, bool allowRefresh)
    {
        if (!TryResolveWebsiteModuleObjectId(canvasManager, out string moduleObjectId, false))
        {
            if (allowRefresh)
            {
                RefreshTrackedObjects(GetElapsedSeconds(), DateTime.UtcNow);
                if (TryResolveWebsiteModuleObjectId(canvasManager, out moduleObjectId, false))
                {
                    return moduleObjectId;
                }
            }

            return UnknownModuleObjectId;
        }

        return moduleObjectId;
    }

    private bool TryResolveWebsiteModuleObjectId(CanvasManager canvasManager, out string moduleObjectId, bool allowRefresh)
    {
        moduleObjectId = null;
        if (canvasManager == null || !isCollecting)
        {
            return false;
        }

        Transform target = canvasManager.Target != null ? canvasManager.Target : canvasManager.FindTargetLeafTransformByContentId();
        if (target == null)
        {
            return false;
        }

        ModuleController moduleController = target.GetComponentInParent<ModuleController>();
        if (moduleController == null)
        {
            return false;
        }

        return TryGetTrackedModuleObjectId(moduleController, out moduleObjectId, allowRefresh);
    }

    private static GameObject FindTaggedWebsiteRoot(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag("Website"))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    private static string MakeFileNameSafe(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        StringBuilder sanitized = new StringBuilder(value.Length);

        foreach (char c in value)
        {
            sanitized.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
        }

        return sanitized.Length == 0 ? DefaultSessionName : sanitized.ToString();
    }

    private static string GetHierarchyPath(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return string.Empty;
        }

        StringBuilder path = new StringBuilder(gameObject.name);
        Transform current = gameObject.transform.parent;
        while (current != null)
        {
            path.Insert(0, '/');
            path.Insert(0, current.name);
            current = current.parent;
        }

        return path.ToString();
    }

    private static string Coalesce(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                return values[i];
            }
        }

        return null;
    }

    private static bool StringEquals(string a, string b)
    {
        return string.Equals(a, b, StringComparison.Ordinal);
    }

    [Serializable]
    private sealed class SessionMetaRecord
    {
        public string schema_version;
        public string session_id;
        public string session_name;
        public string session_folder;
        public string participant_id;
        public string study_id;
        public string condition_id;
        public string task_id;
        public string started_at_utc_iso8601;
        public float sample_interval_seconds;
        public CoordinateSpaceRecord coordinate_space;
        public DeviceInfoRecord device_info;
    }

    [Serializable]
    private sealed class DeviceInfoRecord
    {
        public string device_model;
        public string device_name;
        public string device_type;
        public string operating_system;
        public string graphics_device_name;
    }

    [Serializable]
    private sealed class CoordinateSpaceRecord
    {
        public string frame;
        public string handedness;
        public string position_units;
        public string scale_units;
        public string positive_x;
        public string positive_y;
        public string positive_z;
    }

    [Serializable]
    private sealed class SampleRecord
    {
        public string session_id;
        public string trial_id;
        public float elapsed_seconds;
        public string wall_clock_utc_iso8601;
        public PoseRecord head;
        public HandSampleRecord left_hand;
        public HandSampleRecord right_hand;
        public ModuleSampleRecord[] modules;
        public WebsiteSampleRecord[] websites;
    }

    [Serializable]
    private sealed class EventRecord
    {
        public string session_id;
        public string trial_id;
        public string timestamp;
        public float elapsed_seconds;
        public string event_type;
        public string event_name;
        public string condition_id;
        public string task_id;
        public string module_object_id;
        public string object_id;
        public string object_type;
        public string related_object_id;
        public string related_object_type;
        public string secondary_object_id;
        public string secondary_object_type;
        public string url;
        public string content_id;
        public string outcome;
        public string details;
        public string notes;
        public bool has_success;
        public bool success;
        public bool has_duration_seconds;
        public float duration_seconds;
        public bool has_distance_meters;
        public float distance_meters;
    }

    [Serializable]
    private sealed class ObjectEventRecord
    {
        public string session_id;
        public string trial_id;
        public string timestamp;
        public float elapsed_seconds;
        public string event_type;
        public string object_id;
        public string module_object_id;
        public string tag;
        public string name;
        public string hierarchy_path;
    }

    [Serializable]
    private sealed class PoseRecord
    {
        public Vector3Record position;
        public QuaternionRecord rotation;
    }

    [Serializable]
    private sealed class HandSampleRecord
    {
        public bool tracked;
        public Vector3Record position;
        public QuaternionRecord rotation;
    }

    [Serializable]
    private sealed class ModuleSampleRecord
    {
        public string object_id;
        public Vector3Record realPosition;
        public Vector3Record realScale;
        public QuaternionRecord realRotation;
    }

    [Serializable]
    private sealed class WebsiteSampleRecord
    {
        public string object_id;
        public string module_object_id;
        public Vector3Record position;
        public Vector3Record scale;
        public QuaternionRecord rotation;
        public string url;
    }

    [Serializable]
    private sealed class Vector3Record
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    private sealed class QuaternionRecord
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    private sealed class TrackedModuleInfo
    {
        public int instance_id;
        public string object_id;
        public int sequence;
        public ModuleController controller;
        public GameObject game_object;
        public string name;
        public string hierarchy_path;
    }

    private sealed class TrackedWebsiteInfo
    {
        public int instance_id;
        public string object_id;
        public int sequence;
        public Transform transform;
        public CanvasManager canvas_manager;
        public GameObject game_object;
        public string name;
        public string hierarchy_path;
        public string module_object_id;
    }
}
