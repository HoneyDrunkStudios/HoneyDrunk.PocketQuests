import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import fixtures from "../../../contracts/wire-fixtures.json";
import { App } from "../src/App";
import type {
  AdminSystemQuest,
  AdminSystemQuestRevision,
  Quest,
} from "../src/api/admin-api";

let saved: AdminSystemQuest | null;
let history: AdminSystemQuestRevision[];
let malformedHistory: boolean;
let writes: number;
let loseSave: boolean;
let conflict: boolean;
let authenticated: boolean;
let publications: number;
let publicationFailure: "none" | "lost" | "malformed";
beforeEach(() => {
  saved = null;
  history = [];
  malformedHistory = false;
  writes = 0;
  loseSave = false;
  conflict = false;
  authenticated = true;
  publications = 0;
  publicationFailure = "none";
  vi.stubGlobal(
    "confirm",
    vi.fn(() => true),
  );
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: string, options: RequestInit) => {
      const body = options.body ? JSON.parse(String(options.body)) : null;
      const reply = (status: number, value: unknown) =>
        new Response(status === 204 ? null : JSON.stringify(value), {
          status,
          headers: { "Content-Type": "application/json" },
        });
      if (!authenticated) return reply(401, {});
      if (input.endsWith("/session"))
        return reply(200, {
          userId: "usr_" + "A".repeat(26),
          csrfToken: "fixture-only",
        });
      if (input.endsWith("/users/search"))
        return reply(200, { users: [], nextCursor: null });
      if (input.endsWith("/quests/list"))
        return reply(200, {
          quests: saved ? [saved] : [],
          catalog: fixtures.catalog,
        });
      if (input.endsWith("/quests/history")) {
        const rows = history
          .filter((row) => row.number < (body.before ?? 1001))
          .slice()
          .reverse();
        const page = rows.slice(0, 20);
        return reply(200, {
          id: malformedHistory ? crypto.randomUUID() : body.id,
          revisions: page,
          nextBefore: rows.length > 20 ? page.at(-1)!.number : null,
        });
      }
      if (input.endsWith("/quests/preview")) {
        if (!body.quest.title.trim())
          return reply(400, { detail: "Title: enter 1–120 characters." });
        return reply(200, {
          ...body.quest,
          title: body.quest.title.trim(),
          baseXp: 10,
        });
      }
      if (input.endsWith("/quests/draft")) {
        writes++;
        if (conflict) return reply(409, {});
        saved = {
          id: body.id,
          version: "AAAAAAAAAAE=",
          draftNumber: (saved?.draftNumber ?? 0) + 1,
          draftSystemQuestRevisionId: crypto.randomUUID(),
          publishedSystemQuestRevisionId:
            saved?.publishedSystemQuestRevisionId ?? null,
          draft: { ...body.quest, baseXp: 10 } as Quest,
          published: saved?.published ?? null,
          modifiedAt: "2026-01-01T12:00:00Z",
        };
        history.push({
          id: saved.draftSystemQuestRevisionId,
          number: saved.draftNumber,
          createdAt: saved.modifiedAt,
          quest: structuredClone(saved.draft),
        });
        return loseSave ? reply(503, {}) : reply(200, saved);
      }
      if (input.endsWith("/quests/publish")) {
        publications++;
        if (
          conflict ||
          !saved ||
          body.expectedDraftRevisionId !== saved.draftSystemQuestRevisionId
        )
          return reply(409, {});
        saved = {
          ...saved,
          version: "AAAAAAAAAAI=",
          published: body.published ? saved.draft : null,
          publishedSystemQuestRevisionId: body.published
            ? saved.draftSystemQuestRevisionId
            : null,
        };
        return publicationFailure === "lost"
          ? reply(503, {})
          : reply(
              200,
              publicationFailure === "malformed"
                ? { ...saved, id: crypto.randomUUID() }
                : saved,
            );
      }
      if (input.endsWith("/logout")) return reply(204, null);
      throw new Error("Unexpected fixture route: " + input);
    }),
  );
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

