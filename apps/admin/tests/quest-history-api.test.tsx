import { afterEach, expect, it, vi } from "vitest";
import fixtures from "../../../contracts/wire-fixtures.json";
import { AdminApi } from "../src/api/admin-api";
import type { AdminSystemQuestRevision } from "../src/api/admin-api";

afterEach(() => vi.unstubAllGlobals());
const id = "9375baf0-ecba-4e79-a782-f66715348589";
function revision(number: number): AdminSystemQuestRevision {
  return {
    id: crypto.randomUUID(),
    number,
    createdAt: "2026-11-04T12:00:00Z",
    quest: {
      ...fixtures.catalog.quests[0],
      id: "PQ-ADM-" + id.replaceAll("-", ""),
    },
  };
}
function api(page: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn(
      async () =>
        new Response(JSON.stringify(page), {
          headers: { "Content-Type": "application/json" },
        }),
    ),
  );
  return new AdminApi({
    userId: "usr_" + "A".repeat(26),
    csrfToken: "fixture-only",
  });
}

it("normalizes a permitted numeric-string cursor before requesting the older page", async () => {
  const entries = Array.from({ length: 20 }, (_, index) =>
    revision(22 - index),
  );
  const service = api({ id, revisions: entries, nextBefore: "3" });
  const page = await service.questHistory(
    id,
    null,
    new AbortController().signal,
  );
  expect(page.nextBefore).toBe(3);
  vi.stubGlobal(
    "fetch",
    vi.fn(
      async () =>
        new Response(
          JSON.stringify({
            id,
            revisions: [revision(2), revision(1)],
            nextBefore: null,
          }),
        ),
    ),
  );
  const older = await service.questHistory(
    id,
    page.nextBefore,
    new AbortController().signal,
  );
  expect(older.revisions.map((row) => row.number)).toEqual([2, 1]);
  expect(
    JSON.parse(vi.mocked(fetch).mock.calls[0][1]!.body as string).before,
  ).toBe(3);
});

it.each([
  [
    "another quest",
    () => [
      {
        ...revision(2),
        quest: { ...fixtures.catalog.quests[0], id: "PQ-CAT-Q01" },
      },
    ],
  ],
  [
    "a duplicate identity",
    () => {
      const row = revision(2);
      return [row, { ...row, number: 1 }];
    },
  ],
  ["unordered versions", () => [revision(1), revision(2)]],
  ["an invalid timestamp", () => [{ ...revision(2), createdAt: "not-a-date" }]],
  [
    "too many rows",
    () => Array.from({ length: 21 }, (_, index) => revision(22 - index)),
  ],
] as const)(
  "rejects history containing %s before restoration is offered",
  async (_name, rows) => {
    await expect(
      api({ id, revisions: rows(), nextBefore: null }).questHistory(
        id,
        null,
        new AbortController().signal,
      ),
    ).rejects.toThrow(/verified/);
  },
);

it("rejects a cursor that skips revisions and rows outside the requested older page", async () => {
  const entries = Array.from({ length: 20 }, (_, index) =>
    revision(22 - index),
  );
  await expect(
    api({ id, revisions: entries, nextBefore: 2 }).questHistory(
      id,
      null,
      new AbortController().signal,
    ),
  ).rejects.toThrow(/verified/);
  await expect(
    api({ id, revisions: [revision(3)], nextBefore: null }).questHistory(
      id,
      3,
      new AbortController().signal,
    ),
  ).rejects.toThrow(/verified/);
});


it.each(["", " 3", "03", "3.0", "3e0", "NaN", "1001", "-3"])(
  "rejects malformed or out-of-range legacy cursor %s",
  async (nextBefore) => {
    const entries = Array.from({ length: 20 }, (_, index) => revision(22 - index));
    await expect(
      api({ id, revisions: entries, nextBefore }).questHistory(
        id, null, new AbortController().signal,
      ),
    ).rejects.toThrow(/verified/);
  },
);

it("rejects a malformed history response before cursor normalization", async () => {
  await expect(
    api(null).questHistory(id, null, new AbortController().signal),
  ).rejects.toThrow(/verified/);
});
