import { useEffect, useRef, useState } from "react";
import { AdminApi, AdminApiError } from "../../api/admin-api";
import type {
  AdminSystemQuest,
  AdminSystemQuestRevision,
} from "../../api/admin-api";
import type { CatalogResponse } from "../../../../mobile/src/api/generated";
import { QuestPreview } from "./QuestPreview";

export function QuestHistory({
  selected,
  api,
  catalog,
  disabled,
  onRestore,
  onEndSession,
}: Readonly<{
  selected: AdminSystemQuest;
  api: AdminApi;
  catalog: CatalogResponse;
  disabled: boolean;
  onRestore: (revision: AdminSystemQuestRevision) => void;
  onEndSession: (message: string) => void;
}>) {
  const [entries, setEntries] = useState<AdminSystemQuestRevision[]>([]);
  const [before, setBefore] = useState<number | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [pending, setPending] = useState(false);
  const [notice, setNotice] = useState("");
  const [inspected, setInspected] = useState<AdminSystemQuestRevision | null>(
    null,
  );
  const lifetime = useRef<AbortController | null>(null);
  const busy = useRef(false);
  useEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    return () => controller.abort();
  }, []);

  async function load(more: boolean) {
    const signal = lifetime.current?.signal;
    if (disabled || busy.current || !signal || signal.aborted) return;
    busy.current = true;
    setPending(true);
    setNotice("Loading saved revisions…");
    try {
      const page = await api.questHistory(
        selected.id,
        more ? before : null,
        signal,
      );
      if (signal.aborted) return;
      setEntries((old) =>
        more ? [...old, ...page.revisions] : page.revisions,
      );
      setBefore(page.nextBefore);
      setLoaded(true);
      if (!more) setInspected(null);
      setNotice(
        "Saved revisions loaded. Choose a version to inspect its terms.",
      );
    } catch (error) {
      if (signal.aborted) return;
      if (error instanceof AdminApiError && [401, 403].includes(error.status))
        onEndSession(error.message);
      else setNotice(historyFailureMessage(error));
    } finally {
      if (!signal.aborted) {
        busy.current = false;
        setPending(false);
      }
    }
  }

  return (
    <section className="revision-history" aria-label="Revision history">
      <h3>Revision history</h3>
      <p className="fine">
        Inspect saved terms or bring an older version into a new draft. Accepted
        quests and publication keep their existing terms.
      </p>
      <button
        className="quiet"
        disabled={disabled || pending}
        onClick={() => void load(false)}
      >
        {loaded ? "Refresh revision history" : "Load revision history"}
      </button>
      <p role="status">{notice}</p>
      {loaded && (
        <RevisionEntries
          entries={entries}
          selected={selected}
          inspected={inspected?.id}
          disabled={disabled || pending}
          onInspect={setInspected}
        />
      )}
      {loaded && before !== null && (
        <button
          className="quiet"
          disabled={disabled || pending}
          onClick={() => void load(true)}
        >
          Load older revisions
        </button>
      )}
      {inspected && (
        <section aria-label={`Inspect revision ${inspected.number}`}>
          <h4>Revision {inspected.number}</h4>
          <QuestPreview quest={inspected.quest} catalog={catalog} />
          <button
            disabled={
              disabled ||
              pending ||
              inspected.id === selected.draftSystemQuestRevisionId
            }
            onClick={() => onRestore(inspected)}
          >
            Use revision {inspected.number} as draft
          </button>
          <p className="fine">
            Review and validate the restored content before saving a new
            revision. Publishing requires a separate review.
          </p>
        </section>
      )}
    </section>
  );
}

function RevisionEntries({
  entries,
  selected,
  inspected,
  disabled,
  onInspect,
}: Readonly<{
  entries: AdminSystemQuestRevision[];
  selected: AdminSystemQuest;
  inspected?: string;
  disabled: boolean;
  onInspect: (revision: AdminSystemQuestRevision) => void;
}>) {
  return (
    <ol className="revision-list">
      {entries.map((entry) => (
        <li key={entry.id}>
          <button
            className="quiet"
            aria-pressed={inspected === entry.id}
            disabled={disabled}
            onClick={() => onInspect(entry)}
          >
            Revision {entry.number} · {entry.quest.title}
            {entry.id === selected.draftSystemQuestRevisionId &&
              " · Current draft"}
            {entry.id === selected.publishedSystemQuestRevisionId &&
              " · Published"}
          </button>
          <p className="fine">
            Saved {new Date(entry.createdAt).toLocaleString()}
          </p>
        </li>
      ))}
    </ol>
  );
}

function historyFailureMessage(error: unknown) {
  if (error instanceof AdminApiError)
    return "Revision history could not load. Try again. Your current draft is kept.";
  return error instanceof Error
    ? error.message
    : "Revision history could not load. Try again.";
}
