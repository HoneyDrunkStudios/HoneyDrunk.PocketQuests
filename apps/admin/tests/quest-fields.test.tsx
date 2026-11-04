import { useState } from "react";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it } from "vitest";
import fixtures from "../../../contracts/wire-fixtures.json";
import type { CatalogResponse, Quest } from "../../mobile/src/api/generated";
import { QuestFields } from "../src/features/quests/QuestFields";

afterEach(cleanup);

it("preserves reward inputs and keyboard focus while targets change or adjacent rows are removed", () => {
  const catalog = fixtures.catalog as CatalogResponse;
  const targets = catalog.attributes;
  function Form() {
    const [quest, setQuest] = useState<Quest>({
      ...catalog.quests[0],
      attributes: targets
        .slice(0, 2)
        .map((target) => ({ id: target.id, basisPoints: 5000 })),
      skills: [],
    });
    return (
      <QuestFields
        value={quest}
        catalog={catalog}
        disabled={false}
        onChange={setQuest}
      />
    );
  }
  render(<Form />);
  const first = screen.getByLabelText(
    "attributes target 1",
  ) as HTMLSelectElement;
  const retained = screen.getByLabelText("attributes percent 2");
  expect(
    first.querySelector<HTMLOptionElement>(`option[value="${targets[1].id}"]`)!
      .disabled,
  ).toBe(true);
  first.focus();
  fireEvent.change(first, { target: { value: targets[2].id } });
  expect(document.activeElement).toBe(
    screen.getByLabelText("attributes target 1"),
  );
  expect(
    (screen.getByLabelText("attributes percent 1") as HTMLInputElement).value,
  ).toBe("50");
  expect(screen.getByLabelText("attributes percent 2")).toBe(retained);
  fireEvent.click(screen.getAllByRole("button", { name: "Remove" })[0]);
  expect(screen.getByLabelText("attributes percent 1")).toBe(retained);
  expect((retained as HTMLInputElement).value).toBe("50");
});
