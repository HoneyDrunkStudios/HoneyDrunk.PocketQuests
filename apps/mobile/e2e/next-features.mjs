import { expect } from "@playwright/test";
import path from "node:path";

// Public map transport is deterministic here; account reads/writes use the real
// authenticated API and disposable SQL. Live provider probing is a separate lane.
export async function verifyNextFeatures({
  page,
  context,
  fixture,
  artifacts,
  visibleText,
}) {
  const visible = (role, name) =>
    page.getByRole(role, { name, exact: true }).filter({ visible: true });
  async function narrowCapture(name, testId) {
    await page.setViewportSize({ width: 320, height: 844 });
    const content = page.getByTestId(testId).filter({ visible: true });
    const previous = await content.evaluate((root) => {
      const elements = [...root.querySelectorAll("*")].filter((element) =>
        [...element.childNodes].some(
          (node) => node.nodeType === Node.TEXT_NODE && node.textContent.trim(),
        ),
      );
      return elements.map((element) => {
        const prior = element.getAttribute("style");
        const computed = getComputedStyle(element);
        element.style.fontSize = `${parseFloat(computed.fontSize) * 2}px`;
        if (computed.lineHeight !== "normal")
          element.style.lineHeight = `${parseFloat(computed.lineHeight) * 2}px`;
        return prior;
      });
    });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: path.join(artifacts, `${name}-320-large-text.png`),
    });
    await content.evaluate((root, styles) => {
      const elements = [...root.querySelectorAll("*")].filter((element) =>
        [...element.childNodes].some(
          (node) => node.nodeType === Node.TEXT_NODE && node.textContent.trim(),
        ),
      );
      elements.forEach((element, index) =>
        styles[index] === null
          ? element.removeAttribute("style")
          : element.setAttribute("style", styles[index]),
      );
    }, previous);
    await page.setViewportSize({ width: 390, height: 844 });
  }
  const api = async (route) => {
    const response = await context.request.get(
      `http://localhost:5217/api/${route}`,
      {
        headers: { Authorization: `Bearer ${fixture.token}` },
      },
    );
    expect(response.ok()).toBe(true);
    return response.json();
  };
  const before = await api("state");
  await expect(
    page.getByTestId("today-quests").filter({ visible: true }),
  ).toBeVisible();
  await visible("link", "See your active quests").click();
  await expect(visible("tab", "Active")).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await visible("tab", "Available").click();
  await visible("tab", "Home").click();
  await visible("link", "See your active quests").click();
  await expect(visible("tab", "Active")).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await visible("tab", "Home").click();
  await visible("link", "Open your reward journal").click();
  await expect(
    page
      .getByText("Your reward journal", { exact: true })
      .filter({ visible: true }),
  ).toBeVisible();
  await visible("button", "Earned").click();
  await visible("button", "All rewards").click();
  await page.screenshot({ path: path.join(artifacts, "reward-journal.png") });
  await narrowCapture("reward-journal", "reward-journal");
  await visible("link", "Open your wardrobe").click();
  await visible("tab", "Clothing").click();
  expect(before.entitlements.find((reward) => reward.id === "PQ-CAT-A01").earned).toBe(true);
  await expect(visible("button", "Maker’s vest")).toBeEnabled();
  await expect(visible("button", "Save avatar")).toBeDisabled();
  await page.screenshot({ path: path.join(artifacts, "wardrobe-unlocked.png") });
  await page.goBack();
  await page.goBack();
  await visible("tab", "Progress").click();
  const recap = page.getByTestId("activity-recap").filter({ visible: true });
  await expect(recap).toBeVisible();
  await recap
    .getByRole("button", { name: "Last 30 days", exact: true })
    .click();
  await recap
    .getByRole("button", { name: "Explore activity", exact: true })
    .click();
  await recap.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(artifacts, "activity-recap.png") });
  await narrowCapture("activity-recap", "activity-recap");

  let searches = 0;
  const feature = (id, name, key, value, longitude, latitude) => ({
    type: "Feature",
    geometry: { type: "Point", coordinates: [longitude, latitude] },
    properties: {
      osm_type: "N",
      osm_id: id,
      name,
      osm_key: key,
      osm_value: value,
    },
  });
  await context.route("https://photon.komoot.io/**", (route) => {
    searches++;
    const isArea = new URL(route.request().url()).pathname === "/api/";
    return route.fulfill({
      headers: { "access-control-allow-origin": "http://localhost:8081" },
      json: {
        type: "FeatureCollection",
        features: isArea
          ? [
              feature(
                900001,
                "Asheville sample area",
                "place",
                "city",
                -82.554,
                35.595,
              ),
            ]
          : [
              feature(
                900002,
                "Sample riverside park",
                "leisure",
                "park",
                -82.553,
                35.595,
              ),
              feature(
                900003,
                "Sample neighborhood library",
                "amenity",
                "library",
                -82.554,
                35.596,
              ),
            ],
      },
    });
  });
  await visible("tab", "Explore").click();
  await expect(visible("textbox", "Search area")).toBeVisible();
  expect(searches).toBe(0);
  await visible("textbox", "Search area").fill("Asheville");
  await visible("button", "Find an area").click();
  await visible("button", "Asheville sample area").click();
  await visible("button", "1. Sample riverside park · Parks").click();
  await visible("button", "Save place to journal").click();
  await expect(
    page
      .getByText("Discovery journal", { exact: true })
      .filter({ visible: true }),
  ).toBeVisible();
  await visible("button", "Sample riverside park · Want to visit").click();
  await visible("textbox", "Notes for Sample riverside park").fill(
    "Bring a sketchbook for a quiet afternoon.",
  );
  await visible("button", "Want to visit — mark visited").click();
  await visible("button", "Link an accepted quest").click();
  await visible("textbox", "Find a quest to link").fill(
    "Calendar planned quest",
  );
  await visible("button", "Calendar planned quest").click();
  const loseResponse = async (route) => {
    await route.fetch();
    await route.abort("failed");
  };
  await context.route(
    "http://localhost:5217/api/discoveries/save",
    loseResponse,
    { times: 1 },
  );
  await visible("button", "Save place details").click();
  await expect(visible("button", "Save place details")).toBeDisabled();
  await expect(
    visible("textbox", "Notes for Sample riverside park"),
  ).toHaveValue("Bring a sketchbook for a quiet afternoon.");
  await visible("button", "Reload saved places").click();
  await expect(
    visible("button", "Sample riverside park · Visited"),
  ).toBeVisible();
  const saved = await api("discoveries");
  expect(saved).toHaveLength(1);
  expect(saved[0].notes).toBe("Bring a sketchbook for a quiet afternoon.");
  expect(saved[0].questOccurrenceId).toBeTruthy();
  expect(saved[0].revision).toBe(2);
  if (!(await visible("button", "Show on map").isVisible()))
    await visible("button", "Sample riverside park · Visited").click();
  await page.screenshot({
    path: path.join(artifacts, "saved-place-journal.png"),
  });
  await narrowCapture("saved-place-journal", "exploration");
  const searchCount = searches;
  await visible("button", "Show on map").click();
  await expect(visible("button", "Find nearby places")).toBeVisible();
  expect(searches).toBe(searchCount);
  await page.screenshot({
    path: path.join(artifacts, "explore-saved-map.png"),
  });
  await visible("link", "Browse Rest & Recreation quests").click();
  await expect(page).toHaveURL(/\/quest\/discover\/c07/);
  await page.goBack();
  await visible("tab", "Home").click();
  const after = await api("state");
  await visibleText(`${after.overallXp} overall XP`, { exact: true }).scrollIntoViewIfNeeded();
  expect(after.overallXp).toBe(before.overallXp);
  expect(after.profile.avatarCode).toBe(before.profile.avatarCode);
  expect(after.occurrences).toHaveLength(before.occurrences.length);
  console.log(
    "Daily-plan navigation, reward journal, recap, map search and saved-place notes/visit/quest link passed against real SQL, including committed-response loss and reload; XP/avatar/quests preserved. Map responses were fixtures.",
  );
}
