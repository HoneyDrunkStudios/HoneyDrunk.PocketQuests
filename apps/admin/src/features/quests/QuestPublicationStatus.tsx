import type { AdminSystemQuest } from "../../api/admin-api";

export function QuestPublicationReview({
  published,
  disabled,
  onPublish,
  onCancel,
}: Readonly<{
  published: boolean;
  disabled: boolean;
  onPublish: () => void;
  onCancel: () => void;
}>) {
  return (
    <section
      className="publication-review"
      aria-label="Confirm publication change"
    >
      <h3>
        {published
          ? "Publish this saved draft?"
          : "Withdraw this quest from discovery?"}
      </h3>
      <p>
        {published
          ? "New acceptances will use the terms below. Existing accepted quests and recurring series retain their original terms."
          : "Players will no longer find or newly accept this template. Their accepted quests, recurring series and rewards remain available."}
      </p>
      <div className="actions">
        <button disabled={disabled} onClick={onPublish}>
          {published ? "Publish reviewed draft" : "Withdraw from discovery"}
        </button>
        <button className="quiet" disabled={disabled} onClick={onCancel}>
          Cancel publication review
        </button>
      </div>
    </section>
  );
}

export function QuestPublicationStatus({
  selected,
  dirty,
  disabled,
  onReview,
}: Readonly<{
  selected: AdminSystemQuest;
  dirty: boolean;
  disabled: boolean;
  onReview: (published: boolean) => void;
}>) {
  return (
    <section className="publication" aria-label="Publication status">
      <h3>{selected.published ? "Published to players" : "Private draft"}</h3>
      {selected.published && (
        <p>
          Live: {selected.published.title} · {selected.published.baseXp} base
          XP.{" "}
          {selected.publishedSystemQuestRevisionId ===
          selected.draftSystemQuestRevisionId
            ? "This saved draft is live."
            : "Newer draft changes are private."}
        </p>
      )}
      <div className="actions">
        <button
          className="quiet"
          disabled={
            disabled ||
            selected.publishedSystemQuestRevisionId ===
              selected.draftSystemQuestRevisionId
          }
          onClick={() => onReview(true)}
        >
          Review publication
        </button>
        {selected.published && (
          <button
            className="quiet"
            disabled={disabled}
            onClick={() => onReview(false)}
          >
            Review withdrawal
          </button>
        )}
      </div>
      {dirty && (
        <p className="fine">
          Save and review your latest draft before changing publication.
        </p>
      )}
    </section>
  );
}
