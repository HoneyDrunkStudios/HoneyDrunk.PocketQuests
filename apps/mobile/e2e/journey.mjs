import { chromium, expect } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import { createDiagnostics } from "./safe-diagnostics.mjs";
import { activeRole, activeRouteSelector } from "./active-route.mjs";
import { verifyNextFeatures } from "./next-features.mjs";
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
const activeRoute = page.locator(activeRouteSelector);
const visibleRole = (role, options) =>
  activeRole(page, role, options).and(activeRoute).filter({ visible: true });
const visibleText = (text, options) =>
  page
    .getByText(text, options)
    .and(activeRoute)
    .filter({ visible: true });
const homeXp = visibleText("See your progress · 280 overall XP", {
  exact: true,
});

function americanDate(isoDate) {
  const [year, month, day] = isoDate.split("-");
  return `${month}/${day}/${year}`;
}

async function verifyTravelerScene() {
  let checkpoint = "character-markup";
  try {
    await expect
      .poll(async () => {
        checkpoint = await page.evaluate(() => {
          // Expo retains previous Home screens behind the active route.
          const sheet = Array.from(
            document.querySelectorAll('[data-testid="character-sheet"]'),
          ).find(
            (element) =>
              !element.closest('[aria-hidden="true"]') &&
              element.getClientRects().length > 0 &&
              getComputedStyle(element).visibility === "visible",
          );
          const scene = sheet?.querySelector('[data-testid="traveler-scene"]');
          const traveler = scene?.querySelector('[data-testid="traveler-avatar"]');
          const progress = sheet?.querySelector(
            '[data-testid="home-level-progress"]',
          );
          const backdrop = scene?.querySelector(
            '[data-testid="traveler-backdrop"]',
          );
          if (!scene || !traveler || !progress || !backdrop)
            return "character-markup";
          const sceneBox = scene.getBoundingClientRect();
          const avatarBox = traveler.getBoundingClientRect();
          const progressBox = progress.getBoundingClientRect();
          const backdropBox = backdrop.getBoundingClientRect();
          if (![sceneBox, avatarBox, progressBox, backdropBox].every(
            ({ width, height }) => width > 0 && height > 0,
          ))
            return "character-markup";
          const sceneCenter = sceneBox.left + sceneBox.width / 2;
          const avatarCenter = avatarBox.left + avatarBox.width / 2;
          if (Math.abs(avatarCenter - sceneCenter) > 1) return "character-center";
          if (progressBox.top < sceneBox.bottom) return "character-progress";
          if (
            backdropBox.left > sceneBox.left + 1 ||
            backdropBox.right < sceneBox.right - 1 ||
            backdropBox.top > sceneBox.top + 1 ||
            backdropBox.bottom < sceneBox.bottom - 1
          )
            return "character-backdrop";
          return "ready";
        });
        return checkpoint;
      })
      .toBe("ready");
  } catch (error) {
    diagnostics.checkpoint(checkpoint);
    throw error;
  }
}
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
// External OIDC and public map responses are test fixtures. Identity validates
// the signed token; product requests reach the actual API and isolated SQL.
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
// Search is view state: every result comes from the loaded occurrence snapshots.
async function verifyHistorySearch(view) {
  const readState = async () =>
    (
      await context.request.get("http://localhost:5217/api/state", {
        headers: { Authorization: `Bearer ${fixture.token}` },
      })
    ).json();
  // The previous Start may still be committing after its local projection appears.
  await expect
    .poll(
      async () =>
        (await readState()).occurrences.some(
          (item) =>
            item.status === view &&
            item.occurrence.quest.title === "Browser verified custom quest",
        ),
      { timeout: 15000 },
    )
    .toBe(true);
  const before = await readState();
  const writes = [];
  const observe = (request) => {
    if (
      request.method() === "POST" &&
      request.url().startsWith("http://localhost:5217/api/commands")
    )
      writes.push(request.url());
  };
  page.on("request", observe);
  try {
    const search = activeRole(page, "textbox", {
      name: `Search ${view.toLowerCase()} quests`,
      exact: true,
    });
    const link = activeRole(page, "link", {
      name: "Quest details: Browser verified custom quest",
      exact: true,
    });
    await search.fill("browser delivered");
    await expect(link).toBeVisible();
    await activeRole(page, "button", { name: "Show quest filters", exact: true })
      .click();
    await activeRole(page, "radio", { name: "Work & Purpose", exact: true })
      .click();
    await activeRole(page, "radio", { name: "Medium", exact: true }).click();
    await expect(link).toBeVisible();
    await activeRole(page, "radio", { name: "Small", exact: true }).click();
    await expect(link).toHaveCount(0);
    await expect(
      visibleText("No quests match your search and filters.", { exact: true }),
    ).toBeVisible();
    await activeRole(page, "button", { name: "Clear search and filters", exact: true })
      .click();
    await expect(link).toBeVisible();
    await expect(search).toHaveValue("");
    await activeRole(page, "button", { name: "Hide quest filters", exact: true })
      .click();
    const after = await readState();
    expect(writes).toHaveLength(0);
    expect(after.overallXp).toBe(before.overallXp);
    expect(after.occurrences).toEqual(before.occurrences);
    expect(after.ledger).toEqual(before.ledger);
  } finally {
    page.off("request", observe);
  }
}

