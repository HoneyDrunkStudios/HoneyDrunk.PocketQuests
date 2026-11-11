import "./artwork-fixture.mjs";
import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { JSDOM } = require("jsdom");
const dom = new JSDOM("<!doctype html><html><body></body></html>");
global.window = dom.window;
global.document = dom.window.document;
global.HTMLElement = dom.window.HTMLElement;
global.ShadowRoot = dom.window.ShadowRoot;
global.IS_REACT_ACT_ENVIRONMENT = true;
const React = require("react");
const { act } = React;
const { createRoot } = require("react-dom/client");
const nativeWeb = require("react-native-web");
const ts = require("typescript");
let session, client, locate, calls, saved, journalActions, mapGesture;
process.env.EXPO_PUBLIC_APP_ENV = "development";
const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native")
    return {
      ...nativeWeb,
      PanResponder: {
        create: (handlers) => {
          mapGesture = handlers;
          return { panHandlers: {} };
        },
      },
    };
  if (name.endsWith("/places-client"))
    return { createPlacesClient: () => client };
  if (name === "./map-location") return { locateOnce: () => locate() };
  if (name.endsWith("/session-page"))
    return {
      Page: ({ children }) => React.createElement("main", null, children),
    };
  if (name === "expo-crypto")
    return { randomUUID: () => globalThis.crypto.randomUUID() };
  if (name.endsWith("/session"))
    return {
      useAccountSnapshot: () => session,
      useSessionActions: () => journalActions,
      useSessionStatus: () => ({ offline: false, busy: false }),
    };
  if (name === "expo-router")
    return {
      Link: ({ href, children, ...props }) =>
        React.createElement(
          nativeWeb.Text,
          {
            ...props,
            href:
              typeof href === "string"
                ? href
                : `/quest/discover/${href.params.categoryId}`,
          },
          children,
        ),
    };
  return load.call(this, name, ...rest);
};
for (const extension of [".ts", ".tsx"])
  require.extensions[extension] = (module, file) =>
    module._compile(
      ts.transpileModule(require("node:fs").readFileSync(file, "utf8"), {
        fileName: file,
        compilerOptions: {
          module: ts.ModuleKind.CommonJS,
          jsx: ts.JsxEmit.ReactJSX,
          target: ts.ScriptTarget.ES2022,
        },
      }).outputText,
      file,
    );
