import { chromium, expect } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import { createDiagnostics } from "./safe-diagnostics.mjs";
const diagnostics = createDiagnostics(
  process.env.POCKETQUESTS_JOURNEY_DIAGNOSTICS,
);
diagnostics.checkpoint("setup");
const root = path.resolve("../..");
const artifacts = path.resolve(
  root,
  process.env.POCKETQUESTS_ARTIFACT_DIRECTORY ?? "artifacts",
);
await fs.mkdir(artifacts, { recursive: true });
const fixtureDirectory = path.resolve(
  root,
  process.env.POCKETQUESTS_FIXTURE_DIRECTORY ?? ".local/browser",
);
const fixture = JSON.parse(
  await fs.readFile(path.join(fixtureDirectory, "ready.json"), "utf8"),
);
const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: 390, height: 844 },
  reducedMotion: "reduce",
});
const page = await context.newPage();
// Native navigation retains hidden screens; assertions target the visible one.
const visibleText = (text, options) =>
  page
    .getByText(text, options)
    .and(page.locator(':not([aria-hidden="true"], [aria-hidden="true"] *)'))
    .filter({ visible: true });
page.setDefaultTimeout(15000);
// A cold Metro compilation can exceed the action timeout; keep navigation bounded separately.
page.setDefaultNavigationTimeout(60000);
const failures = [];
page.on("pageerror", (error) => failures.push(error.message));
const codes = new Map();
const cors = {
  "access-control-allow-origin": "http://localhost:8081",
  "access-control-allow-headers": "*",
  "access-control-allow-methods": "GET, POST, OPTIONS",
};
// Only the external OIDC provider is a test double. Identity validates the signed
// token, and every product request reaches the actual API and isolated SQL databases.
await context.route("http://localhost:5218/client-configuration", (route) =>
  route.fulfill({
    json: {
      authority: "https://identity.test",
      clientId: "browser-fixture",
      scope: "quests",
    },
    headers: cors,
  }),
);
await context.route("https://identity.test/**", async (route) => {
  const request = route.request();
  const url = new URL(request.url());
  if (request.method() === "OPTIONS")
    return route.fulfill({ status: 204, headers: cors });
  if (url.pathname.endsWith("openid-configuration"))
    return route.fulfill({
      headers: cors,
      json: {
        issuer: "https://identity.test",
        authorization_endpoint: "https://identity.test/authorize",
        token_endpoint: "https://identity.test/token",
        response_types_supported: ["code"],
        code_challenge_methods_supported: ["S256"],
      },
    });
  if (url.pathname === "/authorize") {
    const code = crypto.randomUUID();
    codes.set(code, url.searchParams.get("code_challenge"));
    const callback = new URL(url.searchParams.get("redirect_uri"));
    callback.searchParams.set("code", code);
    callback.searchParams.set("state", url.searchParams.get("state"));
    return route.fulfill({
      status: 302,
      headers: { location: callback.toString() },
    });
  }
  if (url.pathname === "/token") {
    const body = new URLSearchParams(request.postData());
    const code = body.get("code");
    const challenge = crypto
      .createHash("sha256")
      .update(body.get("code_verifier") ?? "")
      .digest("base64url");
    if (!codes.has(code) || codes.get(code) !== challenge)
      return route.fulfill({
        status: 400,
        headers: cors,
        json: { error: "invalid_grant" },
      });
    codes.delete(code);
    return route.fulfill({
      headers: cors,
      json: {
        access_token: fixture.token,
        token_type: "Bearer",
        expires_in: 600,
      },
    });
  }
  return route.abort();
});
try {
  diagnostics.checkpoint("sign-in");
  await page.goto("http://localhost:8081");
  const signIn = page.getByRole("button", {
    name: "Sign in or create an account",
  });
  await expect(signIn).toBeEnabled({ timeout: 90000 });
  await signIn.click();
  await expect(
    visibleText("Your adventure starts here", { exact: true }),
  ).toBeVisible({ timeout: 30000 });
  diagnostics.checkpoint("onboarding");
  await page.getByRole("button", { name: "Let’s begin", exact: true }).click();
  await page
    .getByRole("checkbox", { name: "Work & Purpose", exact: true })
    .click();
  await expect(
    page.getByRole("checkbox", { name: "Work & Purpose", exact: true }),
  ).toBeChecked();
  await page.getByRole("button", { name: "Continue", exact: true }).click();
  diagnostics.checkpoint("onboarding-save");
  await page
    .getByRole("button", {
      name: "Choose my first quest",
      exact: true,
    })
    .click();
  await expect(page).toHaveURL("http://localhost:8081/board");
  await expect(
    page.getByRole("heading", { name: "Pick your next quest", exact: true }),
  ).toBeVisible();
  // Experience is optional profile editing, outside the first-run journey.
  await page
    .getByRole("link", { name: "Settings and profile", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Programming: Expert", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Confirm experience correction", exact: true })
    .click();
  await expect(
    visibleText("Current assessment: Expert", { exact: true }),
  ).toBeVisible({ timeout: 15000 });
  await page.getByRole("tab", { name: "Quests", exact: true }).click();
  diagnostics.checkpoint("custom-quest");
  await page
    .getByRole("button", { name: "Create a custom quest", exact: true })
    .click();
  await page
    .getByLabel("Quest title", { exact: true })
    .fill("Browser verified custom quest");
  await page
    .getByLabel("Completion criterion", { exact: true })
    .fill("I have delivered the working result");
  await page
    .getByRole("button", { name: "Work & Purpose", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Add Programming", exact: true })
    .click();
  await page.getByRole("button", { name: "Rank A", exact: true }).click();
  await page.getByRole("button", { name: "Medium", exact: true }).click();
  let rejectSave = true;
  let loseCompletionResponse = true;
  let skewNextCompletion = false;
  let clockRecordedAt = null;
  let clockAnchor = null;
  let clockUndoPayload = null;
  let clockUndoReceipt = null;
  let clockRetryVerified = false;
  await context.route("http://localhost:5217/api/commands", async (route) => {
    if (route.request().method() !== "POST") return route.continue();
    const command = route.request().postDataJSON();
    if (command.action === "complete" && skewNextCompletion) {
      skewNextCompletion = false;
      const committed = await route.fetch();
      expect(committed.ok()).toBe(true);
      const receipt = await committed.json();
      clockRecordedAt = receipt.occurrences.find(
        (item) => item.completion?.id === command.operationId,
      ).completion.recordedAt;
      expect(Date.parse(clockRecordedAt)).toBeGreaterThan(Date.now());
      // Restore the physical clock before production drain requests its next anchor.
      // The command proof was created and persisted by the real session, not rewritten here.
      await page.evaluate(() => {
        Object.defineProperty(performance, "now", {
          configurable: true,
          value: window.pocketQuestsOriginalNow,
        });
        delete window.pocketQuestsOriginalNow;
      });
      return route.fulfill({ response: committed });
    }
    if (command.action === "undo" && clockRecordedAt && !clockRetryVerified) {
      if (!clockUndoPayload) {
        clockUndoPayload = command;
        expect(
          Date.parse(clockAnchor.serverUtc) +
            command.recordedTime.elapsedMilliseconds,
        ).toBeLessThan(Date.parse(clockRecordedAt));
        const committed = await route.fetch();
        expect(committed.ok()).toBe(true);
        clockUndoReceipt = await committed.json();
        expect(clockUndoReceipt.overallXp).toBe(280);
        return route.abort("failed");
      }
      expect(command).toEqual(clockUndoPayload);
      const replay = await route.fetch();
      expect(replay.ok()).toBe(true);
      expect(await replay.json()).toEqual(clockUndoReceipt);
      clockRetryVerified = true;
      return route.fulfill({ response: replay });
    }
    if (command.action === "save-definition" && rejectSave) {
      rejectSave = false;
      return route.fulfill({
        status: 503,
        headers: cors,
        json: { detail: "Temporary save failure for retry verification." },
      });
    }
    if (command.action === "complete" && loseCompletionResponse) {
      loseCompletionResponse = false;
      const committed = await route.fetch();
      expect(committed.ok()).toBe(true);
      return route.abort("failed"); // SQL committed, but the device receives no receipt.
    }
    return route.continue();
  });
  diagnostics.checkpoint("save-retry");
  await page
    .getByRole("button", { name: "Save custom quest", exact: true })
    .click();
  await expect(
    visibleText(
      "The request could not be completed (503). Try again when connected.",
      {
        exact: true,
      },
    ),
  ).toBeVisible();
  await expect(page.getByLabel("Quest title", { exact: true })).toHaveValue(
    "Browser verified custom quest",
  );
  await expect(
    page.getByLabel("Completion criterion", { exact: true }),
  ).toHaveValue("I have delivered the working result");
  await page.getByRole("button", { name: "Try again", exact: true }).click();
  await expect(visibleText(/Saved revision 1/)).toBeVisible();
  await page
    .getByRole("button", { name: "Back to available quests", exact: true })
    .click();
  await page
    .getByRole("button", {
      name: "View quest & rewards: Browser verified custom quest",
      exact: true,
    })
    .click();
  await expect(
    page.getByRole("heading", { name: "Complete when", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Completion rewards", exact: true }),
  ).toBeVisible();
  await expect(
    visibleText("I have delivered the working result", { exact: true }),
  ).toBeVisible();
  for (const width of [320, 430]) {
    await page.setViewportSize({ width, height: 844 });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({
    path: path.join(artifacts, "available-quest-details.png"),
  });
  await page
    .getByRole("button", { name: "Back to available quests", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Complete when", exact: true }),
  ).toBeHidden();
  await page
    .getByRole("heading", {
      name: "Browser verified custom quest",
      exact: true,
    })
    .evaluate((element) =>
      element.parentElement.scrollIntoView({ block: "start" }),
    );
  await page.screenshot({ path: path.join(artifacts, "available-quests.png") });
  await page
    .getByRole("button", {
      name: "Start: Browser verified custom quest",
      exact: true,
    })
    .click();
  await page.getByRole("tab", { name: "Active", exact: true }).click();
  const activeQuestLink = page.getByRole("link", {
    name: "View quest & rewards: Browser verified custom quest",
    exact: true,
  });
  await expect(activeQuestLink).toBeVisible();
  await expect(
    visibleText("I have delivered the working result", { exact: true }),
  ).toHaveCount(0);
  await activeQuestLink.evaluate((element) =>
    element.parentElement.scrollIntoView({ block: "start" }),
  );
  for (const width of [320, 430]) {
    await page.setViewportSize({ width, height: 844 });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: path.join(artifacts, "active-quests.png") });
  await page.getByRole("link", { name: /^View quest & rewards:/ }).click();
  await expect(
    page.getByRole("heading", { name: "Complete when", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", {
      name: "Complete: Browser verified custom quest",
      exact: true,
    }),
  ).toBeEnabled();
  await expect(
    page.getByRole("heading", { name: "Completion rewards", exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: path.join(artifacts, "active-quest-details.png"),
  });
  diagnostics.checkpoint("definition-edit");
  await page
    .getByRole("button", {
      name: "Edit: Browser verified custom quest",
      exact: true,
    })
    .click();
  await page
    .getByLabel("Completion criterion", { exact: true })
    .fill("I have delivered and reviewed the working result");
  await page
    .getByRole("button", { name: "Save custom quest", exact: true })
    .click();
  await expect(visibleText(/Saved revision 2/)).toBeVisible();
  await page
    .getByRole("button", { name: "Back to quest details", exact: true })
    .click();
  await expect(
    visibleText("I have delivered and reviewed the working result", {
      exact: true,
    }),
  ).toBeVisible();
  await page
    .getByRole("button", {
      name: "Set recurrence: Browser verified custom quest",
      exact: true,
    })
    .click();
  await expect(
    visibleText("Repeat: Browser verified custom quest", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Back to quest details", exact: true })
    .click();
  await page
    .getByRole("button", {
      name: "Archive definition: Browser verified custom quest",
      exact: true,
    })
    .click();
  await expect(
    page.getByRole("button", {
      name: "Edit: Browser verified custom quest",
      exact: true,
    }),
  ).toHaveCount(0);
  console.log(
    "Verified active definition editing, recurrence access and archival without losing its occurrence.",
  );
  diagnostics.checkpoint("completion-response-loss");
  await page
    .getByRole("button", {
      name: "Complete: Browser verified custom quest",
      exact: true,
    })
    .click({ clickCount: 2 });
  await expect(visibleText(/You’re offline.*saved progress/)).toBeVisible();
  await expect(visibleText("Quest complete!", { exact: true })).toHaveCount(0);
  const committed = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  expect(committed.overallXp).toBe(280);
  const synchronized = page.waitForResponse(
    (response) =>
      response.url() === "http://localhost:5217/api/sync-anchor" &&
      response.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Try saving again", exact: true })
    .click();
  expect((await synchronized).ok()).toBe(true);
  await expect(visibleText("Quest complete!", { exact: true })).toBeVisible();
  await expect(visibleText("Level up!", { exact: true })).toBeVisible();
  await expect(
    visibleText("Overall · Level 1 → 2", { exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: path.join(artifacts, "completion-celebration.png"),
    fullPage: false,
  });
  await page
    .getByRole("button", { name: "Keep adventuring", exact: true })
    .click();
  await page.getByRole("link", { name: "Back to Home", exact: true }).click();
  await expect(visibleText("Your character", { exact: true })).toBeVisible();
  await expect(visibleText("280 overall XP", { exact: true })).toBeVisible();
  console.log("Verified completion response-loss retry and character totals.");
  diagnostics.checkpoint("character");
  await expect(
    page.getByRole("progressbar", { name: / level progress$/ }),
  ).toHaveCount(1);
  for (const [title, items] of [
    ["Categories", committed.categories],
    ["Attributes", committed.attributes],
    ["Skills", committed.skills],
  ]) {
    await expect(
      page.getByRole("heading", { name: title, exact: true }),
    ).toBeVisible();
    const section = page.getByRole("button", {
      name: `${title}, ${items.length}`,
      exact: true,
    });
    await expect(section).toHaveAttribute("aria-expanded", "false");
    await section.focus();
    await section.press(title === "Attributes" ? "Space" : "Enter");
    await expect(section).toHaveAttribute("aria-expanded", "true");
  }
  await expect(
    page.getByRole("progressbar", { name: / level progress$/ }),
  ).toHaveCount(
    1 +
      committed.categories.length +
      committed.attributes.length +
      committed.skills.length,
  );
  await page
    .getByRole("button", { name: "View rank progress", exact: true })
    .click();
  const rankBars = page.getByRole("progressbar", {
    name: /^(Qualifying categories|Combined category XP)$/,
  });
  await expect(rankBars).toHaveCount(2);
  await expect(
    page.getByRole("progressbar", {
      name: "Overall level progress",
      exact: true,
    }),
  ).toHaveAttribute("aria-valuetext", "180/300 XP toward level 3");
  await page
    .getByRole("button", { name: "See the rank ladder", exact: true })
    .click();
  await expect(
    visibleText("Rank F · Current rank", { exact: true }),
  ).toBeVisible();
  await expect(
    visibleText(
      "10 categories at 9000 XP each, and 150000 combined category XP",
      { exact: true },
    ),
  ).toBeVisible();
  await page.screenshot({ path: path.join(artifacts, "rank-ladder.png") });
  await page
    .getByRole("button", { name: "Hide rank ladder", exact: true })
    .click();
  await page
    .getByRole("button", { name: "See category requirements", exact: true })
    .click();
  await expect(
    visibleText("280 XP · Floor met", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Hide category requirements", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Hide rank progress", exact: true })
    .click();
  await expect(page.getByRole("button", { name: /^Complete:/ })).toHaveCount(0);
  await expect(
    visibleText("Work & Purpose: 1 day in this streak. Done today.", {
      exact: true,
    }),
  ).toBeVisible();
  for (const title of ["Categories", "Attributes", "Skills"]) {
    const section = page.getByRole("button", {
      name: new RegExp(`^${title}, `),
    });
    await section.click();
    await expect(section).toHaveAttribute("aria-expanded", "false");
  }
  await page
    .getByRole("heading", { name: "Your character", exact: true })
    .scrollIntoViewIfNeeded();
  await page.setViewportSize({ width: 320, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({ path: path.join(artifacts, "character-narrow.png") });
  await page.setViewportSize({ width: 390, height: 844 });
  const portrait = await page
    .getByRole("img", {
      name: "Illustrated traveler character portrait",
      exact: true,
    })
    // RN Web also emits an inner accessibility img. Measure the visible wrapper.
    .and(page.locator('[role="img"]'))
    .boundingBox();
  const overallBar = await page
    .getByRole("progressbar", { name: "Overall level progress", exact: true })
    .boundingBox();
  expect(portrait).not.toBeNull();
  expect(overallBar).not.toBeNull();
  expect(portrait.x + portrait.width).toBeLessThanOrEqual(overallBar.x);
  await page
    .getByRole("heading", { name: "Categories", exact: true })
    .evaluate((element) => element.scrollIntoView({ block: "start" }));
  await page.screenshot({
    path: path.join(artifacts, "character-home.png"),
    fullPage: false,
  });
  diagnostics.checkpoint("export-timer");
  // The overlaid gear must remain reachable above the decorative heading art.
  await page
    .getByRole("link", { name: "Settings and profile", exact: true })
    .click();
  await expect(
    visibleText("280 base XP earned", { exact: true }),
  ).toBeVisible();
  const response = await context.request.get(
    "http://localhost:5217/api/state",
    { headers: { Authorization: `Bearer ${fixture.token}` } },
  );
  const state = await response.json();
  expect(state.overallXp).toBe(280);
  expect(state.definitions).toHaveLength(1);
  expect(state.skills.find((s) => s.id === "s07").xp).toBe(12285);
  expect(state.occurrences[0].status).toBe("Completed");
  expect(
    state.ledger.filter((entry) => entry.track === "Overall"),
  ).toHaveLength(1);
  const downloadEvent = page.waitForEvent("download");
  await page
    .getByRole("button", { name: "Download full JSON export", exact: true })
    .click();
  const download = await downloadEvent;
  const exported = JSON.parse(await fs.readFile(await download.path(), "utf8"));
  expect(exported.state.overallXp).toBe(280);
  expect(exported.completions[0].snapshot.title).toBe(
    "Browser verified custom quest",
  );
  await page.getByRole("tab", { name: "Quests", exact: true }).click();
  await page.getByRole("tab", { name: "Completed", exact: true }).click();
  await expect(
    page.getByRole("link", {
      name: "View quest & rewards: Browser verified custom quest",
      exact: true,
    }),
  ).toBeVisible();
  await page
    .getByRole("heading", {
      name: "Browser verified custom quest",
      exact: true,
    })
    .evaluate((element) =>
      element.parentElement.scrollIntoView({ block: "start" }),
    );
  await page.screenshot({ path: path.join(artifacts, "completed-quests.png") });
  await expect(
    visibleText("Browser verified custom quest", { exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: /^Complete:/ })).toHaveCount(0);
  await page.getByRole("tab", { name: "Available", exact: true }).click();
  await page
    .getByRole("button", { name: "Start: Enjoy some downtime", exact: true })
    .click();
  await page.getByRole("tab", { name: "Active", exact: true }).click();
  await page.getByRole("link", { name: /^View quest & rewards:/ }).click();
  await page
    .getByRole("button", { name: "Do Now with an optional timer", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Start focus timer", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Pause focus timer", exact: true }),
  ).toBeVisible();
  const duringTimer = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  expect(duringTimer.overallXp).toBe(280);
  console.log("Verified completed list, export, and timer without XP award.");
  await page.getByRole("link", { name: "Back to Home", exact: true }).click();
  await expect(visibleText("Your character", { exact: true })).toBeVisible();
  await expect(page).toHaveURL("http://localhost:8081/");
  await page.goBack();
  await expect(
    page.getByRole("button", { name: "Resume focus timer", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Close focus timer", exact: true })
    .click();
  diagnostics.checkpoint("offline-undo");
  await context.setOffline(true);
  await page
    .getByRole("button", { name: "Complete: Enjoy some downtime", exact: true })
    .click();
  await expect(visibleText(/You’re offline.*saved progress/)).toBeVisible();
  await expect(visibleText("Quest complete!", { exact: true })).toHaveCount(0);
  // The web adapter intentionally uses memory-only storage. Native process-death
  // persistence must be tested on a native development build, not inferred here.
  await expect(
    visibleText(/Rewards will be confirmed when synchronized/),
  ).toBeVisible();
  await context.setOffline(false);
  // The adapter's existing online listener automatically resumes the queued sync.
  await expect(visibleText("Quest complete!", { exact: true })).toBeVisible({
    timeout: 30000,
  });
  await page
    .getByRole("button", { name: "Undo recent completion", exact: true })
    .click();
  await expect(
    page.getByRole("button", {
      name: "Complete: Enjoy some downtime",
      exact: true,
    }),
  ).toBeEnabled();
  console.log("Verified offline completion and matching Undo.");
  diagnostics.checkpoint("clock-undo-retry");
  // Reproduce the reported clock decrease across normal anchor refresh in the actual client.
  // The server receives an untouched, durably queued proof with a bounded clock lead.
  await page.evaluate(() => {
    window.pocketQuestsOriginalNow = performance.now.bind(performance);
    Object.defineProperty(performance, "now", {
      configurable: true,
      value: () => window.pocketQuestsOriginalNow() + 3500,
    });
  });
  skewNextCompletion = true;
  const refreshedAfterSkew = page.waitForResponse(
    (response) =>
      response.url() === "http://localhost:5217/api/sync-anchor" &&
      response.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Complete: Enjoy some downtime", exact: true })
    .click();
  const clockAnchorResponse = await refreshedAfterSkew;
  expect(clockAnchorResponse.ok()).toBe(true);
  clockAnchor = await clockAnchorResponse.json();
  expect(Date.parse(clockAnchor.recordedTimeFloor)).toBeGreaterThanOrEqual(
    Date.parse(clockRecordedAt),
  );
  await expect(visibleText("Quest complete!", { exact: true })).toBeVisible();
  await page
    .getByRole("button", { name: "Undo recent completion", exact: true })
    .click();
  await expect(
    page.getByRole("button", {
      name: "Try saving again",
      exact: true,
    }),
  ).toBeEnabled();
  await page
    .getByRole("button", { name: "Try saving again", exact: true })
    .click();
  const completeAgain = page.getByRole("button", {
    name: "Complete: Enjoy some downtime",
    exact: true,
  });
  await expect(completeAgain).toBeEnabled();
  expect(clockRetryVerified).toBe(true);
  // A subsequent real command must drain normally; no discard or proof mutation is allowed.
  await completeAgain.click();
  await expect(visibleText("Quest complete!", { exact: true })).toBeVisible();
  await page
    .getByRole("button", { name: "Undo recent completion", exact: true })
    .click();
  await expect(completeAgain).toBeEnabled();
  console.log(
    "Verified future-skew completion, refreshed anchor, immediate Undo, lost-response immutable retry and an unblocked next command.",
  );
  const synced = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  expect(synced.overallXp).toBe(280);
  expect(
    synced.occurrences.filter((o) => o.status === "Completed"),
  ).toHaveLength(1);
  diagnostics.checkpoint("fresh-session");
  await page.reload();
  // Fresh adapter session, same isolated test account: verifies server persistence.
  await expect(signIn).toBeEnabled();
  await signIn.click();
  await expect(visibleText("280 overall XP", { exact: true })).toBeVisible();
  await page.getByRole("tab", { name: "Quests", exact: true }).click();
  await page.getByRole("tab", { name: "Active", exact: true }).click();
  await page.getByRole("link", { name: /^View quest & rewards:/ }).click();
  await expect(
    page.getByRole("button", {
      name: "Complete: Enjoy some downtime",
      exact: true,
    }),
  ).toBeEnabled();
  await page.getByRole("link", { name: "Back to Home", exact: true }).click();
  diagnostics.checkpoint("calendar-planning");
  // Calendar planning uses the same saved account and real SQL-backed commands.
  await page.getByRole("tab", { name: "Calendar", exact: true }).click();
  await page.getByRole("tab", { name: "7 days", exact: true }).click();
  await expect(
    page.getByRole("tab", { name: "7 days", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  const plannedDate = new Date(
    Date.parse(synced.today + "T00:00:00Z") + 3 * 86400000,
  )
    .toISOString()
    .slice(0, 10);
  await page.getByLabel("Calendar date YYYY-MM-DD").fill("2026-02-30");
  await page.getByRole("button", { name: "Go to date", exact: true }).click();
  await expect(visibleText(/Enter a real date/)).toBeVisible();
  await page.getByLabel("Calendar date YYYY-MM-DD").fill(plannedDate);
  await page.getByRole("button", { name: "Go to date", exact: true }).click();
  await page
    .getByRole("button", {
      name: `Create a quest for ${plannedDate}`,
      exact: true,
    })
    .click();
  await page
    .getByLabel("Quest title", { exact: true })
    .fill("Calendar planned quest");
  await page
    .getByLabel("Completion criterion", { exact: true })
    .fill("Finish the planned calendar action");
  await page
    .getByRole("button", { name: "Save custom quest", exact: true })
    .click();
  await expect(visibleText(/Saved revision 1/)).toBeVisible();
  await page
    .getByRole("button", { name: "Back to available quests", exact: true })
    .click();
  await page
    .getByRole("button", {
      name: "View quest & rewards: Calendar planned quest",
      exact: true,
    })
    .click();
  await expect(page.getByLabel("Due date in YYYY-MM-DD format")).toHaveValue(
    plannedDate,
  );
  await page.getByLabel("Optional planned time HH:mm").fill("09:30");
  const startCalendarQuest = page.getByRole("button", {
    name: "Start: Calendar planned quest",
    exact: true,
  });
  await expect(startCalendarQuest).toBeEnabled();
  await startCalendarQuest.click();
  await expect(startCalendarQuest).toBeHidden();
  await page
    .getByRole("button", { name: "Back to available quests", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Back to calendar", exact: true })
    .click();
  await expect(
    visibleText("Calendar planned quest", { exact: true }),
  ).toBeVisible();
  let plannedState;
  await expect
    .poll(async () => {
      plannedState = await (
        await context.request.get("http://localhost:5217/api/state", {
          headers: { Authorization: `Bearer ${fixture.token}` },
        })
      ).json();
      return plannedState.occurrences.some(
        (row) => row.occurrence.quest.title === "Calendar planned quest",
      );
    })
    .toBe(true);
  const calendarQuest = plannedState.occurrences.find(
    (row) => row.occurrence.quest.title === "Calendar planned quest",
  );
  expect(calendarQuest.occurrence.dueDate).toBe(plannedDate);
  expect(calendarQuest.occurrence.plannedTime).toBe("09:30");
  expect(calendarQuest.status).toBe("Active");
  expect(plannedState.overallXp).toBe(synced.overallXp);
  await page.screenshot({
    path: path.join(artifacts, "calendar-seven-days.png"),
  });
  await page.getByRole("tab", { name: "Day", exact: true }).click();
  await page.screenshot({ path: path.join(artifacts, "calendar-day.png") });
  await page.getByRole("tab", { name: "Quests", exact: true }).click();
  await page.getByRole("tab", { name: "Active", exact: true }).click();
  await expect(
    visibleText(`Today · ${plannedState.today}`, { exact: true }),
  ).toBeVisible();
  await expect(visibleText("Unscheduled", { exact: true })).toBeVisible();
  await expect(visibleText("Upcoming", { exact: true })).toBeVisible();
  await expect(
    visibleText("Calendar planned quest", { exact: true }),
  ).toBeVisible();
  await page.screenshot({ path: path.join(artifacts, "active-planning.png") });
  await page
    .getByRole("link", { name: /^View quest & rewards:/ })
    .and(page.locator(`a[href="/quest/${calendarQuest.occurrence.id}"]`))
    .click();
  diagnostics.checkpoint("plan-edit");
  await expect(
    page.getByRole("button", { name: "Save plan", exact: true }),
  ).toBeDisabled();
  await page
    .getByRole("textbox", { name: "Quest due date YYYY-MM-DD", exact: true })
    .fill("2026-02-30");
  await expect(
    visibleText("Enter a real due date in YYYY-MM-DD format.", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Save plan", exact: true }),
  ).toBeDisabled();
  await page
    .getByRole("button", { name: "Leave this quest unscheduled", exact: true })
    .click();
  await expect(
    page.getByRole("textbox", {
      name: "Quest planned time HH:mm",
      exact: true,
    }),
  ).toHaveValue("");
  await page.getByRole("button", { name: "Save plan", exact: true }).click();
  async function waitForSavedPlan(dueDate, plannedTime) {
    await expect
      .poll(async () => {
        const account = await (
          await context.request.get("http://localhost:5217/api/state", {
            headers: { Authorization: `Bearer ${fixture.token}` },
          })
        ).json();
        const occurrence = account.occurrences.find(
          (row) => row.occurrence.id === calendarQuest.occurrence.id,
        )?.occurrence;
        return {
          dueDate: occurrence?.dueDate,
          plannedTime: occurrence?.plannedTime,
          xp: account.overallXp,
        };
      })
      .toEqual({ dueDate, plannedTime, xp: synced.overallXp });
  }
  await waitForSavedPlan(null, null);
  await page
    .getByRole("button", { name: "Plan for today", exact: true })
    .click();
  await page
    .getByRole("textbox", { name: "Quest planned time HH:mm", exact: true })
    .fill("08:15");
  await page.getByRole("button", { name: "Save plan", exact: true }).click();
  await waitForSavedPlan(plannedState.today, "08:15");
  await expect(
    page.getByRole("button", { name: "Save plan", exact: true }),
  ).toBeDisabled();
  await page.screenshot({
    path: path.join(artifacts, "quest-plan-editor.png"),
  });
  await page.getByRole("link", { name: "Back to Home", exact: true }).click();
  await page.getByRole("tab", { name: "Home", exact: true }).click();
  console.log(
    "Calendar day/week navigation, invalid-date recovery and selected-date custom creation/acceptance passed against real SQL; planning awarded no XP.",
  );
  console.log(
    "Quest plan editing rejected invalid dates, persisted unscheduled date/time removal and today's timed plan, and preserved XP.",
  );
  diagnostics.checkpoint("viewports");
  for (const [width, height] of [
    [320, 720],
    [430, 932],
  ]) {
    await page.setViewportSize({ width, height });
    await expect(visibleText("280 overall XP", { exact: true })).toBeVisible();
    // Bundled images have intrinsic dimensions; artwork must fit its dynamic container.
    for (const name of ["heading", "rank"]) {
      const background = page
        .getByTestId(`journal-background-${name}`)
        .filter({ visible: true });
      const bounds = await background.evaluate((element) => {
        const image = element.getBoundingClientRect();
        const container = element.parentElement.getBoundingClientRect();
        return {
          widthDifference: Math.abs(image.width - container.width),
          heightDifference: Math.abs(image.height - container.height),
        };
      });
      expect(bounds.widthDifference).toBeLessThanOrEqual(1);
      expect(bounds.heightDifference).toBeLessThanOrEqual(1);
    }
    await page.screenshot({
      path: path.join(artifacts, `home-${width}.png`),
    });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
  }
  diagnostics.checkpoint("page-errors");
  expect(failures).toEqual([]);
  console.log(
    "Web adapter / real API and isolated SQL journey passed: test OIDC/PKCE, save failure and retry, double tap with lost completion response, exact level-up, character sheet, export, completed list, optional timer and route pause, offline completion, reconnect, matching Undo, fresh-session SQL persistence. No real-provider or native-device claim.",
  );
} catch (error) {
  diagnostics.fail(error);
  await page.screenshot({
    path: path.join(artifacts, "browser-failure.png"),
    fullPage: false,
  });
  await fs.writeFile(
    path.join(artifacts, "browser-failure.html"),
    await page.content(),
  );
  console.error((await page.locator("body").innerText()).slice(0, 5000));
  throw error;
} finally {
  await browser.close();
  await fs.writeFile(path.join(fixtureDirectory, "done"), "done");
}

diagnostics.checkpoint("complete");
