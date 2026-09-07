import {
  startTransition,
  useEffect,
  useRef,
  useState,
} from "react";
import FileLoader from "./components/FileLoader";
import ReplayViewport from "./components/ReplayViewport";
import {
  clamp,
  findSessionById,
  formatElapsed,
  formatUtc,
  getDefaultSelectedOccurrenceTypes,
  getNextOccurrence,
  getReplayFrame,
} from "./lib/study";
import type { LayerState, LoadedStudy, Occurrence } from "./types";

const DEFAULT_LAYERS: LayerState = {
  showHead: true,
  showLeftHand: true,
  showRightHand: true,
  showModules: true,
  showWebsites: true,
  showTrails: true,
  showModuleLabels: true,
  showWebsiteLabels: true,
  showAxes: true,
};

const TRAIL_LENGTH_MIN = 5;
const TRAIL_LENGTH_MAX = 200;

export default function App(): JSX.Element {
  const [study, setStudy] = useState<LoadedStudy | null>(null);
  const [activeSessionId, setActiveSessionId] = useState<
    string | null
  >(null);
  const [selectedTypeKeys, setSelectedTypeKeys] = useState<string[]>(
    [],
  );
  const [activeElapsedSeconds, setActiveElapsedSeconds] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const [layers, setLayers] = useState<LayerState>(DEFAULT_LAYERS);
  const [trailLengthControl, setTrailLengthControl] = useState(40);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadStatus, setLoadStatus] = useState("No study loaded.");
  const playbackFrameRef = useRef<number | null>(null);
  const lastPlaybackTickRef = useRef<number | null>(null);

  const activeSession = findSessionById(study, activeSessionId);
  const replayFrame = getReplayFrame(
    activeSession,
    activeElapsedSeconds,
  );
  const trailLengthLimit =
    trailLengthControl >= TRAIL_LENGTH_MAX
      ? null
      : trailLengthControl;

  useEffect(() => {
    if (!activeSession) {
      setIsPlaying(false);
      return;
    }

    const step = (now: number) => {
      if (lastPlaybackTickRef.current === null) {
        lastPlaybackTickRef.current = now;
      }

      const deltaSeconds = (now - lastPlaybackTickRef.current) / 1000;
      lastPlaybackTickRef.current = now;
      setActiveElapsedSeconds((current) => {
        const next = current + deltaSeconds * playbackSpeed;
        const clamped = clamp(
          next,
          activeSession.elapsedRangeSeconds.start,
          activeSession.elapsedRangeSeconds.end,
        );
        if (clamped >= activeSession.elapsedRangeSeconds.end) {
          setIsPlaying(false);
        }
        return clamped;
      });

      playbackFrameRef.current = requestAnimationFrame(step);
    };

    if (isPlaying) {
      playbackFrameRef.current = requestAnimationFrame(step);
    }

    return () => {
      if (playbackFrameRef.current !== null) {
        cancelAnimationFrame(playbackFrameRef.current);
      }
      playbackFrameRef.current = null;
      lastPlaybackTickRef.current = null;
    };
  }, [activeSession, isPlaying, playbackSpeed]);

  useEffect(() => {
    if (!activeSession) {
      return;
    }

    setActiveElapsedSeconds((current) =>
      clamp(
        current,
        activeSession.elapsedRangeSeconds.start,
        activeSession.elapsedRangeSeconds.end,
      ),
    );
  }, [activeSessionId, activeSession]);

  function handleStudyLoaded(nextStudy: LoadedStudy) {
    startTransition(() => {
      setStudy(nextStudy);
      setActiveSessionId(
        nextStudy.sessions[0]?.meta.session_id ?? null,
      );
      setSelectedTypeKeys(
        getDefaultSelectedOccurrenceTypes(nextStudy),
      );
      setActiveElapsedSeconds(
        nextStudy.sessions[0]?.elapsedRangeSeconds.start ?? 0,
      );
      setLoadError(null);
      setLoadStatus(
        `Loaded ${nextStudy.sessions.length} session(s).`,
      );
    });
  }

  function handleOccurrenceSelect(occurrence: Occurrence) {
    if (occurrence.sessionId !== activeSessionId) {
      setActiveSessionId(occurrence.sessionId);
    }
    setActiveElapsedSeconds(occurrence.elapsedSeconds);
  }

  function handleSessionChange(nextSessionId: string) {
    const nextSession = findSessionById(study, nextSessionId);
    setActiveSessionId(nextSessionId);
    if (nextSession) {
      setActiveElapsedSeconds(nextSession.elapsedRangeSeconds.start);
    }
  }

  function jumpOccurrence(direction: 1 | -1) {
    if (!activeSession) {
      return;
    }

    const next = getNextOccurrence(
      activeSession,
      activeElapsedSeconds,
      selectedTypeKeys,
      direction,
    );
    if (next) {
      handleOccurrenceSelect(next);
    }
  }

  function nudgeFrame(direction: 1 | -1) {
    if (!activeSession) {
      return;
    }

    const step = activeSession.meta.sample_interval_seconds || 0.2;
    setActiveElapsedSeconds((current) =>
      clamp(
        current + direction * step,
        activeSession.elapsedRangeSeconds.start,
        activeSession.elapsedRangeSeconds.end,
      ),
    );
  }

  function setLayer<K extends keyof LayerState>(
    key: K,
    value: LayerState[K],
  ) {
    setLayers((current) => ({
      ...current,
      [key]: value,
    }));
  }

  return (
    <div className="app-shell">
      <aside className="left-rail">
        <div className="panel">
          <FileLoader
            onStudyLoaded={handleStudyLoaded}
            onError={setLoadError}
            onStatus={setLoadStatus}
          />
          <div className="status-block">
            <strong>Status</strong>
            <div>{loadStatus}</div>
            {loadError ? (
              <div className="error-text">{loadError}</div>
            ) : null}
          </div>
        </div>

        {study ? (
          <>
            <div className="panel">
              <label className="field">
                <span>Active session</span>
                <select
                  value={activeSessionId ?? ""}
                  onChange={(event) =>
                    handleSessionChange(event.target.value)
                  }
                >
                  {study.sessions.map((session) => (
                    <option
                      key={session.meta.session_id}
                      value={session.meta.session_id}
                    >
                      {session.meta.session_folder}
                    </option>
                  ))}
                </select>
              </label>
              {activeSession ? (
                <div className="mini-meta">
                  <div>
                    Session ID:{" "}
                    <code>{activeSession.meta.session_id}</code>
                  </div>
                  <div>
                    Time:{" "}
                    {formatUtc(activeSession.timeRangeUtcMs.start)} to{" "}
                    {formatUtc(activeSession.timeRangeUtcMs.end)}
                  </div>
                  <div>
                    Samples: {activeSession.samples.length} | Events:{" "}
                    {activeSession.events.length} | Objects:{" "}
                    {activeSession.objects.length}
                  </div>
                </div>
              ) : null}
            </div>

            <div className="panel">
              <div className="toggle-grid">
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={layers.showHead}
                    onChange={(event) =>
                      setLayer("showHead", event.target.checked)
                    }
                  />
                  Head
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={layers.showLeftHand}
                    onChange={(event) =>
                      setLayer("showLeftHand", event.target.checked)
                    }
                  />
                  Left Hand
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={layers.showRightHand}
                    onChange={(event) =>
                      setLayer("showRightHand", event.target.checked)
                    }
                  />
                  Right Hand
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={layers.showModules}
                    onChange={(event) =>
                      setLayer("showModules", event.target.checked)
                    }
                  />
                  Modules
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={layers.showWebsites}
                    onChange={(event) =>
                      setLayer("showWebsites", event.target.checked)
                    }
                  />
                  Websites
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={layers.showTrails}
                    onChange={(event) =>
                      setLayer("showTrails", event.target.checked)
                    }
                  />
                  Trails
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={layers.showModuleLabels}
                    onChange={(event) =>
                      setLayer(
                        "showModuleLabels",
                        event.target.checked,
                      )
                    }
                  />
                  Module Labels
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={layers.showWebsiteLabels}
                    onChange={(event) =>
                      setLayer(
                        "showWebsiteLabels",
                        event.target.checked,
                      )
                    }
                  />
                  Website Labels
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={layers.showAxes}
                    onChange={(event) =>
                      setLayer("showAxes", event.target.checked)
                    }
                  />
                  Axes
                </label>
                </div>
              <label className="field trail-length-field">
                <span>
                  Trail length:{" "}
                  <strong>
                    {trailLengthLimit === null
                      ? "Infinite"
                      : `${trailLengthLimit} frames`}
                  </strong>
                </span>
                <input
                  type="range"
                  min={TRAIL_LENGTH_MIN}
                  max={TRAIL_LENGTH_MAX}
                  step={1}
                  value={trailLengthControl}
                  onChange={(event) =>
                    setTrailLengthControl(Number(event.target.value))
                  }
                />
              </label>
            </div>
          </>
        ) : null}
      </aside>

      <main className="main-column">
        <section className="panel replay-panel">
          <ReplayViewport
            replayFrame={replayFrame}
            layers={layers}
            trailLengthLimit={trailLengthLimit}
          />
        </section>

        <section className="scrubber-bar panel">
          <div className="scrubber-top-row">
            <div className="scrubber-controls">
              <button
                type="button"
                onClick={() => setIsPlaying((current) => !current)}
                disabled={!activeSession}
              >
                {isPlaying ? "Pause" : "Play"}
              </button>
              <button
                type="button"
                onClick={() => nudgeFrame(-1)}
                disabled={!activeSession}
              >
                Prev Frame
              </button>
              <button
                type="button"
                onClick={() => nudgeFrame(1)}
                disabled={!activeSession}
              >
                Next Frame
              </button>
              <button
                type="button"
                onClick={() => jumpOccurrence(-1)}
                disabled={
                  !activeSession || selectedTypeKeys.length === 0
                }
              >
                Prev Occurrence
              </button>
              <button
                type="button"
                onClick={() => jumpOccurrence(1)}
                disabled={
                  !activeSession || selectedTypeKeys.length === 0
                }
              >
                Next Occurrence
              </button>
            </div>
            <label className="speed-field scrubber-speed">
              <span>Speed</span>
              <select
                value={playbackSpeed}
                onChange={(event) =>
                  setPlaybackSpeed(Number(event.target.value))
                }
              >
                <option value={0.25}>0.25x</option>
                <option value={0.5}>0.5x</option>
                <option value={1}>1x</option>
                <option value={2}>2x</option>
                <option value={4}>4x</option>
              </select>
            </label>
          </div>
          <div className="scrubber-bottom-row">
            <div className="scrubber-readout">
              <div className="scrubber-metric">
                <span className="scrubber-label">Elapsed</span>
                <strong>{formatElapsed(activeElapsedSeconds)}</strong>
              </div>
              <div className="scrubber-metric">
                <span className="scrubber-label">UTC</span>
                <strong>
                  {formatUtc(replayFrame?.currentUtcMs ?? null)}
                </strong>
              </div>
            </div>
            {activeSession ? (
              <input
                className="scrubber-slider"
                type="range"
                min={activeSession.elapsedRangeSeconds.start}
                max={activeSession.elapsedRangeSeconds.end}
                step={
                  activeSession.meta.sample_interval_seconds || 0.2
                }
                value={activeElapsedSeconds}
                onChange={(event) =>
                  setActiveElapsedSeconds(Number(event.target.value))
                }
              />
            ) : (
              <input
                className="scrubber-slider"
                type="range"
                min={0}
                max={1}
                value={0}
                disabled
              />
            )}
          </div>
        </section>

        <section className="panel legend-panel">
          <div className="legend-grid">
            <div className="legend-item">
              <span className="legend-swatch head" />
              <span>Head</span>
            </div>
            <div className="legend-item">
              <span className="legend-swatch left-hand" />
              <span>Left hand</span>
            </div>
            <div className="legend-item">
              <span className="legend-swatch right-hand" />
              <span>Right hand</span>
            </div>
            <div className="legend-item">
              <span className="legend-swatch module" />
              <span>Module</span>
            </div>
            <div className="legend-item">
              <span className="legend-swatch website" />
              <span>Website</span>
            </div>
          </div>
        </section>

        <section className="panel websites-panel">
          <div className="websites-panel-header">Open Websites</div>
          <div className="websites-table-scroll">
            <table className="websites-table">
              <thead>
                <tr>
                  <th>ID</th>
                  <th>URL</th>
                </tr>
              </thead>
              <tbody>
                {activeSession && replayFrame?.sample
                  ? replayFrame.sample.websites.map((w) => (
                      <tr key={w.object_id}>
                        <td className="websites-id">{w.object_id}</td>
                        <td className="websites-url">{w.url || "\u2014"}</td>
                      </tr>
                    ))
                  : null}
              </tbody>
            </table>
          </div>
        </section>
      </main>
    </div>
  );
}
