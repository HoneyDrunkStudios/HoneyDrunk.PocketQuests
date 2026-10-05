import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const ts = require("typescript");
const wire = JSON.parse(
  fs.readFileSync(
    new URL("../../../../contracts/wire-fixtures.json", import.meta.url),
    "utf8",
  ),
);
let session, inputs, buttons;
const native = ({ children }) => React.createElement("div", null, children);
const original = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native") return { Text: native, View: native };
  if (name === "expo-crypto") return { randomUUID: () => "new-series" };
  if (name.endsWith("/session")) return { useSession: () => session };
  if (name.endsWith("/planned-preview")) return { PlannedPreview: () => null };
  if (name.endsWith("/shared/ui"))
    return {
      Label: native,
      styles: {},
      Input: (p) => {
        inputs.push(p);
        return React.createElement("input", {
          value: p.value,
          "aria-label": p.accessibilityLabel,
          onInput: (e) => p.onChangeText(e.currentTarget.value),
          onChange: () => {},
        });
      },
      Button: (p) => {
        buttons.push(p);
        return React.createElement(
          "button",
          { disabled: p.disabled, onClick: p.onPress },
          p.title,
        );
      },
    };
  return original.call(this, name, ...rest);
};
for (const ext of [".ts", ".tsx"])
  require.extensions[ext] = (module, file) =>
    module._compile(
      ts.transpileModule(fs.readFileSync(file, "utf8"), {
        compilerOptions: {
          module: ts.ModuleKind.CommonJS,
          jsx: ts.JsxEmit.ReactJSX,
          target: ts.ScriptTarget.ES2022,
        },
      }).outputText,
      file,
    );
const { SeriesEditor } = require("../../src/features/quests/series-editor.tsx");
function render(anchor) {
  inputs = [];
  buttons = [];
  const sent = [];
  const initial = {
    id: "series",
    anchor,
    cadence: "Months",
    interval: 1,
    plannedTime: "09:00",
    version: 3,
    quest: wire.catalog.quests[0],
  };
  session = {
    state: {
      ...wire.state,
      today: "2026-10-05",
      schedule: { ...wire.state.schedule, series: [initial] },
    },
    command: async (c) => sent.push(c),
    busy: false,
    pending: false,
  };
  const html = renderToStaticMarkup(
    React.createElement(SeriesEditor, {
      quest: initial.quest,
      initial,
      onClose() {},
    }),
  );
  return {
    initial,
    sent,
    html,
    date: inputs.find(
      (p) => p.accessibilityLabel === "First delivery YYYY-MM-DD",
    ),
    save: buttons.find((p) => p.title === "Save future schedule"),
  };
}
test("saving an existing future monthly series retains its delivery anchor and revision", async () => {
  const view = render("2026-10-31");
  assert.equal(view.date.value, "2026-10-31");
  assert.equal(view.save.disabled, false);
  await view.save.onPress();
  assert.equal(view.sent[0].dueDate, "2026-10-31");
  assert.equal(view.sent[0].expectedRevision, 3);
  assert.equal(view.sent[0].seriesId, "series");
});
test("editing a series with a past anchor requires an explicit replacement date", () => {
  const view = render("2026-09-30");
  assert.equal(view.date.value, "");
  assert.equal(view.save.disabled, true);
  assert.match(view.html, /Choose.*delivery date/);
});

test("a real mounted time-only edit retains the monthly date and cadence through React state updates", async () => {
  const { JSDOM } = require("jsdom");
  const dom = new JSDOM("<div id='root'></div>");
  global.window = dom.window;
  global.document = dom.window.document;
  global.IS_REACT_ACT_ENVIRONMENT = true;
  const { createRoot } = require("react-dom/client");
  const root = createRoot(document.getElementById("root"));
  const view = render("2026-10-31");
  try {
    await React.act(async () =>
      root.render(
        React.createElement(SeriesEditor, {
          quest: view.initial.quest,
          initial: view.initial,
          onClose() {},
        }),
      ),
    );
    const time = document.querySelector(
      '[aria-label="Recurring planned time HH:mm"]',
    );
    await React.act(async () => {
      time.value = "17:30";
      time.dispatchEvent(new window.Event("input", { bubbles: true }));
    });
    await React.act(async () =>
      [...document.querySelectorAll("button")]
        .find((button) => button.textContent === "Save future schedule")
        .click(),
    );
    assert.equal(view.sent.length, 1);
    assert.equal(view.sent[0].plannedTime, "17:30");
    assert.equal(view.sent[0].dueDate, "2026-10-31");
    assert.equal(view.sent[0].cadence, "Months");
    assert.equal(view.sent[0].interval, 1);
    assert.equal(view.sent[0].expectedRevision, 3);
  } finally {
    await React.act(async () => root.unmount());
    dom.window.close();
  }
});
