import { useRef } from "react";
import {
  loadStudyFromFiles,
  showStudyDirectoryPicker,
} from "../lib/study";
import type { LoadedStudy } from "../types";

interface FileLoaderProps {
  onStudyLoaded: (study: LoadedStudy) => void;
  onError: (message: string | null) => void;
  onStatus: (message: string) => void;
}

export default function FileLoader(
  props: FileLoaderProps,
): JSX.Element {
  const filesInputRef = useRef<HTMLInputElement | null>(null);
  const folderInputRef = useRef<HTMLInputElement | null>(null);

  async function loadFromFiles(files: File[]) {
    if (files.length === 0) {
      props.onError("No files were provided.");
      return;
    }

    try {
      props.onStatus(`Loading ${files.length} file(s)...`);
      const study = await loadStudyFromFiles(files);
      if (study.sessions.length === 0) {
        props.onError(
          "No complete session bundles were found. Provide a UserStudy root, session folders, or the four raw session files.",
        );
        props.onStatus("Nothing loaded.");
        return;
      }

      props.onStudyLoaded(study);
      props.onError(null);
    } catch (error) {
      props.onError(
        error instanceof Error ? error.message : String(error),
      );
      props.onStatus("Load failed.");
    }
  }

  async function handlePickDirectory() {
    try {
      props.onStatus("Opening folder picker...");
      const study = await showStudyDirectoryPicker();
      if (!study) {
        props.onError(
          "File System Access API is unavailable in this browser. Use folder import or drag/drop instead.",
        );
        props.onStatus("Folder picker unavailable.");
        return;
      }

      if (study.sessions.length === 0) {
        props.onError(
          "No session folders were found under the selected directory.",
        );
        props.onStatus("Nothing loaded.");
        return;
      }

      props.onStudyLoaded(study);
      props.onError(null);
    } catch (error) {
      props.onError(
        error instanceof Error ? error.message : String(error),
      );
      props.onStatus("Directory load failed.");
    }
  }

  function handleFileSelection(
    event: React.ChangeEvent<HTMLInputElement>,
  ) {
    const files = [...(event.target.files ?? [])];
    void loadFromFiles(files);
    event.target.value = "";
  }

  function handleDrop(event: React.DragEvent<HTMLDivElement>) {
    event.preventDefault();
    const files = [...event.dataTransfer.files];
    void loadFromFiles(files);
  }

  return (
    <div className="loader-stack">
      <div className="loader-actions">
        <button
          type="button"
          onClick={() => void handlePickDirectory()}
        >
          Load Study Folder
        </button>
        <button
          type="button"
          onClick={() => folderInputRef.current?.click()}
        >
          Import Folder(s)
        </button>
        <button
          type="button"
          onClick={() => filesInputRef.current?.click()}
        >
          Import Files
        </button>
      </div>
      <input
        ref={filesInputRef}
        type="file"
        multiple
        hidden
        onChange={handleFileSelection}
        accept=".json,.ndjson"
      />
      <input
        ref={folderInputRef}
        type="file"
        hidden
        multiple
        onChange={handleFileSelection}
        accept=".json,.ndjson"
        {...({ webkitdirectory: "", directory: "" } as Record<
          string,
          string
        >)}
      />
    </div>
  );
}
