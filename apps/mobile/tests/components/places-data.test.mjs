import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url),
  ts = require("typescript");
process.env.EXPO_PUBLIC_APP_ENV = "development";
let platform = "android",
  permissionResult = null,
  permissionCalls = 0,
  permission = "granted",
  locationUnavailable = true,
  locationCalls = 0;
const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native")
    return {
      Platform: {
        get OS() {
          return platform;
        },
      },
    };
  if (name === "expo-location") {
    if (locationUnavailable)
      throw new Error("Cannot find native module ExpoLocation");
    return {
      Accuracy: { Balanced: 3 },
      requestForegroundPermissionsAsync: async () => {
        permissionCalls++;
        return permissionResult ?? { status: permission };
      },
      getCurrentPositionAsync: async () => {
        locationCalls++;
        return { coords: { latitude: 35, longitude: -82 } };
      },
    };
  }
  return load.call(this, name, ...rest);
};
require.extensions[".ts"] = (module, file) =>
  module._compile(
    ts.transpileModule(require("node:fs").readFileSync(file, "utf8"), {
      compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2022,
      },
    }).outputText,
    file,
  );
const {
  decodeAreas,
  decodePlaces,
  placesParameters,
  distanceMeters,
} = require("../../src/api/places-contract.ts");
const { createPlacesClient } = require("../../src/api/places-client.ts");
const {
  locateOnce,
} = require("../../src/features/progression/map-location.ts");
const {
  worldPoint,
  moveCenter,
  mapPosition,
  visibleTiles,
} = require("../../src/features/progression/map-geometry.ts");
const center = { latitude: 35, longitude: -82 };
const {
  savedMapPoint,
  mergeSavedMapPoints,
} = require("../../src/features/progression/saved-map-points.ts");
const feature = (id = 1, extra = {}) => ({
  type: "Feature",
  properties: {
    osm_id: id,
    osm_type: "N",
    osm_key: "leisure",
    osm_value: "park",
    name: "Test park",
    ...extra,
  },
  geometry: { type: "Point", coordinates: [-82, 35] },
});
const collection = (features) => ({ type: "FeatureCollection", features });

test("saved map pins exclude private fields, deduplicate provider places and keep their addresses", () => {
  const place = {
    ...center,
    id: "account-row",
    placeKey: "node/1",
    name: "Saved park",
    kind: "Parks",
    categoryId: "c07",
    notes: "Private note",
    questOccurrenceId: "private-quest",
    visitedAt: "2026-10-28T12:00:00Z",
  };
  const point = savedMapPoint(place, center);
  assert.equal(point.id, "node/1");
  assert.equal(point.distanceMeters, 0);
  assert.equal("notes" in point, false);
  assert.equal("questOccurrenceId" in point, false);
  assert.equal("visitedAt" in point, false);
  const publicPoint = { ...point, name: "Public park", address: "Main Street" };
  const before = JSON.stringify([publicPoint, place]);
  const merged = mergeSavedMapPoints([publicPoint], [place], center);
  assert.equal(merged.length, 1);
  assert.equal(merged[0].name, "Saved park");
  assert.equal(merged[0].address, "Main Street");
  assert.equal(JSON.stringify([publicPoint, place]), before);
});

test("saved pins are limited to the viewed area without removing prior provider results", () => {
  const nearby = {
    ...center,
    placeKey: "node/near",
    name: "Nearby",
    kind: "Parks",
    categoryId: "c07",
  };
  const far = { ...nearby, placeKey: "node/far", latitude: 36 };
  assert.deepEqual(
    mergeSavedMapPoints([], [nearby, far], center).map((p) => p.id),
    ["node/near"],
  );
  assert.equal(
    mergeSavedMapPoints([savedMapPoint(far, center)], [nearby], center).length,
    2,
  );
});

