import { chromium, expect } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
const root = path.resolve("../..");
const fixture = JSON.parse(
  await fs.readFile(
    path.join(
      root,
      process.env.POCKETQUESTS_FIXTURE_DIRECTORY ?? ".local/browser",
      "ready.json",
    ),
    "utf8",
  ),
);
const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: 390, height: 844 },
  reducedMotion: "reduce",
});
const page = await context.newPage();
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
    page.getByText("Your interests and experience", { exact: true }),
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
    page.getByText("Current assessment: Expert", { exact: true }),
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
  await page
    .getByRole("button", { name: "Save custom quest", exact: true })
    .click();
  await expect(page.getByText(/Saved revision 1/)).toBeVisible();
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
      name: "Complete: Browser verified custom quest",
      exact: true,
    })
    .click({ clickCount: 2 });
  await expect(
    page.getByText("Quest complete!", { exact: true }),
  ).toBeVisible();
  await expect(page.getByText("Level up!", { exact: true })).toBeVisible();
  await expect(
    page.getByText("Overall · Level 1 → 2", { exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: path.join(root, "artifacts/completion-celebration.png"),
    fullPage: false,
  });
  await page
    .getByRole("button", { name: "Keep adventuring", exact: true })
    .click();
  await page.getByRole("link", { name: "Back to Home", exact: true }).click();
  await expect(page.getByText("Your character", { exact: true })).toBeVisible();
  await expect(
    page.getByText("280 overall XP earned", { exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: /^Complete:/ })).toHaveCount(0);
  await page.screenshot({
    path: path.join(root, "artifacts/character-home.png"),
    fullPage: false,
  });
  await page.getByRole("tab", { name: "Progress", exact: true }).click();
  await expect(
    page.getByText("280 base XP earned", { exact: true }),
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
  await page
    .getByRole("button", { name: "Close focus timer", exact: true })
    .click();
  await context.setOffline(true);
  await page
    .getByRole("button", { name: "Complete: Enjoy some downtime", exact: true })
    .click();
  await expect(
    page.getByText(/Offline - showing your private device cache/).first(),
  ).toBeVisible();
  await expect(page.getByText("Quest complete!", { exact: true })).toHaveCount(
    0,
  );
  await page.reload();
  await expect(
    page.getByText(/Rewards will be confirmed when synchronized/),
  ).toBeVisible();
  await context.setOffline(false);
  await page
    .getByRole("button", { name: "Synchronize recorded changes", exact: true })
    .click();
  await expect(page.getByText("Quest complete!", { exact: true })).toBeVisible({
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
  await page.reload();
  const synced = await (
    await context.request.get("http://localhost:5217/api/state", {
      headers: { Authorization: `Bearer ${fixture.token}` },
    })
  ).json();
  expect(synced.overallXp).toBe(280);
  expect(
    synced.occurrences.filter((o) => o.status === "Completed"),
  ).toHaveLength(1);
  await expect(
    page.getByRole("button", {
      name: "Complete: Enjoy some downtime",
      exact: true,
    }),
  ).toBeEnabled();
  expect(failures).toEqual([]);
  console.log(
    "Native web adapter / real SQL slice journey passed: PKCE fixture → real Identity → onboarding → custom A quest → SQL completion → profile → private JSON download → offline completion → reconnect reconciliation.",
  );
} catch (error) {
  await page.screenshot({
    path: path.join(root, "artifacts/browser-failure.png"),
    fullPage: false,
  });
  console.error((await page.locator("body").innerText()).slice(0, 5000));
  throw error;
} finally {
  await browser.close();
  await fs.writeFile(
    path.join(
      root,
      process.env.POCKETQUESTS_FIXTURE_DIRECTORY ?? ".local/browser",
      "done",
    ),
    "done",
  );
}
