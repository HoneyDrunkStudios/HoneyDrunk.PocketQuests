import { chromium, expect } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
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
  await page.goto("http://localhost:8081");
  const signIn = page.getByRole("button", {
    name: "Sign in or create an account",
  });
  await expect(signIn).toBeEnabled({ timeout: 90000 });
  await signIn.click();
  await expect(
    visibleText("Your interests and experience", { exact: true }),
  ).toBeVisible({ timeout: 30000 });
  await page
    .getByRole("button", { name: "Choose: Work & Purpose", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Selected: Work & Purpose", exact: true }),
  ).toBeEnabled();
  await page
    .getByRole("button", { name: "Programming: Expert", exact: true })
    .click();
  await expect(
    visibleText("Current assessment: Expert", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", {
      name: "Save setup and choose my first quest",
      exact: true,
    })
    .click();
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
  await context.route("http://localhost:5217/api/commands", async (route) => {
    if (route.request().method() !== "POST") return route.continue();
    const command = route.request().postDataJSON();
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
  await page
    .getByRole("button", { name: "Save custom quest", exact: true })
    .click();
  await expect(
    visibleText("Temporary save failure for retry verification.", {
      exact: true,
    }),
  ).toBeVisible();
  await expect(page.getByLabel("Quest title", { exact: true })).toHaveValue(
    "Browser verified custom quest",
  );
  await expect(
    page.getByLabel("Completion criterion", { exact: true }),
  ).toHaveValue("I have delivered the working result");
  await page
    .getByRole("button", { name: "Retry pending action", exact: true })
    .click();
  await expect(visibleText(/Saved revision 1/)).toBeVisible();
  await page
    .getByRole("button", { name: "Back to available quests", exact: true })
    .click();
  await page
    .getByRole("button", {
      name: "Start: Browser verified custom quest",
      exact: true,
    })
    .click();
  await page.getByRole("tab", { name: "Active", exact: true }).click();
  await page.getByRole("link", { name: "Open quest", exact: true }).click();
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
  await page
    .getByRole("button", {
      name: "Complete: Browser verified custom quest",
      exact: true,
    })
    .click({ clickCount: 2 });
  await expect(
    visibleText(/Offline.*showing your private device cache/),
  ).toBeVisible();
  await expect(visibleText("Quest complete!", { exact: true })).toHaveCount(0);
  const committed = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  expect(committed.overallXp).toBe(280);
  await page
    .getByRole("button", { name: "Synchronize recorded changes", exact: true })
    .click();
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
  await expect(
    visibleText("280 overall XP earned", { exact: true }),
  ).toBeVisible();
  console.log("Verified completion response-loss retry and character totals.");
  await expect(page.getByRole("button", { name: /^Complete:/ })).toHaveCount(0);
  await expect(
    visibleText("Work & Purpose: 1 day in this streak. Done today.", {
      exact: true,
    }),
  ).toBeVisible();
  await page.screenshot({
    path: path.join(artifacts, "character-home.png"),
    fullPage: false,
  });
  await page.getByRole("tab", { name: "Progress", exact: true }).click();
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
    visibleText("Browser verified custom quest", { exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: /^Complete:/ })).toHaveCount(0);
  await page.getByRole("tab", { name: "Available", exact: true }).click();
  await page
    .getByRole("button", { name: "Start: Enjoy some downtime", exact: true })
    .click();
  await page.getByRole("tab", { name: "Active", exact: true }).click();
  await page.getByRole("link", { name: "Open quest", exact: true }).click();
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
  await page.goBack();
  await expect(
    page.getByRole("button", { name: "Resume focus timer", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Close focus timer", exact: true })
    .click();
  await context.setOffline(true);
  await page
    .getByRole("button", { name: "Complete: Enjoy some downtime", exact: true })
    .click();
  await expect(
    visibleText(/Offline.*showing your private device cache/),
  ).toBeVisible();
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
  const synced = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  expect(synced.overallXp).toBe(280);
  expect(
    synced.occurrences.filter((o) => o.status === "Completed"),
  ).toHaveLength(1);
  await page.reload();
  // Fresh adapter session, same isolated test account: verifies server persistence.
  await expect(signIn).toBeEnabled();
  await signIn.click();
  await expect(
    visibleText("280 overall XP earned", { exact: true }),
  ).toBeVisible();
  await page.getByRole("tab", { name: "Quests", exact: true }).click();
  await page.getByRole("tab", { name: "Active", exact: true }).click();
  await page.getByRole("link", { name: "Open quest", exact: true }).click();
  await expect(
    page.getByRole("button", {
      name: "Complete: Enjoy some downtime",
      exact: true,
    }),
  ).toBeEnabled();
  await page.getByRole("link", { name: "Back to Home", exact: true }).click();
  for (const [width, height] of [
    [320, 720],
    [430, 932],
  ]) {
    await page.setViewportSize({ width, height });
    await expect(
      visibleText("280 overall XP earned", { exact: true }),
    ).toBeVisible();
    await page.screenshot({
      path: path.join(artifacts, `home-${width}.png`),
    });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
  }
  expect(failures).toEqual([]);
  console.log(
    "Web adapter / real API and isolated SQL journey passed: test OIDC/PKCE, save failure and retry, double tap with lost completion response, exact level-up, character sheet, export, completed list, optional timer and route pause, offline completion, reconnect, matching Undo, fresh-session SQL persistence. No real-provider or native-device claim.",
  );
} catch (error) {
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