try {
  diagnostics.checkpoint("sign-in");
  await page.goto("http://localhost:8081");
  const signIn = activeRole(page, "button", {
    name: "Sign in or create an account",
  });
  await expect(signIn).toBeEnabled({ timeout: 90000 });
  await signIn.click();
  await expect(
    visibleText("Create your character", { exact: true }),
  ).toBeVisible({ timeout: 30000 });
  diagnostics.checkpoint("onboarding");
  await activeRole(page, "textbox", { name: "Character name", exact: true })
    .fill("Browser adventurer");
  await activeRole(page, "button", { name: "Save name and continue", exact: true })
    .click();
  await activeRole(page, "button", { name: "More editing options", exact: true })
    .click();
  await activeRole(page, "tab", { name: "Body", exact: true }).click();
  await activeRole(page, "radio", { name: /^(Selected: )?Face: Oval$/ }).click();
  await activeRole(page, "radio", { name: "Skin tone: Tone 6", exact: true })
    .click();
  await activeRole(page, "tab", { name: "Hair", exact: true }).click();
  await activeRole(page, "radio", { name: "Hair color: Silver", exact: true })
    .click();
  await activeRole(page, "tab", { name: "Clothes", exact: true }).click();
  await activeRole(page, "button", { name: /^(Selected preview: )?Field shirt$/ })
    .click();
  await activeRole(page, "button", { name: "Continue", exact: true })
    .click();
  await activeRole(page, "checkbox", { name: "Work & Purpose", exact: true })
    .click();
  await expect(
    activeRole(page, "checkbox", { name: "Work & Purpose", exact: true }),
  ).toBeChecked();
  await activeRole(page, "button", { name: "Continue", exact: true }).click();
  await activeRole(page, "radio", { name: "Programming: Expert", exact: true })
    .click();
  await activeRole(page, "button", { name: "Continue with these skills", exact: true })
    .click();
  diagnostics.checkpoint("onboarding-save");
  await activeRole(page, "button", { name: "Show me around", exact: true }).click();
  const tour = page.getByTestId("journal-tour-panel");
  for (const [route, title] of [
    ["/", "Welcome to your journal"],
    ["/board", "Find something that fits"],
    ["/calendar", "Give it a little space"],
    ["/room", "Make yourself at home"],
  ]) {
    await expect(page).toHaveURL((url) => url.pathname === route);
    await expect(
      tour.getByRole("heading", { name: title, exact: true }),
    ).toBeVisible();
    await tour.getByRole("button", { name: "Next", exact: true }).click();
  }
  diagnostics.checkpoint("tour-settings");
  await expect(page).toHaveURL((url) => url.pathname === "/profile");
  await expect(
    tour.getByRole("heading", { name: "Small steps add up", exact: true }),
  ).toBeVisible();
  await tour.getByRole("button", { name: "Open settings", exact: true }).click();
  await expect(page).toHaveURL((url) => url.pathname === "/settings");
  diagnostics.checkpoint("tour-finish");
  await expect(
    tour.getByRole("heading", { name: "Keep it your own", exact: true }),
  ).toBeVisible();
  await tour.getByRole("button", { name: "Choose a quest", exact: true }).click();
  await expect(page).toHaveURL(/\/board(?:\?view=Available)?$/);
  await expect(
    activeRole(page, "heading", { name: "Pick your next quest", exact: true }),
  ).toBeVisible();
  const initialCharacter = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  expect(initialCharacter.profile.characterName).toBe("Browser adventurer");
  expect(initialCharacter.profile.portraitId).toBe("portrait-1");
  expect(initialCharacter.profile.avatarCode).toBe("v2:5:3:0:4:0:6:0");
  expect(initialCharacter.skills.find((skill) => skill.id === "s07").xp).toBe(
    12005,
  );
  diagnostics.checkpoint("custom-quest");
  await activeRole(page, "button", { name: "Create a custom quest", exact: true })
    .click();
  await page
    .getByLabel("Quest title", { exact: true })
    .fill("Browser verified custom quest");
  await page
    .getByLabel("Completion criterion", { exact: true })
    .fill("I have delivered the working result");
  await activeRole(page, "button", { name: "Work & Purpose", exact: true })
    .click();
  await activeRole(page, "button", { name: "Add Programming", exact: true })
    .click();
  await activeRole(page, "button", { name: "Rank A", exact: true }).click();
  await activeRole(page, "button", { name: "Medium", exact: true }).click();
  let rejectSave = true;
  let loseCompletionResponse = true;
  let completionResponseAborted = false;
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
      await route.abort("failed"); // SQL committed, but the device receives no receipt.
      completionResponseAborted = true;
      return;
    }
    return route.continue();
  });
  diagnostics.checkpoint("save-retry");
  await activeRole(page, "button", { name: "Save custom quest", exact: true })
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
  await activeRole(page, "button", { name: "Try again", exact: true }).click();
  await expect(visibleText(/Saved revision 1/)).toBeVisible();
  await activeRole(page, "button", { name: "Back to available quests", exact: true })
    .click();
  await activeRole(page, "button", {
      name: "Quest details: Browser verified custom quest",
      exact: true,
    })
    .click();
  await expect(
    activeRole(page, "heading", { name: "Complete when", exact: true }),
  ).toBeVisible();
  await expect(
    activeRole(page, "heading", { name: "Completion rewards", exact: true }),
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
  await activeRole(page, "button", { name: "Back to available quests", exact: true })
    .click();
  await expect(
    activeRole(page, "heading", { name: "Complete when", exact: true }),
  ).toBeHidden();
  await activeRole(page, "heading", {
      name: "Browser verified custom quest",
      exact: true,
    })
    .evaluate((element) =>
      element.parentElement.scrollIntoView({ block: "start" }),
    );
  await page.screenshot({ path: path.join(artifacts, "available-quests.png") });
  await activeRole(page, "button", {
      name: "Quest details: Browser verified custom quest",
      exact: true,
    })
    .click();
  await activeRole(page, "button", {
      name: "Start: Browser verified custom quest",
      exact: true,
    })
    .click();
  await expect(
    activeRole(page, "button", { name: "Open active quest", exact: true }),
  ).toBeVisible();
  await activeRole(page, "button", { name: "Back to available quests", exact: true })
    .click();
  await activeRole(page, "tab", { name: "Active", exact: true }).click();
  const activeQuestLink = activeRole(page, "link", {
    name: "Quest details: Browser verified custom quest",
    exact: true,
  });
  await expect(activeQuestLink).toBeVisible();
  await verifyHistorySearch("Active");
  await expect(
    visibleText("I have delivered the working result", { exact: true }),
  ).toHaveCount(1);
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
  await activeRole(page, "link", { name: /^Quest details:/ }).click();
  await expect(
    activeRole(page, "heading", { name: "Complete when", exact: true }),
  ).toBeVisible();
  await expect(
    activeRole(page, "button", {
      name: "Complete: Browser verified custom quest",
      exact: true,
    }),
  ).toBeEnabled();
  await expect(
    activeRole(page, "heading", { name: "Completion rewards", exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: path.join(artifacts, "active-quest-details.png"),
  });
  diagnostics.checkpoint("definition-edit");
  await activeRole(page, "button", {
      name: "Edit: Browser verified custom quest",
      exact: true,
    })
    .click();
  await page
    .getByLabel("Completion criterion", { exact: true })
    .fill("I have delivered and reviewed the working result");
  await activeRole(page, "button", { name: "Save custom quest", exact: true })
    .click();
  await expect(visibleText(/Saved revision 2/)).toBeVisible();
  await activeRole(page, "button", { name: "Back to quest details", exact: true })
    .click();
  await expect(
    visibleText("I have delivered and reviewed the working result", {
      exact: true,
    }),
  ).toBeVisible();
  await activeRole(page, "button", {
      name: "Set recurrence: Browser verified custom quest",
      exact: true,
    })
    .click();
  await expect(
    visibleText("Repeat: Browser verified custom quest", { exact: true }),
  ).toBeVisible();
  await activeRole(page, "button", { name: "Back to quest details", exact: true })
    .click();
  await activeRole(page, "button", {
      name: "Archive definition: Browser verified custom quest",
      exact: true,
    })
    .click();
  await expect(
    activeRole(page, "button", {
      name: "Edit: Browser verified custom quest",
      exact: true,
    }),
  ).toHaveCount(0);
  console.log(
    "Verified active definition editing, recurrence access and archival without losing its occurrence.",
  );
  diagnostics.checkpoint("completion-response-loss");
  await activeRole(page, "button", {
      name: "Complete: Browser verified custom quest",
      exact: true,
    })
    .click({ clickCount: 2 });
  // The response must be committed and dropped before the UI can report recovery.
  // Keep the API's 15-second budget separate from the unchanged UI assertions.
  await expect
    .poll(() => completionResponseAborted, { timeout: 15000 })
    .toBe(true);
  diagnostics.checkpoint("completion-offline-recovery");
  await expect(visibleText(/You’re offline.*saved progress/)).toBeVisible();
  diagnostics.checkpoint("completion-unconfirmed-feedback");
  await expect(visibleText("Quest complete!", { exact: true })).toHaveCount(0);
  diagnostics.checkpoint("completion-committed-state");
  const committed = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  expect(committed.overallXp).toBe(280);
  diagnostics.checkpoint("completion-retry-anchor");
  const synchronized = page.waitForResponse(
    (response) =>
      response.url() === "http://localhost:5217/api/sync-anchor" &&
      response.request().method() === "POST",
  );
  await activeRole(page, "button", { name: "Try saving again", exact: true })
    .click();
  expect((await synchronized).ok()).toBe(true);
  diagnostics.checkpoint("completion-feedback");
  await expect(visibleText("Quest complete!", { exact: true })).toBeVisible();
  diagnostics.checkpoint("completion-level-up");
  await expect(visibleText("Level up!", { exact: true })).toBeVisible();
  diagnostics.checkpoint("completion-level-transition");
  await expect(
    visibleText("Overall · Level 1 → 2", { exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: path.join(artifacts, "completion-celebration.png"),
    fullPage: false,
  });
  await activeRole(page, "button", { name: "Keep adventuring", exact: true })
    .click();
  await activeRole(page, "link", { name: "Back to Home", exact: true }).click();
  await expect(
    activeRole(page, "heading", { name: "Hey, Browser adventurer!", exact: true }),
  ).toBeVisible();
  await expect(homeXp).toBeVisible();
  console.log("Verified completion response-loss retry and character totals.");
  diagnostics.checkpoint("character");
  await verifyTravelerScene();
  diagnostics.checkpoint("home-narrow");
  await page.setViewportSize({ width: 320, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({ path: path.join(artifacts, "character-narrow.png") });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: path.join(artifacts, "character-home.png") });
  await activeRole(page, "tab", { name: "Progress", exact: true }).click();
  diagnostics.checkpoint("progress-details");
  await expect(visibleText("280 overall XP", { exact: true })).toBeVisible();
  diagnostics.checkpoint("progress-overall-bar");
  await expect(
    activeRole(page, "progressbar", { name: / level progress$/ }),
  ).toHaveCount(1);
  diagnostics.checkpoint("progress-sections");
  for (const [title, items] of [
    ["Categories", committed.categories],
    ["Attributes", committed.attributes],
    ["Skills", committed.skills],
  ]) {
    await expect(
      activeRole(page, "heading", { name: title, exact: true }),
    ).toBeVisible();
    const section = activeRole(page, "button", {
      name: `${title}, ${items.length}`,
      exact: true,
    });
    await expect(section).toHaveAttribute("aria-expanded", "false");
    await section.focus();
    await section.press(title === "Attributes" ? "Space" : "Enter");
    await expect(section).toHaveAttribute("aria-expanded", "true");
  }
  diagnostics.checkpoint("progress-expanded-bars");
  await expect(
    activeRole(page, "progressbar", { name: / level progress$/ }),
  ).toHaveCount(
    1 +
      committed.categories.length +
      committed.attributes.length +
      committed.skills.length,
  );
  await activeRole(page, "button", { name: "View rank progress", exact: true })
    .click();
  diagnostics.checkpoint("progress-rank-bars");
  const rankBars = activeRole(page, "progressbar", {
    name: /^(Qualifying categories|Combined category XP)$/,
  });
  await expect(rankBars).toHaveCount(2);
  diagnostics.checkpoint("progress-rank-value");
  await expect(
    activeRole(page, "progressbar", {
      name: "Overall level progress",
      exact: true,
    }),
  ).toHaveAttribute("aria-valuetext", "180/300 XP toward level 3");
  diagnostics.checkpoint("progress-rank-ladder");
  await activeRole(page, "button", { name: "See the rank ladder", exact: true })
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
  await activeRole(page, "button", { name: "Hide rank ladder", exact: true })
    .click();
  diagnostics.checkpoint("progress-category-floor");
  await activeRole(page, "button", { name: "See category requirements", exact: true })
    .click();
  await expect(
    visibleText("280 XP · Floor met", { exact: true }),
  ).toBeVisible();
  await activeRole(page, "button", { name: "Hide category requirements", exact: true })
    .click();
  await activeRole(page, "button", { name: "Hide rank progress", exact: true })
    .click();
  diagnostics.checkpoint("progress-streak");
  await expect(activeRole(page, "button", { name: /^Complete:/ })).toHaveCount(0);
  await expect(
    visibleText("1-day streak. Done today.", {
      exact: true,
    }),
  ).toBeVisible();
  for (const title of ["Categories", "Attributes", "Skills"]) {
    const section = activeRole(page, "button", {
      name: new RegExp(`^${title}, `),
    });
    await section.click();
    await expect(section).toHaveAttribute("aria-expanded", "false");
  }
  diagnostics.checkpoint("export-timer");
  await visibleRole("tab", { name: "Home", exact: true }).click();
  await expect(page).toHaveURL((url) => url.pathname === "/");
  diagnostics.checkpoint("settings-link");
  await visibleRole("link", { name: "Settings and profile", exact: true }).click();
  await expect(page).toHaveURL((url) => url.pathname === "/settings");
  diagnostics.checkpoint("settings-export");
  await activeRole(page, "button", { name: "Your traveler", exact: true }).click();
  await expect(
    visibleText("Your interests and experience", { exact: true }),
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
  await activeRole(page, "button", { name: "Privacy & account", exact: true })
    .click();
  const downloadEvent = page.waitForEvent("download");
  await activeRole(page, "button", { name: "Download full JSON export", exact: true })
    .click();
  const download = await downloadEvent;
  const exported = JSON.parse(await fs.readFile(await download.path(), "utf8"));
  expect(exported.state.overallXp).toBe(280);
  expect(exported.state.profile.avatarCode).toBe(
    initialCharacter.profile.avatarCode,
  );
  expect(exported.completions[0].snapshot.title).toBe(
    "Browser verified custom quest",
  );
  diagnostics.checkpoint("history-after-export");
  await page.goBack();
  await expect(page).toHaveURL((url) => url.pathname === "/");
  await visibleRole("tab", { name: "Quests", exact: true }).click();
  await activeRole(page, "tab", { name: "Completed", exact: true }).click();
  await verifyHistorySearch("Completed");
  await expect(
    activeRole(page, "link", {
      name: "Quest details: Browser verified custom quest",
      exact: true,
    }),
  ).toBeVisible();
  await activeRole(page, "heading", {
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
  await expect(activeRole(page, "button", { name: /^Complete:/ })).toHaveCount(0);
  await activeRole(page, "tab", { name: "Available", exact: true }).click();
  diagnostics.checkpoint("quest-discovery");
  const beforeDiscovery = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  let discoveryWrites = 0;
  const recordDiscoveryWrite = (request) => {
    if (
      request.method() === "POST" &&
      request.url() === "http://localhost:5217/api/commands"
    )
      discoveryWrites++;
  };
  page.on("request", recordDiscoveryWrite);
  try {
    const search = activeRole(page, "textbox", {
      name: "Search quests",
      exact: true,
    });
    const downtime = activeRole(page, "button", {
      name: "Quest details: Enjoy some downtime",
      exact: true,
    });
    await search.fill("no-matching-quest-for-this-search");
    await expect(
      visibleText(/No quests match this search and filter combination/),
    ).toBeVisible();
    await expect(downtime).toHaveCount(0);
    await search.fill("ENJOY downtime");
    await expect(downtime).toBeVisible();
    await activeRole(page, "button", { name: "Show quest filters", exact: true })
      .click();
    await activeRole(page, "radio", { name: "Custom quests", exact: true })
      .click();
    await expect(downtime).toHaveCount(0);
    await activeRole(page, "radio", { name: "Starter quests", exact: true })
      .click();
    await activeRole(page, "radio", { name: "Small", exact: true }).click();
    await activeRole(page, "checkbox", { name: "Ready to start", exact: true })
      .click();
    await activeRole(page, "checkbox", { name: "No XP penalties", exact: true })
      .click();
    await expect(downtime).toBeVisible();
    await activeRole(page, "button", { name: "Clear search and filters", exact: true })
      .click();
    await expect(search).toHaveValue("");
    await expect(
      activeRole(page, "radio", { name: "All quests", exact: true }),
    ).toBeChecked();
    await expect(
      activeRole(page, "radio", { name: "Any effort", exact: true }),
    ).toBeChecked();
    await expect(
      activeRole(page, "checkbox", { name: "Ready to start", exact: true }),
    ).not.toBeChecked();
    await expect(downtime).toBeVisible();
    const afterDiscovery = await (
      await context.request.get("http://localhost:5217/api/state", {
        headers: { Authorization: `Bearer ${fixture.token}` },
      })
    ).json();
    expect(afterDiscovery.overallXp).toBe(beforeDiscovery.overallXp);
    expect(afterDiscovery.occurrences).toEqual(beforeDiscovery.occurrences);
    expect(afterDiscovery.ledger).toEqual(beforeDiscovery.ledger);
    expect(discoveryWrites).toBe(0);
  } finally {
    page.off("request", recordDiscoveryWrite);
  }
  await activeRole(page, "button", {
      name: "Quest details: Enjoy some downtime",
      exact: true,
    })
    .click();
  await activeRole(page, "button", { name: "Start: Enjoy some downtime", exact: true })
    .click();
  await expect(
    activeRole(page, "button", { name: "Open active quest", exact: true }),
  ).toBeVisible();
  await activeRole(page, "button", { name: "Back to available quests", exact: true })
    .click();
  await activeRole(page, "tab", { name: "Active", exact: true }).click();
  await activeRole(page, "link", { name: /^Quest details:/ }).click();
  await activeRole(page, "button", { name: "Do Now with an optional timer", exact: true })
    .click();
  await activeRole(page, "button", { name: "Start focus timer", exact: true })
    .click();
  await expect(
    activeRole(page, "button", { name: "Pause focus timer", exact: true }),
  ).toBeVisible();
  const duringTimer = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  expect(duringTimer.overallXp).toBe(280);
  console.log("Verified completed list, export, and timer without XP award.");
  await activeRole(page, "link", { name: "Back to Home", exact: true }).click();
  await expect(homeXp).toBeVisible();
  await expect(page).toHaveURL("http://localhost:8081/");
  await page.goBack();
  await expect(
    activeRole(page, "button", { name: "Resume focus timer", exact: true }),
  ).toBeVisible();
  await activeRole(page, "button", { name: "Close focus timer", exact: true })
    .click();
  diagnostics.checkpoint("offline-undo");
  await context.setOffline(true);
  await activeRole(page, "button", { name: "Complete: Enjoy some downtime", exact: true })
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
  await activeRole(page, "button", { name: "Undo recent completion", exact: true })
    .click();
  await expect(
    activeRole(page, "button", {
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
  await activeRole(page, "button", { name: "Complete: Enjoy some downtime", exact: true })
    .click();
  const clockAnchorResponse = await refreshedAfterSkew;
  expect(clockAnchorResponse.ok()).toBe(true);
  clockAnchor = await clockAnchorResponse.json();
  expect(Date.parse(clockAnchor.recordedTimeFloor)).toBeGreaterThanOrEqual(
    Date.parse(clockRecordedAt),
  );
  await expect(visibleText("Quest complete!", { exact: true })).toBeVisible();
  await activeRole(page, "button", { name: "Undo recent completion", exact: true })
    .click();
  await expect(
    activeRole(page, "button", {
      name: "Try saving again",
      exact: true,
    }),
  ).toBeEnabled();
  await activeRole(page, "button", { name: "Try saving again", exact: true })
    .click();
  const completeAgain = activeRole(page, "button", {
    name: "Complete: Enjoy some downtime",
    exact: true,
  });
  await expect(completeAgain).toBeEnabled();
  expect(clockRetryVerified).toBe(true);
  // A subsequent real command must drain normally; no discard or proof mutation is allowed.
  await completeAgain.click();
  await expect(visibleText("Quest complete!", { exact: true })).toBeVisible();
  await activeRole(page, "button", { name: "Undo recent completion", exact: true })
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
  await expect(homeXp).toBeVisible();
  await activeRole(page, "tab", { name: "Quests", exact: true }).click();
  await activeRole(page, "tab", { name: "Active", exact: true }).click();
  await activeRole(page, "link", { name: /^Quest details:/ }).click();
  await expect(
    activeRole(page, "button", {
      name: "Complete: Enjoy some downtime",
      exact: true,
    }),
  ).toBeEnabled();
  await activeRole(page, "link", { name: "Back to Home", exact: true }).click();
  diagnostics.checkpoint("calendar-planning");
  // Calendar planning uses the same saved account and real SQL-backed commands.
  await activeRole(page, "tab", { name: "Calendar", exact: true }).click();
  await activeRole(page, "tab", { name: "7 days", exact: true }).click();
  await expect(
    activeRole(page, "tab", { name: "7 days", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  const plannedDate = new Date(
    Date.parse(synced.today + "T00:00:00Z") + 3 * 86400000,
  )
    .toISOString()
    .slice(0, 10);
  const displayPlannedDate = americanDate(plannedDate);
  await page.getByLabel("Calendar date MM/DD/YYYY").fill("02/30/2026");
  await activeRole(page, "button", { name: "Go to date", exact: true }).click();
  await expect(visibleText(/Enter a real date/)).toBeVisible();
  await page.getByLabel("Calendar date MM/DD/YYYY").fill(displayPlannedDate);
  await activeRole(page, "button", { name: "Go to date", exact: true }).click();
  await activeRole(page, "button", {
      name: `Create a quest for ${displayPlannedDate}`,
      exact: true,
    })
    .click();
  await page
    .getByLabel("Quest title", { exact: true })
    .fill("Calendar planned quest");
  await page
    .getByLabel("Completion criterion", { exact: true })
    .fill("Finish the planned calendar action");
  await activeRole(page, "button", { name: "Save custom quest", exact: true })
    .click();
  await expect(visibleText(/Saved revision 1/)).toBeVisible();
  await activeRole(page, "button", { name: "Back to available quests", exact: true })
    .click();
  await activeRole(page, "button", {
      name: "Quest details: Calendar planned quest",
      exact: true,
    })
    .click();
  await expect(page.getByLabel("Due date in MM/DD/YYYY format")).toHaveValue(
    displayPlannedDate,
  );
  await page.getByLabel("Optional planned time HH:mm").fill("09:30");
  const startCalendarQuest = activeRole(page, "button", {
    name: "Start: Calendar planned quest",
    exact: true,
  });
  await expect(startCalendarQuest).toBeEnabled();
  await startCalendarQuest.click();
  await expect(startCalendarQuest).toBeHidden();
  await activeRole(page, "button", { name: "Back to available quests", exact: true })
    .click();
  await activeRole(page, "button", { name: "Back to calendar", exact: true })
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
  await activeRole(page, "tab", { name: "Day", exact: true }).click();
  await page.screenshot({ path: path.join(artifacts, "calendar-day.png") });
  await activeRole(page, "tab", { name: "Quests", exact: true }).click();
  await activeRole(page, "tab", { name: "Active", exact: true }).click();
  await expect(
    visibleText(`Today · ${americanDate(plannedState.today)}`, { exact: true }),
  ).toBeVisible();
  await expect(visibleText("Unscheduled", { exact: true })).toBeVisible();
  await expect(visibleText("Upcoming", { exact: true })).toBeVisible();
  await expect(
    visibleText("Calendar planned quest", { exact: true }),
  ).toBeVisible();
  await page.screenshot({ path: path.join(artifacts, "active-planning.png") });
  await activeRole(page, "link", { name: /^Quest details:/ })
    .and(page.locator(`a[href="/quest/${calendarQuest.occurrence.id}"]`))
    .click();
  diagnostics.checkpoint("plan-edit");
  await expect(
    activeRole(page, "button", { name: "Save plan", exact: true }),
  ).toBeDisabled();
  await activeRole(page, "textbox", { name: "Quest due date MM/DD/YYYY", exact: true })
    .fill("02/30/2026");
  await expect(
    visibleText("Enter a real due date in MM/DD/YYYY format.", { exact: true }),
  ).toBeVisible();
  await expect(
    activeRole(page, "button", { name: "Save plan", exact: true }),
  ).toBeDisabled();
  await activeRole(page, "button", { name: "Leave this quest unscheduled", exact: true })
    .click();
  await expect(
    activeRole(page, "textbox", {
      name: "Quest planned time HH:mm",
      exact: true,
    }),
  ).toHaveValue("");
  await activeRole(page, "button", { name: "Save plan", exact: true }).click();
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
  await activeRole(page, "button", { name: "Plan for today", exact: true })
    .click();
  await activeRole(page, "textbox", { name: "Quest planned time HH:mm", exact: true })
    .fill("08:15");
  await activeRole(page, "button", { name: "Save plan", exact: true }).click();
  await waitForSavedPlan(plannedState.today, "08:15");
  await expect(
    activeRole(page, "button", { name: "Save plan", exact: true }),
  ).toBeDisabled();
  await page.screenshot({
    path: path.join(artifacts, "quest-plan-editor.png"),
  });
  await activeRole(page, "link", { name: "Back to Home", exact: true }).click();
  await activeRole(page, "tab", { name: "Home", exact: true }).click();
  console.log(
    "Calendar day/week navigation, invalid-date recovery and selected-date custom creation/acceptance passed against real SQL; planning awarded no XP.",
  );
  console.log(
    "Quest plan editing rejected invalid dates, persisted unscheduled date/time removal and today's timed plan, and preserved XP.",
  );
  diagnostics.checkpoint("viewports");
  await verifyNextFeatures({ page, context, fixture, artifacts, visibleText });
  for (const [width, height] of [
    [320, 720],
    [430, 932],
  ]) {
    await page.setViewportSize({ width, height });
    await expect(homeXp).toBeVisible();
    await verifyTravelerScene();
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