const { ThemeProvider } = require("@honeydrunk/ui-native");
const { pocketQuestsTheme } = require("../../src/shared/theme.ts");
const {
  ExplorationScreen,
} = require("../../src/features/progression/exploration-screen.tsx");
const Explore = require("../../src/app/explore.tsx").default;
const area = { id: "test", name: "Test town", latitude: 35, longitude: -82 };
const points = [
  {
    ...area,
    id: "node/1",
    name: "Test Park",
    kind: "Parks",
    categoryId: "c07",
    distanceMeters: 100,
    address: "",
  },
  {
    ...area,
    longitude: -82.001,
    id: "way/2",
    name: "Test Library",
    kind: "Libraries",
    categoryId: "c02",
    distanceMeters: 200,
    address: "Main Street",
  },
];
function reset() {
  calls = [];
  saved = [];
  journalActions = {
    readDiscoveries: async () => saved,
    saveDiscovery: async (request) => {
      const row = {
        ...request,
        revision: Number(request.expectedRevision) + 1,
        visitedAt: request.visited ? "2026-10-28T12:00:00Z" : null,
        createdAt: "2026-10-28T12:00:00Z",
        modifiedAt: "2026-10-28T12:00:00Z",
      };
      saved = [row, ...saved.filter((p) => p.id !== row.id)];
      return row;
    },
    removeDiscovery: async (request) => {
      saved = saved.filter((p) => p.id !== request.id);
      return { removed: true };
    },
  };
  session = {
    catalog: {
      categories: [
        { id: "c07", name: "Rest" },
        { id: "c02", name: "Learning" },
      ],
    },
    state: { categories: [], occurrences: [] },
  };
  client = {
    areas: async (q, signal) => {
      calls.push({ kind: "areas", q, signal });
      return [area];
    },
    points: async (center, signal) => {
      calls.push({ kind: "points", center, signal });
      return points;
    },
  };
  locate = async () => area;
}
function savedFixture() {
  return {
    ...points[0],
    id: "saved-1",
    placeKey: points[0].id,
    notes: "Private picnic plan",
    visitedAt: "2026-10-28T12:00:00Z",
    questOccurrenceId: null,
    revision: 1,
    createdAt: "2026-10-28T12:00:00Z",
    modifiedAt: "2026-10-28T12:00:00Z",
  };
}
async function loadArea(view) {
  await view.fill("Search area", "Test town");
  await view.click("Find an area");
  await view.click("Test town");
}
async function mount(Component) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  const view = {
    element,
    async render() {
      await act(async () =>
        root.render(
          React.createElement(
            ThemeProvider,
            { theme: pocketQuestsTheme },
            React.createElement(Component),
          ),
        ),
      );
    },
    button(name) {
      return [...element.querySelectorAll('[role="button"]')].find(
        (button) => button.textContent === name,
      );
    },
    async click(name) {
      const button = view.button(name);
      assert.ok(button, name);
      await act(async () => button.click());
    },
    async fill(label, value) {
      const input = element.querySelector(`[aria-label="${label}"]`);
      assert.ok(input, label);
      await act(async () => {
        Object.getOwnPropertyDescriptor(
          input.tagName === "TEXTAREA"
            ? window.HTMLTextAreaElement.prototype
            : window.HTMLInputElement.prototype,
          "value",
        ).set.call(input, value);
        input.dispatchEvent(new window.Event("input", { bubbles: true }));
      });
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
  await view.render();
  return view;
}

test("Explore loads a real-data map in-app only after an explicit area search", async () => {
  reset();
  const view = await mount(Explore);
  try {
    assert.equal(calls.length, 0);
    assert.doesNotMatch(view.element.textContent, /Google Maps/);
    await loadArea(view);
    assert.ok(view.element.querySelector('[data-testid="exploration-map"]'));
    assert.match(view.element.textContent, /2 places found/);
    await view.click("2. Test Library · Libraries");
    assert.ok(view.element.querySelector('a[href="/quest/discover/c02"]'));
    assert.match(view.element.textContent, /Main Street/);
    await view.click("Save place to journal");
    assert.match(view.element.textContent, /1 saved places/);
    await view.click("Test Library · Want to visit");
    await view.click("Remove saved place");
    assert.equal(saved.length, 1);
    await view.click("Confirm remove saved place");
    assert.match(view.element.textContent, /0 saved places/);
  } finally {
    await view.close();
  }
});
test("map moves do not search automatically; filters and zero results remain usable", async () => {
  reset();
  const view = await mount(ExplorationScreen);
  try {
    await loadArea(view);
    await act(async () => {
      mapGesture.onPanResponderGrant({ nativeEvent: { touches: [] } });
      mapGesture.onPanResponderMove(
        { nativeEvent: { touches: [] } },
        { dx: -160, dy: 0 },
      );
      mapGesture.onPanResponderRelease();
    });
    assert.equal(calls.length, 2);
    await view.click("Libraries");
    assert.equal(view.button("1. Test Park · Parks"), undefined);
    assert.ok(view.button("1. Test Library · Libraries"));
    client.points = async () => [];
    await view.click("Search this map area");
    assert.match(view.element.textContent, /0 places found/);
    assert.match(view.element.textContent, /No places match here/);
  } finally {
    await view.close();
  }
});
test("provider failure keeps the existing map and explicit location denial offers manual search", async () => {
  reset();
  locate = async () => {
    throw new Error("Location access is off. Search for a town instead.");
  };
  const view = await mount(ExplorationScreen);
  try {
    await view.click("Use my location");
    assert.match(view.element.textContent, /Location access is off/);
    assert.equal(calls.length, 0);
    await loadArea(view);
    client.points = async () => {
      throw new Error("Provider unavailable");
    };
    await view.click("Refresh places");
    assert.match(view.element.textContent, /Provider unavailable/);
    assert.ok(view.button("1. Test Park · Parks"));
  } finally {
    await view.close();
  }
});
test("pending lookup prevents duplicate requests; late results cannot repopulate an unmounted screen", async () => {
  reset();
  let finish, signalSeen;
  client.points = (center, signal) => {
    signalSeen = signal;
    return new Promise((resolve) => {
      finish = resolve;
    });
  };
  const view = await mount(ExplorationScreen);
  await view.click("Use my location");
  await view.click("Use my location");
  await view.close();
  assert.equal(signalSeen.aborted, true);
  const next = await mount(ExplorationScreen);
  try {
    await act(async () => finish(points));
    assert.doesNotMatch(next.element.textContent, /Test Park/);
  } finally {
    await next.close();
  }
});

test("account journal reloads after remount and saves notes, visit status and accepted quest link", async () => {
  reset();
  session.state.occurrences = [
    {
      status: "Active",
      occurrence: { id: "quest-1", quest: { title: "Read at the library" } },
    },
  ];
  const first = await mount(ExplorationScreen);
  await loadArea(first);
  await first.click("2. Test Library · Libraries");
  await first.click("Save place to journal");
  await first.close();
  const view = await mount(ExplorationScreen);
  try {
    await view.click("Saved places");
    assert.match(view.element.textContent, /1 saved places/);
    await view.click("Test Library · Want to visit");
    await view.fill("Notes for Test Library", "Look for a new book");
    await view.click("Want to visit — mark visited");
    await view.click("Link an accepted quest");
    await view.click("Read at the library");
    await view.click("Save place details");
    assert.equal(saved[0].notes, "Look for a new book");
    assert.equal(saved[0].questOccurrenceId, "quest-1");
    assert.ok(saved[0].visitedAt);
    await view.click("Visited");
    assert.ok(view.button("Test Library · Visited"));
  } finally {
    await view.close();
  }
});
test("failed journal edits keep notes and require reload before another write", async () => {
  reset();
  const view = await mount(ExplorationScreen);
  try {
    await loadArea(view);
    await view.click("1. Test Park · Parks");
    await view.click("Save place to journal");
    await view.click("Test Park · Want to visit");
    await view.fill("Notes for Test Park", "Keep this draft");
    journalActions.saveDiscovery = async () => {
      throw new Error("This place changed.");
    };
    await view.render();
    await view.click("Save place details");
    assert.match(view.element.textContent, /Reload saved places before/);
    assert.equal(
      view.element.querySelector("textarea").value,
      "Keep this draft",
    );
    assert.equal(
      view.button("Save place details").getAttribute("aria-disabled"),
      "true",
    );
  } finally {
    await view.close();
  }
});

test("a retained saved place reopens in-app without geocoding, GPS or writes and survives empty nearby results", async () => {
  reset();
  saved = [savedFixture()];
  const before = JSON.stringify(saved);
  locate = async () => {
    throw new Error("GPS must not be requested");
  };
  journalActions.saveDiscovery = async () => {
    throw new Error("Read-only map must not save");
  };
  const view = await mount(ExplorationScreen);
  try {
    await view.click("Saved places");
    await view.click("Test Park · Visited");
    await view.click("Show on map");
    assert.equal(calls.length, 0);
    assert.ok(view.element.querySelector('[data-testid="exploration-map"]'));
    assert.match(view.element.textContent, /Saved · Visited/);
    assert.equal(
      view.element.querySelector('[data-testid="map-center-indicator"]'),
      null,
    );
    assert.doesNotMatch(view.element.textContent, /Private picnic plan/);
    assert.ok(
      [...view.element.querySelectorAll('[role="button"]')].some((b) =>
        b.getAttribute("aria-label")?.includes("saved, visited"),
      ),
    );
    await act(async () => {
      mapGesture.onPanResponderGrant({ nativeEvent: { touches: [] } });
      mapGesture.onPanResponderMove(
        { nativeEvent: { touches: [] } },
        { dx: -160, dy: 0 },
      );
      mapGesture.onPanResponderRelease();
    });
    assert.ok(
      view.element.querySelector('[data-testid="map-center-indicator"]'),
    );
    client.points = async () => [];
    await view.click("Find nearby places");
    assert.ok(view.button("1. Test Park · Discovered"));
    await view.click("Show saved places only");
    assert.ok(view.button("1. Test Park · Discovered"));
    assert.equal(JSON.stringify(saved), before);
  } finally {
    await view.close();
  }
});

test("opening a saved map cannot silently discard edited notes", async () => {
  reset();
  saved = [savedFixture()];
  const view = await mount(ExplorationScreen);
  try {
    await view.click("Saved places");
    await view.click("Test Park · Visited");
    await view.fill("Notes for Test Park", "Unsaved thought");
    assert.equal(
      view.button("Show on map").getAttribute("aria-disabled"),
      "true",
    );
    await view.click("Show on map");
    assert.equal(
      view.element.querySelector("textarea").value,
      "Unsaved thought",
    );
    await view.click("Reset place edits");
    assert.equal(
      view.element.querySelector("textarea").value,
      "Private picnic plan",
    );
    await view.click("Show on map");
    assert.ok(view.element.querySelector('[data-testid="exploration-map"]'));
    assert.equal(saved[0].notes, "Private picnic plan");
  } finally {
    await view.close();
  }
});

test("reopening a saved place cancels an in-flight search and ignores its late result", async () => {
  reset();
  saved = [savedFixture()];
  let finish, signal;
  client.points = (_, nextSignal) => {
    signal = nextSignal;
    return new Promise((resolve) => {
      finish = resolve;
    });
  };
  const view = await mount(ExplorationScreen);
  try {
    await loadArea(view);
    await view.click("Saved places");
    await view.click("Test Park · Visited");
    await view.click("Show on map");
    assert.equal(signal.aborted, true);
    await act(async () =>
      finish([{ ...points[1], name: "Late search result" }]),
    );
    assert.match(view.element.textContent, /Saved · Visited/);
    assert.doesNotMatch(view.element.textContent, /Late search result/);
  } finally {
    await view.close();
  }
});
