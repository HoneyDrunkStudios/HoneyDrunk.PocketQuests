import type {
  CatalogResponse,
  Quest,
} from "../../../../mobile/src/api/generated";

export function QuestPreview({
  quest,
  catalog,
}: {
  quest: Quest;
  catalog: CatalogResponse;
}) {
  return (
    <section className="quest-preview" aria-labelledby="quest-preview-heading">
      <p className="eyebrow">PLAYER PREVIEW · SERVER VALIDATED</p>
      <h2 id="quest-preview-heading">{quest.title}</h2>
      <p>{quest.description}</p>
      <h3>Complete when</h3>
      <p>{quest.criterion}</p>
      <p>
        <strong>{quest.baseXp} base XP</strong> · Rank {quest.rank} ·{" "}
        {quest.effort} ·{" "}
        {catalog.categories.find((row) => row.id === quest.categoryId)?.name}
      </p>
      <p>
        {quest.penaltyPercent
          ? `Optional deadline penalty: ${quest.penaltyPercent}%. Travelers explicitly accept penalty commitments.`
          : "No deadline penalty."}
      </p>
      <p className="fine">
        Rank prerequisites still apply. Category streak bonuses depend on the
        traveler's activity.
      </p>
      {(["attributes", "skills"] as const).map((key) => (
        <div key={key}>
          <h3>{key === "attributes" ? "Attribute" : "Skill"} pool</h3>
          {quest[key].length ? (
            <ul>
              {quest[key].map((share) => (
                <li key={share.id}>
                  {catalog[key].find((row) => row.id === share.id)?.name}:{" "}
                  {share.basisPoints / 100}%
                </li>
              ))}
            </ul>
          ) : (
            <p>Unallocated</p>
          )}
        </div>
      ))}
    </section>
  );
}
