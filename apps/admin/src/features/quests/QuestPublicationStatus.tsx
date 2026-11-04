import type { AdminSystemQuest } from "../../api/admin-api";

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
