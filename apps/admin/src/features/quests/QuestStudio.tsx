import { useEffect, useRef, useState } from "react";
import { AdminApi, AdminApiError } from "../../api/admin-api";
import type {
  AdminSystemQuest,
  AdminSystemQuestRevision,
  Quest,
  Session,
  SystemQuestStudio,
} from "../../api/admin-api";
import { Masthead } from "../../components/Masthead";
import { QuestFields } from "./QuestFields";
import { QuestPreview } from "./QuestPreview";
import { QuestHistory } from "./QuestHistory";
import {
  QuestPublicationReview,
  QuestPublicationStatus,
} from "./QuestPublicationStatus";

export function QuestStudio({
  session,
  api,
  onEndSession,
  onPlayers,
}: Readonly<{
  session: Session;
  api: AdminApi;
  onEndSession: (message: string) => void;
  onPlayers: () => void;
}>) {
  const [studio, setStudio] = useState<SystemQuestStudio | null>(null);
  const [selected, setSelected] = useState<AdminSystemQuest | null>(null);
  const [identity, setIdentity] = useState("");
  const [form, setForm] = useState<Quest | null>(null);
  const [preview, setPreview] = useState<Quest | null>(null);
  const [dirty, setDirty] = useState(false);
  const [publication, setPublication] = useState<boolean | null>(null);
  const [pending, setPending] = useState(false);
  const [locked, setLocked] = useState(false);
  const [notice, setNotice] = useState("Loading the quest studio…");
  const [search, setSearch] = useState("");
  const [error, setError] = useState(false);
  const previewButton = useRef<HTMLButtonElement>(null);
  const busy = useRef(false);
  const requests = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    requests.current = controller;
    void api
      .studio(controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) {
          setStudio(value);
          setNotice("Create a new system quest or choose a saved draft.");
        }
      })
      .catch((cause) => {
        if (controller.signal.aborted) return;
        if (cause instanceof AdminApiError && [401, 403].includes(cause.status))
          onEndSession(cause.message);
        else {
          setNotice("The studio could not load. Try loading it again.");
          setError(true);
        }
      });
    return () => controller.abort();
  }, [api, onEndSession]);

  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  function mayLeave() {
    return (
      !busy.current &&
      (!dirty ||
        window.confirm("Discard the unsaved changes in this quest draft?"))
    );
  }

  function edit(saved: AdminSystemQuest) {
    if (locked || !mayLeave()) return;
    setSelected(saved);
    setIdentity(saved.id);
    setForm(saved.draft);
    setPreview(null);
    setPublication(null);
    setDirty(false);
    setError(false);
    setNotice(
      `Editing saved draft ${saved.draftNumber}. Changes stay private until saved and published.`,
    );
  }

  function create(seed?: Quest) {
    if (locked || !studio || !mayLeave()) return;
    const id = crypto.randomUUID();
    setIdentity(id);
    setSelected(null);
    setPreview(null);
    setPublication(null);
    setDirty(true);
    setError(false);
    setForm({
      ...(seed ?? studio.catalog.quests[0]),
      id: "PQ-ADM-" + id.replaceAll("-", ""),
      title: seed ? `${seed.title} (copy)`.slice(0, 120) : "",
      criterion: seed?.criterion ?? "",
      description: seed?.description ?? "",
      isCustom: false,
      penaltyPercent: seed?.penaltyPercent ?? 0,
      attributes: seed?.attributes ?? [],
      skills: seed?.skills ?? [],
    });
    setNotice(
      "New private draft. Enter the outcome, preview its terms, then save it.",
    );
  }

  async function perform<T>(
    operation: (signal: AbortSignal) => Promise<T>,
    success: (value: T) => void,
    mutation = false,
  ) {
    const signal = requests.current?.signal;
    if (busy.current || !signal || signal.aborted) return;
    busy.current = true;
    setPending(true);
    setError(false);
    try {
      const value = await operation(signal);
      if (!signal.aborted) success(value);
    } catch (cause) {
      if (signal.aborted) return;
      if (cause instanceof AdminApiError && [401, 403].includes(cause.status))
        onEndSession(cause.message);
      else {
        setNotice(studioFailureMessage(cause));
        setError(true);
        if (mutation) setLocked(true);
      }
    } finally {
      if (!signal.aborted) {
        busy.current = false;
        setPending(false);
      }
    }
  }

  function change(value: Quest) {
    setForm(value);
    setPreview(null);
    setPublication(null);
    setDirty(true);
  }
  function restoreRevision(revision: AdminSystemQuestRevision) {
    if (locked || publication !== null || !selected || !mayLeave()) return;
    if (revision.quest.id !== selected.draft.id) return;
    change(revision.quest);
    setError(false);
    setNotice(
      `Revision ${revision.number} loaded into your unsaved draft. Validate and save to create a new revision. Publication is unchanged.`,
    );
    previewButton.current?.focus();
  }
  function body() {
    return {
      id: identity,
      expectedVersion: selected?.version ?? null,
      quest: form!,
    };
  }
  function showPreview() {
    if (!form || locked) return;
    void perform(
      (signal) => api.previewQuest(body(), signal),
      (value) => {
        setPreview(value);
        setNotice(
          "Preview checked. Save this as a private draft before publishing.",
        );
      },
    );
  }
  function retain(saved: AdminSystemQuest) {
    setSelected(saved);
    setIdentity(saved.id);
    setForm(saved.draft);
    setPreview(saved.draft);
    setDirty(false);
    setStudio(
      (old) =>
        old && {
          ...old,
          quests: [...old.quests.filter((row) => row.id !== saved.id), saved],
        },
    );
    setPublication(null);
  }
  function save() {
    if (!form || !preview || locked) return;
    void perform(
      (signal) => api.saveQuest(body(), signal),
      (saved) => {
        retain(saved);
        setNotice(
          saved.published
            ? `Draft ${saved.draftNumber} saved. The published version is unchanged.`
            : `Draft ${saved.draftNumber} saved. It is not published to players.`,
        );
      },
      true,
    );
  }
  function reviewPublication(published: boolean) {
    if (!selected || dirty || locked || busy.current) return;
    setPreview(selected.draft);
    setPublication(published);
    setNotice(
      published
        ? "Review these saved terms before publishing them to players."
        : "Review withdrawal. Accepted quests and recurring deliveries keep their existing terms.",
    );
  }
  function publish() {
    if (!selected || dirty || locked || publication === null) return;
    const published = publication;
    void perform(
      (signal) =>
        api.publishQuest(
          {
            id: selected.id,
            expectedVersion: selected.version,
            expectedDraftRevisionId: selected.draftSystemQuestRevisionId,
            published,
          },
          signal,
        ),
      (saved) => {
        retain(saved);
        setNotice(
          published
            ? `Draft ${saved.draftNumber} is now published to players.`
            : "Withdrawn from discovery. Accepted quests and recurring series keep their existing terms.",
        );
      },
      true,
    );
  }
  function reload() {
    if (
      dirty &&
      !window.confirm("Reload the saved version and discard these local edits?")
    )
      return;
    void perform(
      (signal) => api.studio(signal),
      (value) => {
        setStudio(value);
        const saved = value.quests.find((row) => row.id === identity);
        setSelected(saved ?? null);
        setForm(saved?.draft ?? null);
        setIdentity(saved?.id ?? "");
        setPreview(null);
        setDirty(false);
        setLocked(false);
        setPublication(null);
        setNotice(
          saved
            ? "Saved draft reloaded. Review it before making a new change."
            : "No saved draft exists for that identity. You can create a new draft.",
        );
      },
    );
  }
  const visible =
    studio?.quests.filter((row) =>
      row.draft.title
        .toLocaleLowerCase()
        .includes(search.trim().toLocaleLowerCase()),
    ) ?? [];
  return (
    <>
      <Masthead
        userId={session.userId}
        pending={pending}
        onSignOut={() => {
          if (mayLeave())
            void perform(
              (signal) => api.logout(signal),
              () => onEndSession("You have signed out of the console."),
            );
        }}
      />
      <main>
        <nav className="section-nav" aria-label="Administration">
          <button
            className="quiet"
            disabled={pending}
            onClick={() => {
              if (mayLeave()) onPlayers();
            }}
          >
            Players
          </button>
          <span aria-current="page">System quests</span>
        </nav>
        <div className="page-heading">
          <div>
            <p className="eyebrow">QUEST WORKSHOP</p>
            <h1>Make the next adventure</h1>
            <p>
              Create clear outcomes and thoughtful rewards for every traveler.
            </p>
          </div>
          <button
            disabled={!studio || pending || locked}
            onClick={() => create()}
          >
            New system quest
          </button>
        </div>
        <p
          className={`notice${error ? " error" : ""}`}
          role="status"
          aria-live="polite"
        >
          {pending ? "Working…" : notice}
        </p>
        {(!studio || locked) && (
          <button className="quiet" disabled={pending} onClick={reload}>
            {locked
              ? "Reload saved version and discard local edits"
              : "Reload quest studio"}
          </button>
        )}
        {studio && (
          <div className="quest-workspace">
            <aside className="panel quest-directory">
              <h2>Your system quests</h2>
              <label htmlFor="quest-search">Find a draft</label>
              <input
                id="quest-search"
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
              <div className="user-list">
                {visible.map((row) => (
                  <button
                    className="user-row"
                    key={row.id}
                    aria-pressed={row.id === selected?.id}
                    disabled={pending || locked}
                    onClick={() => edit(row)}
                  >
                    <span>
                      {row.draft.title}
                      <small className="user-id">
                        Draft {row.draftNumber} ·{" "}
                        {row.published
                          ? "Published version exists"
                          : "Private draft"}
                      </small>
                    </span>
                  </button>
                ))}
              </div>
              {!visible.length && <p>No saved drafts match this search.</p>}
              <label htmlFor="starter">Start from a starter quest</label>
              <select
                id="starter"
                disabled={pending || locked}
                defaultValue=""
                onChange={(event) => {
                  const seed = studio.catalog.quests.find(
                    (row) => row.id === event.target.value,
                  );
                  if (seed) create(seed);
                  event.target.value = "";
                }}
              >
                <option value="">Choose a quest to copy…</option>
                {studio.catalog.quests.map((row) => (
                  <option key={row.id} value={row.id}>
                    {row.title}
                  </option>
                ))}
              </select>
              <p className="fine">
                Starter templates stay read-only. Copies receive their own
                identity and history.
              </p>
            </aside>
            <section
              className="panel quest-editor"
              aria-label="System quest editor"
            >
              {form ? (
                <>
                  <div className="panel-heading">
                    <h2>
                      {selected ? `Draft ${selected.draftNumber}` : "New draft"}
                    </h2>
                    <span className="status">
                      {dirty ? "Unsaved changes" : "Saved"}
                    </span>
                  </div>
                  <QuestFields
                    value={form}
                    catalog={studio.catalog}
                    disabled={pending || locked || publication !== null}
                    onChange={change}
                  />
                  <div className="actions">
                    <button
                      disabled={pending || locked || publication !== null}
                      ref={previewButton}
                      onClick={showPreview}
                    >
                      Validate and preview
                    </button>
                    <button
                      className="quiet"
                      disabled={
                        pending ||
                        locked ||
                        !preview ||
                        !dirty ||
                        publication !== null
                      }
                      onClick={save}
                    >
                      Save draft
                    </button>
                  </div>
                  <p className="fine">
                    Draft changes are private. Existing accepted quests retain
                    their original terms.
                  </p>
                  {selected && (
                    <>
                      <QuestPublicationStatus
                        selected={selected}
                        dirty={dirty}
                        disabled={
                          pending || locked || dirty || publication !== null
                        }
                        onReview={reviewPublication}
                      />
                      <QuestHistory
                        key={selected.draftSystemQuestRevisionId}
                        selected={selected}
                        api={api}
                        catalog={studio.catalog}
                        disabled={pending || locked || publication !== null}
                        onRestore={restoreRevision}
                        onEndSession={onEndSession}
                      />
                    </>
                  )}
                  {publication !== null && (
                    <QuestPublicationReview
                      published={publication}
                      disabled={pending || locked}
                      onPublish={publish}
                      onCancel={() => setPublication(null)}
                    />
                  )}
                  {preview && (
                    <QuestPreview quest={preview} catalog={studio.catalog} />
                  )}
                </>
              ) : (
                <p className="empty">
                  Choose a draft or create a new system quest.
                </p>
              )}
            </section>
          </div>
        )}
      </main>
    </>
  );
}

function studioFailureMessage(cause: unknown) {
  if (cause instanceof AdminApiError && cause.status === 409)
    return "This system quest changed. Reload the saved version before another change; your local edits are still shown.";
  return cause instanceof Error
    ? cause.message
    : "The request could not be confirmed.";
}
