import { useRef } from "react";
import type {
  CatalogResponse,
  Quest,
  Share,
} from "../../../../mobile/src/api/generated";

export function QuestFields({
  value,
  catalog,
  disabled,
  onChange,
}: Readonly<{
  value: Quest;
  catalog: CatalogResponse;
  disabled: boolean;
  onChange: (quest: Quest) => void;
}>) {
  const focusTarget = useRef<{
    pool: "attributes" | "skills";
    id: string;
  } | null>(null);
  function restoreTargetFocus(
    node: HTMLSelectElement | null,
    pool: "attributes" | "skills",
    id: string,
  ) {
    if (
      node &&
      focusTarget.current?.pool === pool &&
      focusTarget.current.id === id
    ) {
      node.focus();
      focusTarget.current = null;
    }
  }

  function pool(
    key: "attributes" | "skills",
    options: { id: string; name: string }[],
  ) {
    const shares = value[key];
    const update = (next: Share[]) => onChange({ ...value, [key]: next });
    return (
      <fieldset disabled={disabled}>
        <legend>
          {key === "attributes" ? "Attribute" : "Skill"} reward pool
        </legend>
        <p className="fine">
          Leave empty or allocate exactly 100%. Each pool uses the base XP
          independently.
        </p>
        {shares.map((share, index) => (
          <div className="pool-row" key={share.id}>
            <select
              ref={(node) => restoreTargetFocus(node, key, share.id)}
              aria-label={`${key} target ${index + 1}`}
              value={share.id}
              onChange={(event) => {
                const id = event.target.value;
                focusTarget.current = { pool: key, id };
                update(
                  shares.map((row, at) =>
                    at === index ? { ...row, id } : row,
                  ),
                );
              }}
            >
              {options.map((option) => (
                <option
                  key={option.id}
                  value={option.id}
                  disabled={shares.some(
                    (row) => row !== share && row.id === option.id,
                  )}
                >
                  {option.name}
                </option>
              ))}
            </select>
            <label>
              Percent{" "}
              <input
                aria-label={`${key} percent ${index + 1}`}
                type="number"
                min="0"
                max="100"
                step="0.01"
                value={share.basisPoints / 100}
                onChange={(event) =>
                  update(
                    shares.map((row, at) =>
                      at === index
                        ? {
                            ...row,
                            basisPoints: Math.round(
                              Number(event.target.value) * 100,
                            ),
                          }
                        : row,
                    ),
                  )
                }
              />
            </label>
            <button
              type="button"
              className="quiet"
              onClick={() => update(shares.filter((_, at) => at !== index))}
            >
              Remove
            </button>
          </div>
        ))}
        <p className="fine">
          Allocated:{" "}
          {shares.reduce((sum, row) => sum + row.basisPoints, 0) / 100}%
        </p>
        <button
          type="button"
          className="quiet"
          disabled={shares.length >= options.length}
          onClick={() => {
            const option = options.find((option) =>
              shares.every((row) => row.id !== option.id),
            );
            if (option)
              update([
                ...shares,
                { id: option.id, basisPoints: shares.length ? 0 : 10000 },
              ]);
          }}
        >
          Add {key === "attributes" ? "attribute" : "skill"}
        </button>
      </fieldset>
    );
  }
  return (
    <fieldset className="quest-fields" disabled={disabled}>
      <legend className="sr-only">Quest terms</legend>
      <label htmlFor="quest-title">Quest title</label>
      <input
        id="quest-title"
        maxLength={120}
        value={value.title}
        onChange={(event) => onChange({ ...value, title: event.target.value })}
      />
      <label htmlFor="quest-criterion">What counts as complete?</label>
      <textarea
        id="quest-criterion"
        rows={3}
        maxLength={2000}
        value={value.criterion}
        onChange={(event) =>
          onChange({ ...value, criterion: event.target.value })
        }
      />
      <label htmlFor="quest-description">Description (optional)</label>
      <textarea
        id="quest-description"
        rows={3}
        maxLength={2000}
        value={value.description ?? ""}
        onChange={(event) =>
          onChange({ ...value, description: event.target.value })
        }
      />
      <div className="quest-options">
        <label>
          Category{" "}
          <select
            value={value.categoryId}
            onChange={(event) =>
              onChange({ ...value, categoryId: event.target.value })
            }
          >
            {catalog.categories.map((row) => (
              <option key={row.id} value={row.id}>
                {row.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Rank{" "}
          <select
            value={value.rank}
            onChange={(event) =>
              onChange({ ...value, rank: event.target.value as Quest["rank"] })
            }
          >
            {["F", "E", "D", "C", "B", "A", "S"].map((rank) => (
              <option key={rank}>{rank}</option>
            ))}
          </select>
        </label>
        <label>
          Effort{" "}
          <select
            value={value.effort}
            onChange={(event) =>
              onChange({
                ...value,
                effort: event.target.value as Quest["effort"],
              })
            }
          >
            {["Small", "Medium", "Large"].map((effort) => (
              <option key={effort}>{effort}</option>
            ))}
          </select>
        </label>
        <label>
          Optional deadline penalty{" "}
          <select
            value={value.penaltyPercent}
            onChange={(event) =>
              onChange({ ...value, penaltyPercent: Number(event.target.value) })
            }
          >
            {[0, 10, 25, 50].map((percent) => (
              <option key={percent} value={percent}>
                {percent === 0 ? "None" : `${percent}%`}
              </option>
            ))}
          </select>
        </label>
      </div>
      {pool("attributes", catalog.attributes)}
      {pool("skills", catalog.skills)}
    </fieldset>
  );
}
