import assert from "node:assert/strict";
import { test } from "node:test";
import { JSDOM } from "jsdom";
import { activeRouteSelector } from "../../e2e/active-route.mjs";

test("active-route counts exclude both retained-screen accessibility markers", () => {
  const dom = new JSDOM(`
    <section aria-hidden="true"><div role="progressbar" id="native-retained"></div></section>
    <section inert><div role="progressbar" id="web-retained"></div></section>
    <section aria-hidden="false"><div role="progressbar" id="active"></div></section>
    <div role="progressbar" inert id="inert-self"></div>
    <div role="progressbar" aria-hidden="true" id="hidden-self"></div>
  `);
  try {
    const active = [
      ...dom.window.document.querySelectorAll('[role="progressbar"]'),
    ].filter((element) => element.matches(activeRouteSelector));
    assert.deepEqual(
      active.map((element) => element.id),
      ["active"],
    );
    const retained = dom.window.document.querySelector("[inert]");
    retained.removeAttribute("inert");
    assert.equal(retained.firstElementChild.matches(activeRouteSelector), true);
    retained.setAttribute("inert", "");
    assert.equal(
      retained.firstElementChild.matches(activeRouteSelector),
      false,
    );
  } finally {
    dom.window.close();
  }
});