test("place decoding rejects malformed, private, unsupported, distant and duplicate points", () => {
  const valid = feature();
  const invalid = feature(2);
  invalid.geometry.coordinates = [Infinity, 35];
  const far = feature(3);
  far.geometry.coordinates = [0, 0];
  assert.throws(() => decodePlaces({}, center));
  const places = decodePlaces(
    collection([
      valid,
      valid,
      invalid,
      far,
      feature(4, { access: "private" }),
      feature(5, { osm_key: "building", osm_value: "house" }),
      feature(6, { name: "" }),
      feature(7, { osm_type: "invalid" }),
    ]),
    center,
  );
  assert.equal(places.length, 1);
  assert.equal(places[0].id, "node/1");
  assert.equal(places[0].categoryId, "c07");
  assert.equal(places[0].distanceMeters, 0);
  assert.equal(
    decodePlaces(
      collection(Array.from({ length: 100 }, (_, i) => feature(i + 1))),
      center,
    ).length,
    40,
  );
});
test("area decoding validates geometry, caps results and preserves multilingual names", () => {
  assert.throws(() => decodeAreas({}));
  assert.equal(decodeAreas(collection([{}])).length, 0);
  const results = decodeAreas(
    collection(
      Array.from({ length: 9 }, () =>
        feature(1, { name: "Montréal", city: "Montréal", country: "Canada" }),
      ),
    ),
  );
  assert.equal(results.length, 5);
  assert.equal(results[0].name, "Montréal, Canada");
});
test("nearby search is bounded and contains only the supported public-place categories", () => {
  const query = placesParameters(center);
  assert.equal(query.get("radius"), "1.5");
  assert.equal(query.get("limit"), "40");
  assert.equal(query.getAll("osm_tag").length, 8);
  assert.throws(() => placesParameters({ latitude: NaN, longitude: 0 }));
});
test("map projection handles center, pan, poles and date-line wrapping without unbounded tile loads", () => {
  assert.deepEqual(mapPosition(center, center, 14, 320), { x: 160, y: 200 });
  assert.ok(moveCenter(center, 14, 200, 0).longitude > center.longitude);
  const wrapped = moveCenter({ latitude: 0, longitude: 179.999 }, 14, 2000, 0);
  assert.ok(wrapped.longitude < 0);
  assert.ok(Number.isFinite(worldPoint({ latitude: 90, longitude: 0 }, 14).y));
  for (const point of [
    center,
    { latitude: 85, longitude: 179.999 },
    { latitude: -85, longitude: -179.999 },
  ])
    for (const width of [288, 358, 780]) {
      const tiles = visibleTiles(point, 14, width);
      assert.ok(tiles.length <= 15);
      assert.ok(
        tiles.every(
          (t) => t.x >= 0 && t.x < 2 ** 14 && t.y >= 0 && t.y < 2 ** 14,
        ),
      );
    }
  assert.ok(distanceMeters(center, { latitude: 35.001, longitude: -82 }) > 100);
});
test("public map requests omit credentials, encode searches, cache valid results and do not cache invalid payloads", async () => {
  const previous = global.fetch,
    now = Date.now;
  let time = 10000;
  Date.now = () => time;
  const requests = [];
  global.fetch = async (url, init) => {
    requests.push({ url, init });
    return new Response(JSON.stringify(collection([feature()])), {
      status: 200,
    });
  };
  try {
    const client = createPlacesClient(),
      signal = new AbortController().signal;
    await client.areas("A & B", signal);
    await client.areas("A & B", signal);
    assert.equal(requests.length, 1);
    assert.equal(new URL(requests[0].url).searchParams.get("q"), "A & B");
    assert.equal(requests[0].init.credentials, "omit");
    assert.equal(requests[0].init.headers.Authorization, undefined);
    assert.match(requests[0].init.headers["User-Agent"], /PocketQuests/);
    await client.points(center, signal);
    assert.equal(requests.length, 2);
    await assert.rejects(
      client.points({ ...center, latitude: 35.01 }, signal),
      /wait a moment/,
    );
    time += 2000;
    global.fetch = async () => new Response('{"bad":true}', { status: 200 });
    await assert.rejects(client.areas("Other", signal), /unreadable/);
    time += 2000;
    global.fetch = async () =>
      new Response(JSON.stringify(collection([feature()])), { status: 200 });
    assert.equal((await client.areas("Other", signal)).length, 1);
    const cancelled = new AbortController();
    cancelled.abort();
    await assert.rejects(client.areas("A & B", cancelled.signal), /cancelled/);
  } finally {
    global.fetch = previous;
    Date.now = now;
  }
});
test("rate limits and malformed JSON surface recoverable errors without automatic retries", async () => {
  const previous = global.fetch;
  let count = 0;
  try {
    global.fetch = async () => {
      count++;
      return new Response("", { status: 429 });
    };
    await assert.rejects(
      createPlacesClient().areas("Test", new AbortController().signal),
      /busy/,
    );
    assert.equal(count, 1);
    global.fetch = async () => new Response("<html>", { status: 200 });
    await assert.rejects(
      createPlacesClient().areas("Test", new AbortController().signal),
      /unreadable/,
    );
  } finally {
    global.fetch = previous;
  }
});
test("location is foreground and requested only explicitly; denial does not query coordinates", async () => {
  locationUnavailable = false;
  permission = "denied";
  locationCalls = 0;
  await assert.rejects(locateOnce(), /Location access is off/);
  assert.equal(locationCalls, 0);
  permission = "granted";
  assert.deepEqual(await locateOnce(), center);
  assert.equal(locationCalls, 1);
});

test("an older native client can load the map and receives a manual search fallback", async () => {
  locationUnavailable = true;
  locationCalls = 0;
  try {
    await assert.rejects(
      locateOnce(),
      /Search for a town or neighborhood instead/,
    );
    assert.equal(locationCalls, 0);
  } finally {
    locationUnavailable = false;
  }
});

test("cancelling an area lookup before or during the permission prompt never starts GPS", async () => {
  locationUnavailable = false;
  permission = "granted";
  const before = locationCalls;
  const beforePermissions = permissionCalls;
  const stopped = new AbortController();
  stopped.abort();
  await assert.rejects(locateOnce(stopped.signal), /cancelled/);
  assert.equal(permissionCalls, beforePermissions);
  let finish;
  permissionResult = new Promise((resolve) => {
    finish = resolve;
  });
  try {
    const controller = new AbortController();
    const result = locateOnce(controller.signal);
    for (
      let attempt = 0;
      attempt < 10 && permissionCalls === beforePermissions;
      attempt++
    )
      await Promise.resolve();
    assert.equal(permissionCalls, beforePermissions + 1);
    controller.abort();
    finish({ status: "granted" });
    await assert.rejects(result, /cancelled/);
    assert.equal(locationCalls, before);
  } finally {
    permissionResult = null;
  }
});
