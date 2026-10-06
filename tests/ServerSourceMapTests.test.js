"use strict";

const assert = require("node:assert/strict");
const test = require("node:test");
const { normalizeSourceMapSources, resolveEntryPoint } = require("../templates/TypescriptBridge.Template/content/server.js");

// A debug map is stored three directories below the project root.
// The browser receives it at the HTTP root, so its sources must be
// expressed relative to the project rather than the physical map file.
test("debug source paths resolve under the project webRoot", () => {
    const sourceMap = {
        sources: ["../../../ts/src/app.ts", "../../../ts/node_modules/example/index.ts"],
        sourcesContent: ["console.log('app')", "export const value = 1"],
    };

    normalizeSourceMapSources(sourceMap);

    assert.deepEqual(sourceMap.sources, ["ts/src/app.ts", "ts/node_modules/example/index.ts"]);
    assert.deepEqual(sourceMap.sourcesContent, ["console.log('app')", "export const value = 1"]);
});

// Files outside the generated project are not represented as local
// source paths under webRoot.
test("external source paths are preserved", () => {
    const sourceMap = { sources: ["../../../../../external.ts"] };

    normalizeSourceMapSources(sourceMap);

    assert.deepEqual(sourceMap.sources, ["../../../../../external.ts"]);
});

// F5 chooses the configured application source; tests keep their own source.
test("F5 selects a named application entrypoint", () => {
    const selected = resolveEntryPoint({
        run: { entrypoint: "chart" },
        entrypoints: { main: { source: "src/app.ts" }, chart: { source: "src/chart.ts" } },
    });
    assert.ok(selected.endsWith(require("node:path").join("ts", "src", "chart.ts")));
});

test("test mode ignores named application entrypoints", () => {
    const selected = resolveEntryPoint({
        isTestProject: true,
        run: { entrypoint: "chart" },
        entrypoints: { chart: { source: "src/chart.ts" } },
        testEntrypoint: "tests/index.ts",
    });
    assert.ok(selected.endsWith(require("node:path").join("ts", "tests", "index.ts")));
});