async function openStudio() {
  render(<App />);
  fireEvent.click(await screen.findByRole("button", { name: "System quests" }));
  await waitFor(() =>
    expect(
      (
        screen.getByRole("button", {
          name: "New system quest",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(false),
  );
  fireEvent.click(screen.getByRole("button", { name: "New system quest" }));
}
function enter(title: string) {
  fireEvent.change(screen.getByLabelText("Quest title"), {
    target: { value: title },
  });
  fireEvent.change(screen.getByLabelText("What counts as complete?"), {
    target: { value: "Finish a self-chosen walk" },
  });
}
async function preview() {
  fireEvent.click(screen.getByRole("button", { name: "Validate and preview" }));
  await waitFor(() =>
    expect(
      screen.getByRole("button", { name: "Save draft" }).matches(":disabled"),
    ).toBe(false),
  );
}

it("creates, validates, previews and edits private drafts without publishing", async () => {
  await openStudio();
  fireEvent.click(screen.getByRole("button", { name: "Validate and preview" }));
  await screen.findByText("Title: enter 1–120 characters.");
  enter("Walk somewhere new");
  await preview();
  expect(writes).toBe(0);
  fireEvent.click(screen.getByRole("button", { name: "Save draft" }));
  await screen.findByText("Draft 1 saved. It is not published to players.");
  expect(saved?.draft.title).toBe("Walk somewhere new");
  fireEvent.change(screen.getByLabelText("Quest title"), {
    target: { value: "Walk again" },
  });
  expect(
    screen.queryByRole("heading", { name: "Walk somewhere new" }),
  ).toBeNull();
  expect(
    (screen.getByRole("button", { name: "Save draft" }) as HTMLButtonElement)
      .disabled,
  ).toBe(true);
  await preview();
  fireEvent.click(screen.getByRole("button", { name: "Save draft" }));
  await screen.findByText("Draft 2 saved. It is not published to players.");
  expect(writes).toBe(2);
  expect(saved?.published).toBeNull();
});

it("recovers a committed save whose response was lost without creating a second draft", async () => {
  await openStudio();
  enter("Walk by the river");
  await preview();
  loseSave = true;
  fireEvent.click(screen.getByRole("button", { name: "Save draft" }));
  fireEvent.click(
    await screen.findByRole("button", {
      name: "Reload saved version and discard local edits",
    }),
  );
  await screen.findByText(
    "Saved draft reloaded. Review it before making a new change.",
  );
  expect(writes).toBe(1);
  expect((screen.getByLabelText("Quest title") as HTMLInputElement).value).toBe(
    "Walk by the river",
  );
  expect(
    (screen.getByRole("button", { name: "Save draft" }) as HTMLButtonElement)
      .disabled,
  ).toBe(true);
});

it("retains conflicting edits until explicit reload and clears the workshop on session expiry", async () => {
  await openStudio();
  enter("Walk with a friend");
  await preview();
  conflict = true;
  fireEvent.click(screen.getByRole("button", { name: "Save draft" }));
  await screen.findByRole("button", {
    name: "Reload saved version and discard local edits",
  });
  expect((screen.getByLabelText("Quest title") as HTMLInputElement).value).toBe(
    "Walk with a friend",
  );
  authenticated = false;
  fireEvent.click(
    screen.getByRole("button", {
      name: "Reload saved version and discard local edits",
    }),
  );
  await screen.findByText(/This session cannot access/);
  expect(screen.queryByLabelText("Quest title")).toBeNull();
});

it("previews edited reward pools and protects unsaved terms when copying a starter or leaving", async () => {
  await openStudio();
  enter("Walk with purpose");
  fireEvent.change(screen.getByLabelText("Description (optional)"), {
    target: { value: "Choose a route that feels comfortable." },
  });
  for (const [label, value] of [
    ["Category", fixtures.catalog.categories[1].id],
    ["Rank", "E"],
    ["Effort", "Medium"],
    ["Optional deadline penalty", "25"],
  ])
    fireEvent.change(screen.getByLabelText(label), { target: { value } });
  fireEvent.click(screen.getByRole("button", { name: "Add attribute" }));
  fireEvent.click(screen.getByRole("button", { name: "Add attribute" }));
  fireEvent.change(screen.getByLabelText("attributes target 1"), {
    target: { value: fixtures.catalog.attributes[2].id },
  });
  fireEvent.change(screen.getByLabelText("attributes percent 1"), {
    target: { value: "60" },
  });
  fireEvent.change(screen.getByLabelText("attributes percent 2"), {
    target: { value: "40" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Add skill" }));
  fireEvent.change(screen.getByLabelText("skills target 1"), {
    target: { value: fixtures.catalog.skills[1].id },
  });
  await preview();
  await screen.findByText(/Optional deadline penalty: 25%/);
  expect(screen.getByText(/: 60%/)).toBeTruthy();
  expect(screen.getByText(/: 40%/)).toBeTruthy();
  expect(writes).toBe(0);
  vi.mocked(window.confirm).mockReturnValueOnce(false);
  fireEvent.click(screen.getByRole("button", { name: "Players" }));
  expect(screen.getByLabelText("Quest title")).toBeTruthy();
  vi.mocked(window.confirm).mockReturnValueOnce(false);
  fireEvent.change(screen.getByLabelText("Start from a starter quest"), {
    target: { value: fixtures.catalog.quests[0].id },
  });
  expect((screen.getByLabelText("Quest title") as HTMLInputElement).value).toBe(
    "Walk with purpose",
  );
  fireEvent.click(screen.getAllByRole("button", { name: "Remove" })[2]);
  expect(screen.queryByLabelText("skills target 1")).toBeNull();
  expect(
    screen.queryByRole("heading", { name: "Walk with purpose" }),
  ).toBeNull();
  fireEvent.change(screen.getByLabelText("Start from a starter quest"), {
    target: { value: fixtures.catalog.quests[0].id },
  });
  expect((screen.getByLabelText("Quest title") as HTMLInputElement).value).toBe(
    fixtures.catalog.quests[0].title + " (copy)",
  );
  expect(writes).toBe(0);
  fireEvent.click(screen.getByRole("button", { name: "Players" }));
  await screen.findByRole("heading", { name: "Your travelers" });
  expect(screen.queryByLabelText("Quest title")).toBeNull();
});

async function savedQuest() {
  await openStudio();
  enter("Walk from the workshop");
  await preview();
  await act(async () => {
    fireEvent.click(screen.getByRole("button", { name: "Save draft" }));
  });
  await screen.findByText("Draft 1 saved. It is not published to players.");
}

it("reviews saved terms before publication and withdrawal while edits stay private", async () => {
  await savedQuest();
  fireEvent.click(screen.getByRole("button", { name: "Review publication" }));
  expect(publications).toBe(0);
  expect(screen.getByLabelText("Quest title").matches(":disabled")).toBe(true);
  fireEvent.click(
    screen.getByRole("button", { name: "Cancel publication review" }),
  );
  expect(publications).toBe(0);
  fireEvent.click(screen.getByRole("button", { name: "Review publication" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Publish reviewed draft" }),
  );
  await screen.findByText("Draft 1 is now published to players.");
  expect(publications).toBe(1);
  fireEvent.change(screen.getByLabelText("Quest title"), {
    target: { value: "Walk with new terms" },
  });
  expect(
    (
      screen.getByRole("button", {
        name: "Review publication",
      }) as HTMLButtonElement
    ).disabled,
  ).toBe(true);
  await preview();
  fireEvent.click(screen.getByRole("button", { name: "Save draft" }));
  await screen.findByText("Draft 2 saved. The published version is unchanged.");
  expect(saved?.published?.title).toBe("Walk from the workshop");
  fireEvent.click(screen.getByRole("button", { name: "Review publication" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Publish reviewed draft" }),
  );
  await screen.findByText("Draft 2 is now published to players.");
  expect(saved?.published?.title).toBe("Walk with new terms");
  fireEvent.click(screen.getByRole("button", { name: "Review withdrawal" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Withdraw from discovery" }),
  );
  await screen.findByText(
    "Withdrawn from discovery. Accepted quests and recurring series keep their existing terms.",
  );
  expect(publications).toBe(3);
  expect(saved?.published).toBeNull();
});

it.each(["lost", "malformed"] as const)(
  "recovers a %s publication response without another write",
  async (failure) => {
    await savedQuest();
    publicationFailure = failure;
    fireEvent.click(screen.getByRole("button", { name: "Review publication" }));
    fireEvent.click(
      screen.getByRole("button", { name: "Publish reviewed draft" }),
    );
    fireEvent.click(
      await screen.findByRole("button", {
        name: "Reload saved version and discard local edits",
      }),
    );
    await screen.findByText(
      "Saved draft reloaded. Review it before making a new change.",
    );
    expect(publications).toBe(1);
    expect(saved?.published?.title).toBe("Walk from the workshop");
    expect(
      (
        screen.getByRole("button", {
          name: "Review publication",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(true);
  },
);

it("requires reload after a conflicting publication review", async () => {
  await savedQuest();
  conflict = true;
  fireEvent.click(screen.getByRole("button", { name: "Review publication" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Publish reviewed draft" }),
  );
  await screen.findByText(/This system quest changed/);
  expect(
    (
      screen.getByRole("button", {
        name: "Publish reviewed draft",
      }) as HTMLButtonElement
    ).disabled,
  ).toBe(true);
  expect(saved?.published).toBeNull();
});

it("inspects immutable history and restores a new private draft only after explicit validation and save", async () => {
  await savedQuest();
  fireEvent.click(screen.getByRole("button", { name: "Review publication" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Publish reviewed draft" }),
  );
  await screen.findByText("Draft 1 is now published to players.");
  enter("Walk with later terms");
  await preview();
  fireEvent.click(screen.getByRole("button", { name: "Save draft" }));
  await screen.findByText("Draft 2 saved. The published version is unchanged.");
  const originalId = history[0].id;
  fireEvent.click(
    screen.getByRole("button", { name: "Load revision history" }),
  );
  fireEvent.click(
    await screen.findByRole("button", {
      name: /Revision 1 · Walk from the workshop/,
    }),
  );
  expect(writes).toBe(2);
  const previewIds = [...document.querySelectorAll("[id]")].map(
    (element) => element.id,
  );
  expect(new Set(previewIds).size).toBe(previewIds.length);
  enter("Walk with unsaved text");
  vi.mocked(window.confirm).mockReturnValueOnce(false);
  fireEvent.click(
    screen.getByRole("button", { name: "Use revision 1 as draft" }),
  );
  expect((screen.getByLabelText("Quest title") as HTMLInputElement).value).toBe(
    "Walk with unsaved text",
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Use revision 1 as draft" }),
  );
  expect((screen.getByLabelText("Quest title") as HTMLInputElement).value).toBe(
    "Walk from the workshop",
  );
  expect(document.activeElement).toBe(
    screen.getByRole("button", { name: "Validate and preview" }),
  );
  expect(
    screen.getByRole("button", { name: "Save draft" }).matches(":disabled"),
  ).toBe(true);
  expect(writes).toBe(2);
  expect(publications).toBe(1);
  await preview();
  fireEvent.click(screen.getByRole("button", { name: "Save draft" }));
  await screen.findByText("Draft 3 saved. The published version is unchanged.");
  expect(saved?.publishedSystemQuestRevisionId).toBe(originalId);
  expect(history[0].id).toBe(originalId);
  expect(history[2].quest.title).toBe("Walk from the workshop");
  expect(history[2].id).not.toBe(originalId);
  const headings = [...document.querySelectorAll("[id]")].map(
    (element) => element.id,
  );
  expect(new Set(headings).size).toBe(headings.length);
});

it("rejects history for another quest without locking the editor and clears history on session expiry", async () => {
  await savedQuest();
  malformedHistory = true;
  fireEvent.click(
    screen.getByRole("button", { name: "Load revision history" }),
  );
  await screen.findByText(/The revision history could not be verified/);
  expect(screen.queryByRole("button", { name: /Use revision/ })).toBeNull();
  expect(screen.getByLabelText("Quest title").matches(":disabled")).toBe(false);
  authenticated = false;
  fireEvent.click(
    screen.getByRole("button", { name: "Load revision history" }),
  );
  await screen.findByText(/This session cannot access/);
  expect(screen.queryByRole("region", { name: "Revision history" })).toBeNull();
});
