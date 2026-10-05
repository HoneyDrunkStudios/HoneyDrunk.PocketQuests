import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import "./provider-harness.cjs";
const require = createRequire(import.meta.url);
const { decodeResponse } = require("../../src/api/decode.ts");
const wire = require("../../../../contracts/wire-fixtures.json");
const examples = {
  "POST /api/sync-anchor": {
    id: "id",
    deviceId: "device",
    bootId: "boot",
    serverUtc: "2026-10-05T00:00:00Z",
    deviceUtc: "2026-10-05T00:00:00Z",
    recordedTimeFloor: null,
  },
  "GET /api/catalog": wire.catalog,
  "POST /api/profile": wire.state,
  "GET /api/state": wire.state,
  "POST /api/commands": wire.state,
  "GET /api/planning/clock": {
    requested: "09:00",
    resolved: "09:00",
    instant: "2026-10-05T09:00:00Z",
    adjusted: false,
    repeated: false,
  },
  "GET /api/planning/zone": {
    previousZone: "UTC",
    zone: "America/New_York",
    deadlines: [],
  },
};
test("all seven current JSON operations decode their actual compatible response shapes without coercion", () => {
  for (const [operation, value] of Object.entries(examples)) {
    assert.equal(decodeResponse(operation, value), value);
    assert.throws(() => decodeResponse(operation, {}), /unreadable response/);
    assert.throws(() => decodeResponse(operation, null), /unreadable response/);
  }
});
test("network decoding rejects missing nested collections, wrong output primitives and enum drift", () => {
  for (const corrupt of [
    (s) => delete s.categories,
    (s) => delete s.profile.customSkills,
    (s) => delete s.schedule.series,
    (s) => {
      s.occurrences[0].status = "Unexpected";
    },
    (s) => {
      s.overallXp = "10";
    },
    (s) => delete s.occurrences[0].occurrence.quest.skills,
    (s) => {
      s.occurrences[0].occurrence.quest.description = { invalid: "type" };
    },
  ]) {
    const value = structuredClone(wire.state);
    corrupt(value);
    assert.throws(
      () => decodeResponse("GET /api/state", value),
      /unreadable response/,
    );
  }
});
