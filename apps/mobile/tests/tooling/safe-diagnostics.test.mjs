import assert from "node:assert/strict";
import { test } from "node:test";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import {
  checkpoints,
  createDiagnostics,
  errorCategory,
} from "../../e2e/safe-diagnostics.mjs";

test("diagnostics persist only the current fixed checkpoint and safe category", () => {
  const directory = mkdtempSync(path.join(tmpdir(), "pq-diagnostics-"));
  try {
    const file = path.join(directory, "diagnostics.json");
    const diagnostics = createDiagnostics(file);
    for (const checkpoint of checkpoints) {
      diagnostics.checkpoint(checkpoint);
      assert.deepEqual(JSON.parse(readFileSync(file, "utf8")), {
        checkpoint,
        errorCategory: checkpoint === "complete" ? "none" : "unknown",
      });
    }
    diagnostics.checkpoint("onboarding-save");
    for (const [error, category] of [
      [
        {
          name: "TimeoutError",
          message: "Bearer secret",
          headers: { Authorization: "secret" },
        },
        "timeout",
      ],
      [
        {
          matcherResult: { actual: "secret", expected: "secret" },
          stack: "secret",
        },
        "assertion",
      ],
      [
        { name: "Bearer secret", message: "https://private/?token=secret" },
        "unknown",
      ],
      [null, "unknown"],
      ["secret", "unknown"],
    ]) {
      diagnostics.fail(error);
      assert.deepEqual(JSON.parse(readFileSync(file, "utf8")), {
        checkpoint: "onboarding-save",
        errorCategory: category,
      });
    }
    diagnostics.checkpoint({ secret: "secret" });
    assert.deepEqual(JSON.parse(readFileSync(file, "utf8")), {
      checkpoint: "unknown",
      errorCategory: "unknown",
    });
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("diagnostic failures never replace journey failures or cleanup", () => {
  assert.equal(
    errorCategory({
      get name() {
        throw new Error("secret");
      },
    }),
    "unknown",
  );
  for (const filename of [undefined, "\0invalid"]) {
    const diagnostics = createDiagnostics(filename);
    assert.doesNotThrow(() => diagnostics.checkpoint("onboarding"));
    assert.doesNotThrow(() => diagnostics.fail(new Error("secret")));
  }
});

test("every scenario checkpoint is wired into the existing journey", () => {
  const journey = readFileSync(
    new URL("../../e2e/journey.mjs", import.meta.url),
    "utf8",
  );
  const actual = [
    ...journey.matchAll(/diagnostics\.checkpoint\("([a-z-]+)"\)/g),
  ].map((match) => match[1]);
  const sceneFailures = checkpoints.filter((checkpoint) =>
    checkpoint.startsWith("character-"),
  );
  assert.deepEqual(
    actual,
    checkpoints.filter((checkpoint) => !sceneFailures.includes(checkpoint)),
  );
  assert.deepEqual(
    [...new Set(
      [...journey.matchAll(/return "(character-[a-z]+)"/g)]
        .map((match) => match[1]),
    )],
    sceneFailures,
  );
  assert.match(journey, /catch \(error\) \{\s+diagnostics\.fail\(error\);/);
});

test("completion recovery assertions follow the committed and aborted response", () => {
  const journey = readFileSync(
    new URL("../../e2e/journey.mjs", import.meta.url),
    "utf8",
  );
  assert.match(
    journey,
    /if \(command\.action === "complete" && loseCompletionResponse\) \{\s+loseCompletionResponse = false;\s+const committed = await route\.fetch\(\);\s+expect\(committed\.ok\(\)\)\.toBe\(true\);\s+await route\.abort\("failed"\);[^\n]*\n\s+completionResponseAborted = true;/,
  );
  assert.match(
    journey,
    /diagnostics\.checkpoint\("completion-response-loss"\);[\s\S]*?\.click\(\{ clickCount: 2 \}\);[\s\S]*?\.poll\(\(\) => completionResponseAborted, \{ timeout: 15000 \}\)\s+\.toBe\(true\);\s+diagnostics\.checkpoint\("completion-offline-recovery"\);\s+await expect\(visibleText\(\/You’re offline\.\*saved progress\/\)\)\.toBeVisible\(\);/,
  );
});
