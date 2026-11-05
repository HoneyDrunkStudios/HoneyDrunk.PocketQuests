import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import harness from "./provider-harness.cjs";
const require = createRequire(import.meta.url);
const wire = require("../../../../contracts/wire-fixtures.json");
const { account, createHarness } = harness;
const response = (value) => ({
  ok: true,
  status: 200,
  json: async () => structuredClone(value),
});

test("ordinary online refresh discovers publication, revised terms and withdrawal without reconnecting", async () => {
  const host = createHarness();
  let catalog = structuredClone(wire.catalog);
  host.respond = async (url) =>
    url.endsWith("/api/catalog") ? response(catalog) : null;
  await host.mount();
  const published = {
    ...structuredClone(catalog.quests[0]),
    id: "PQ-ADM-" + "a".repeat(32),
    title: "A newly published adventure",
  };
  catalog = { ...catalog, quests: [...catalog.quests, published] };
  await host.render().refresh();
  assert.equal(host.render().catalog.quests.at(-1).title, published.title);
  assert.deepEqual(host.disk.catalog, catalog);
  catalog = {
    ...catalog,
    quests: catalog.quests.map((quest) =>
      quest.id === published.id
        ? { ...quest, title: "Revised adventure", penaltyPercent: 25 }
        : quest,
    ),
  };
  await host.render().refresh();
  assert.equal(host.render().catalog.quests.at(-1).penaltyPercent, 25);
  catalog = {
    ...catalog,
    quests: catalog.quests.filter((quest) => quest.id !== published.id),
  };
  await host.render().refresh();
  assert.equal(
    host.render().catalog.quests.some((quest) => quest.id === published.id),
    false,
  );
  assert.deepEqual(host.disk.catalog, catalog);
  assert.deepEqual(host.sent, []);
});

test("offline and failed or malformed catalog refreshes preserve the last verified cache", async () => {
  const cached = account();
  cached.catalog = structuredClone(wire.catalog);
  const host = createHarness(cached);
  host.online = false;
  await host.mount();
  assert.deepEqual(host.render().catalog, cached.catalog);
  await host.render().refresh();
  assert.deepEqual(host.disk.catalog, cached.catalog);
  host.online = true;
  for (const bad of [
    { ok: false, status: 503, json: async () => ({ detail: "Try later" }) },
    response({ quests: [null] }),
  ]) {
    host.respond = async (url) => (url.endsWith("/api/catalog") ? bad : null);
    await host.render().refresh();
    assert.deepEqual(host.render().catalog, cached.catalog);
    assert.deepEqual(host.disk.catalog, cached.catalog);
    assert.ok(host.render().error);
  }
  const restored = createHarness(host.disk, "offline-restart");
  restored.online = false;
  await restored.mount();
  assert.deepEqual(restored.render().catalog, cached.catalog);
});
