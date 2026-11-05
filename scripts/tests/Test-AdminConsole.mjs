// Real Chromium against shipped static assets with a controlled API. SQL and
// encrypted-cookie/CSRF behavior are covered separately by RelationalApiTests.
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile, mkdir } from "node:fs/promises";
import { createRequire } from "node:module";
import { resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../../", import.meta.url));
const require = createRequire(resolve(root, "apps/mobile/package.json"));
const { chromium } = require("@playwright/test");
const staticRoot = resolve(root, "src/PocketQuests.Api/wwwroot/admin");
const server = createServer(async (request, response) => {
  const path = new URL(request.url, "http://localhost").pathname;
  const file = resolve(staticRoot, path.slice("/admin/".length));
  if (
    !path.startsWith("/admin/") ||
    !file.startsWith(staticRoot + sep) ||
    !/\.(html|css|js)$/.test(file)
  ) {
    response.writeHead(404).end();
    return;
  }
  response.setHeader(
    "Content-Type",
    file.endsWith("html")
      ? "text/html; charset=utf-8"
      : file.endsWith("css")
        ? "text/css"
        : "text/javascript",
  );
  response.setHeader(
    "Content-Security-Policy",
    "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; base-uri 'none'; frame-ancestors 'none'; form-action 'self'",
  );
  try {
    response.end(await readFile(file));
  } catch {
    response.writeHead(404).end();
  }
});
await new Promise((done) => server.listen(0, "127.0.0.1", done));
let browser;
try {
  browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({
    viewport: { width: 1280, height: 1000 },
  });
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  const operator = "usr_" + "A".repeat(26);
  const player = {
    userId: "usr_" + "B".repeat(26),
    characterName: "River <script>alert(1)</script>",
    zone: "America/New_York",
    createdAt: "2026-01-01T12:00:00Z",
    onboardingComplete: true,
    suspended: false,
    reason: null,
    revision: 0,
  };
  let current = { ...player };
  let writes = 0;
  let failMutation = false;
  let authenticated = true;
  let lastSearch;
  let studioSaved = null;
  let studioWrites = 0;
  const studioCatalog = JSON.parse(
    await readFile(resolve(root, "contracts/wire-fixtures.json"), "utf8"),
  ).catalog;
  await page.route("**/api/admin/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    const body = route.request().postDataJSON();
    const reply = (status, value) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(value),
      });
    if (!authenticated) return reply(401, {});
    if (path.endsWith("/session"))
      return reply(200, { userId: operator, csrfToken: "fixture-only" });
    assert.equal(route.request().headers()["x-csrf-token"], "fixture-only");
    if (path.endsWith("/quests/list"))
      return reply(200, {
        quests: studioSaved ? [studioSaved] : [],
        catalog: studioCatalog,
      });
    if (path.endsWith("/quests/preview"))
      return reply(200, { ...body.quest, baseXp: 10 });
    if (path.endsWith("/quests/draft")) {
      studioWrites++;
      studioSaved = {
        id: body.id,
        version: "AAAAAAAAAAE=",
        draftNumber: studioWrites,
        draftSystemQuestRevisionId: "11111111-1111-4111-8111-111111111111",
        publishedSystemQuestRevisionId: null,
        draft: { ...body.quest, baseXp: 10 },
        published: null,
        modifiedAt: "2026-01-01T12:00:00Z",
      };
      return reply(200, studioSaved);
    }
    if (path.endsWith("/users/search")) {
      lastSearch = body;
      return reply(200, {
        users: [
          current,
          { ...player, userId: operator, characterName: "Operator" },
        ],
        nextCursor: null,
      });
    }
    if (path.endsWith("/users/access")) {
      writes++;
      if (failMutation) return reply(409, {});
      assert.equal(body.expectedRevision, current.revision);
      current = {
        ...current,
        suspended: body.suspended,
        reason: body.reason,
        revision: current.revision + 1,
      };
      return reply(200, current);
    }
    if (path.endsWith("/logout")) {
      authenticated = false;
      return route.fulfill({ status: 204 });
    }
    throw new Error("Unexpected test API route");
  });
  const url = `http://127.0.0.1:${server.address().port}/admin/index.html`;
  await page.goto(url);
  await page.locator(".user-row").first().waitFor();
  await page.locator(".user-row").first().click();
  assert.equal(
    await page.locator("#detail-heading").textContent(),
    player.characterName,
  );
  assert.equal(await page.locator("#details script").count(), 0);
  await page.locator("#reason").fill("Review requested by support");
  await page.locator("#review").click();
  assert.equal(writes, 0, "Review must not mutate access");
  await page.locator("#cancel").click();
  assert.equal(writes, 0, "Cancel must preserve access");
  await page.locator("#review").click();
  await page.locator("#confirm").click();
  await page
    .getByText("Access suspended. The profile and progress are preserved.", {
      exact: true,
    })
    .waitFor();
  assert.equal(writes, 1);
  await page.locator("#reason").fill("Review complete");
  await page.locator("#review").click();
  failMutation = true;
  await page.locator("#confirm").click();
  await page.locator("#reload").waitFor();
  assert.equal(
    await page.locator("#confirmation-reason").textContent(),
    "Review complete",
  );
  assert.equal(await page.locator("#confirm").isDisabled(), true);
  assert.equal(await page.locator(".user-row").first().isDisabled(), true);
  assert.equal(await page.locator("#search button").isDisabled(), true);
  await page.locator("#reload").click();
  await page
    .getByText(
      "Status reloaded. Review your reason before making a new change.",
      { exact: true },
    )
    .waitFor();
  failMutation = false;
  assert.equal(await page.locator("#reason").inputValue(), "Review complete");
  await page.locator("#review").click();
  await page.locator("#confirm").click();
  await page
    .getByText("Access reactivated. The traveler can reconnect.", {
      exact: true,
    })
    .waitFor();
  await page.locator(".user-row").last().click();
  assert.equal(await page.locator("#review").isDisabled(), true);
  await page.locator("#query").fill("literal % and ' name");
  await page.locator("#search button").click();
  await page.waitForFunction(
    () => !document.querySelector(".user-row").disabled,
  );
  assert.equal(lastSearch.query, "literal % and ' name");
  await page.locator(".user-row").first().click();
  if (process.env.PQ_ADMIN_EVIDENCE) {
    await mkdir(process.env.PQ_ADMIN_EVIDENCE, { recursive: true });
    await page.screenshot({
      path: resolve(process.env.PQ_ADMIN_EVIDENCE, "admin-users-desktop.png"),
      fullPage: true,
    });
  }
  await page.setViewportSize({ width: 320, height: 900 });
  assert.equal(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
    true,
    "320px must not overflow",
  );
  await page.evaluate(() => {
    document.documentElement.style.fontSize = "200%";
  });
  assert.equal(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
    true,
    "Large text must not overflow",
  );
  if (process.env.PQ_ADMIN_EVIDENCE)
    await page.screenshot({
      path: resolve(
        process.env.PQ_ADMIN_EVIDENCE,
        "admin-users-large-text.png",
      ),
      fullPage: true,
    });
  await page.locator("#signout").click();
  await page.locator("#signin").waitFor();
  assert.equal(
    await page.locator("#users, #metadata, #query, #reason").count(),
    0,
  );
  authenticated = true;
  await page.reload();
  await page.locator(".user-row").first().waitFor();
  authenticated = false;
  await page.locator("#search button").click();
  await page.locator("#signin").waitFor();
  assert.match(
    await page.locator("#auth-notice").textContent(),
    /session cannot access/,
  );
  assert.equal(await page.locator("#users").count(), 0);
  authenticated = true;
  await page.reload();
  await page
    .getByRole("button", { name: "System quests", exact: true })
    .click();
  await page
    .getByRole("button", { name: "New system quest", exact: true })
    .click();
  await page
    .getByLabel("Quest title", { exact: true })
    .fill("A little adventure outside");
  await page
    .getByLabel("What counts as complete?", { exact: true })
    .fill("Finish one self-chosen walk somewhere new.");
  assert.equal(
    await page
      .getByRole("button", { name: "Save draft", exact: true })
      .isDisabled(),
    true,
  );
  await page
    .getByRole("button", { name: "Validate and preview", exact: true })
    .click();
  await page
    .getByRole("heading", { name: "A little adventure outside", exact: true })
    .waitFor();
  assert.equal(studioWrites, 0);
  await page.getByRole("button", { name: "Save draft", exact: true }).click();
  await page
    .getByText("Draft 1 saved. It is not published to players.", {
      exact: true,
    })
    .waitFor();
  assert.equal(studioWrites, 1);
  await page.setViewportSize({ width: 1280, height: 1000 });
  if (process.env.PQ_ADMIN_EVIDENCE)
    await page.screenshot({
      path: resolve(
        process.env.PQ_ADMIN_EVIDENCE,
        "system-quest-studio-desktop.png",
      ),
      fullPage: true,
    });
  await page.setViewportSize({ width: 320, height: 900 });
  assert.equal(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
    true,
    "Quest editor must fit 320px",
  );
  await page.evaluate(() => {
    document.documentElement.style.fontSize = "200%";
  });
  assert.equal(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
    true,
    "Quest editor must fit enlarged text",
  );
  if (process.env.PQ_ADMIN_EVIDENCE)
    await page.screenshot({
      path: resolve(
        process.env.PQ_ADMIN_EVIDENCE,
        "system-quest-studio-large-text.png",
      ),
      fullPage: true,
    });
  assert.deepEqual(errors, []);
  console.log(
    "Admin Chromium checks passed: review/cancel, access changes, stale edit/reload, self protection, literal rendering/search, logout/expiry clearing, 320px/large text.",
  );
} finally {
  await browser?.close();
  await new Promise((done) => server.close(done));
}
