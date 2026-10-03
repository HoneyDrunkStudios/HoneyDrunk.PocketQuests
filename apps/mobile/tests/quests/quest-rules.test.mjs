import test from "node:test";
import { strict as assert } from "node:assert";
import { baseXp, eligible } from "../../src/features/quests/quest-rules.ts";

test("quest previews preserve the approved rewards for every rank and effort", () => {
  const expected = [
    ["F", 10, 80, 800],
    ["E", 13, 104, 1040],
    ["D", 17, 136, 1360],
    ["C", 22, 176, 1760],
    ["B", 28, 224, 2240],
    ["A", 35, 280, 2800],
    ["S", 43, 344, 3440],
  ];
  for (const [rank, small, medium, large] of expected) {
    assert.equal(baseXp(rank, "Small"), small);
    assert.equal(baseXp(rank, "Medium"), medium);
    assert.equal(baseXp(rank, "Large"), large);
  }
});

test("category eligibility switches exactly at each rank boundary", () => {
  for (const [rank, level] of [["F", 1], ["E", 5], ["D", 10], ["C", 20], ["B", 35], ["A", 50], ["S", 70]]) {
    const quest = { rank, categoryId: "c01", skills: [] };
    assert.equal(eligible(quest, { categories: [{ id: "c01", level: level - 1 }], skills: [] }), false);
    assert.equal(eligible(quest, { categories: [{ id: "c01", level }], skills: [] }), true);
  }
});

test("all selected skills must qualify, including zero-weight skills", () => {
  const quest = {
    rank: "D", categoryId: "c01",
    skills: [{ id: "s01", basisPoints: 10000 }, { id: "s02", basisPoints: 0 }],
  };
  const state = {
    categories: [{ id: "c01", level: 99 }],
    skills: [{ id: "s01", level: 10 }, { id: "s02", level: 9 }],
  };
  assert.equal(eligible(quest, state), false);
  state.skills[1].level = 10;
  assert.equal(eligible(quest, state), true);
  state.skills.pop();
  assert.equal(eligible(quest, state), false);
});
