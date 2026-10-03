import test from "node:test";
import { strict as assert } from "node:assert";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const ts = require("typescript");
// Render native primitives through the existing web adapter, without a new test dependency.
const nativeWeb = require("react-native-web");
const Module = require("node:module");
const load = Module._load;
Module._load = function (name, ...rest) {
  return name === "react-native" ? nativeWeb : load.call(this, name, ...rest);
};
for (const extension of [".ts", ".tsx"])
  require.extensions[extension] = (module, file) => {
    const source = require("node:fs").readFileSync(file, "utf8");
    const output = ts.transpileModule(source, {
      compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        jsx: ts.JsxEmit.ReactJSX,
        target: ts.ScriptTarget.ES2022,
      },
    }).outputText;
    module._compile(output, file);
  };
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const { foundation, resolveTheme } = require("@honeydrunk/ui-tokens");
const {
  ThemeProvider,
  Button,
  Card,
  Input,
  Label,
  Notice,
  createStyles,
  buttonAppearance,
} = require("@honeydrunk/ui-native");
const { pocketQuestsTheme } = require("../src/theme.ts");
const alternate = {
  ...foundation,
  colors: {
    text: "#17221A",
    muted: "#405347",
    background: "#FFFFFF",
    surface: "#F1F7F2",
    primary: "#23623E",
    onPrimary: "#FFFFFF",
    border: "#657B6C",
    accent: "#765321",
    danger: "#9A1F30",
    input: "#FFFFFF",
  },
};
const render = (theme, child) =>
  renderToStaticMarkup(React.createElement(ThemeProvider, { theme }, child));
test("native primitives require an explicit app theme", () => {
  assert.throws(
    () => renderToStaticMarkup(React.createElement(Label, null, "Hello")),
    /app-supplied ThemeProvider/,
  );
});
test("two app themes render independently and preserve input accessibility and values", () => {
  for (const theme of [pocketQuestsTheme, alternate]) {
    const html = render(
      theme,
      React.createElement(
        Card,
        { accessibilityLabel: "Profile" },
        React.createElement(Input, {
          accessibilityLabel: "Name",
          value: "Ada",
          editable: false,
        }),
        React.createElement(Label, null, "Hello"),
      ),
    );
    assert.match(html, /aria-label="Profile"/);
    assert.match(html, /aria-label="Name"/);
    assert.match(html, /value="Ada"/);
    assert.match(html, /read[oO]nly/);
    assert.equal(createStyles(theme).text.color, theme.colors.text);
    assert.equal(
      createStyles(theme).card.backgroundColor,
      theme.colors.surface,
    );
  }
  assert.notEqual(
    render(pocketQuestsTheme, React.createElement(Label, null, "Hello")),
    render(alternate, React.createElement(Label, null, "Hello")),
  );
});
test("disabled actions retain semantics, touch target and state precedence in each theme", () => {
  for (const theme of [pocketQuestsTheme, alternate]) {
    const html = render(
      theme,
      React.createElement(Button, {
        title: "Save",
        onPress: () => assert.fail("disabled action fired"),
        disabled: true,
      }),
    );
    assert.match(html, /aria-disabled="true"/);
    assert.match(html, /role="button"/);
    assert.match(html, /Save/);
    assert.ok(theme.controls.minHeight >= 48);
    assert.equal(
      buttonAppearance(theme, false, true, true).opacity,
      theme.controls.disabledOpacity,
    );
    assert.equal(
      buttonAppearance(theme, true, false, true).color,
      theme.colors.primary,
    );
  }
});
test("error notices expose a polite announcement", () => {
  assert.match(
    render(alternate, React.createElement(Notice, null, "Try again")),
    /aria-live="polite"/,
  );
});
function luminance(hex) {
  const rgb = hex
    .slice(1)
    .match(/../g)
    .map((v) => parseInt(v, 16) / 255)
    .map((v) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722;
}
test("Pocket Quests body, muted, error and button text meet AA contrast", () => {
  const c = pocketQuestsTheme.colors;
  for (const [fg, bg] of [
    [c.text, c.background],
    [c.text, c.surface],
    [c.muted, c.surface],
    [c.danger, c.surface],
    [c.onPrimary, c.primary],
    [c.text, c.input],
    [c.accent, c.surface],
  ]) {
    const a = luminance(fg),
      b = luminance(bg);
    assert.ok(
      (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05) >= 4.5,
      fg + " on " + bg,
    );
  }
});
test("product control boundaries and focus indicators meet non-text contrast on their adjacent surfaces", () => {
  const c = resolveTheme(pocketQuestsTheme).colors;
  for (const fg of [c.border, c.focus]) {
    for (const bg of [c.background, c.surface, c.input]) {
      const a = luminance(fg),
        b = luminance(bg);
      assert.ok(
        (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05) >= 3,
        `${fg} on ${bg}`,
      );
    }
  }
});
