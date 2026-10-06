// TypescriptBridge debug HTTP server.
//
// Responsibilities:
//   1. Parse --f5 (do not launch a browser in F5 mode).
//   2. Read config.json and ts/tsconfig.json.
//   3. Validate the port from config.json.
//   4. Clean and recreate obj/TypescriptBridge/debug/.
//   5. Locate esbuild.
//   6. Run esbuild synchronously on ts/src/app.ts.
//   7. Write debug.js + debug.js.map + index.html.
//   8. Start HTTP on 127.0.0.1:<port>.
//   9. Wait for SIGINT.
//  10. On SIGINT: stop the server and (direct mode) kill the browser.

"use strict";

const http = require("http");
const fs = require("fs");
const path = require("path");
const { spawnSync, spawn } = require("child_process");

const projectRoot = __dirname;
const configPath = path.join(projectRoot, "config.json");
const tsconfigPath = path.join(projectRoot, "ts", "tsconfig.json");

const objRoot = path.join(projectRoot, "obj", "TypescriptBridge");
const debugRoot = path.join(objRoot, "debug");
const debugJsPath = path.join(debugRoot, "debug.js");
const debugMapPath = path.join(debugRoot, "debug.js.map");
const debugHtmlPath = path.join(debugRoot, "index.html");

const stableEsbuildPath = path.join(
    objRoot,
    "tools",
    "esbuild",
    "win-x64",
    "esbuild.exe"
);

const legacyEsbuildPath = path.join(
    projectRoot,
    "tools",
    "esbuild",
    "win-x64",
    "esbuild.exe"
);

const isF5Mode = process.argv.includes("--f5");

function fatal(message) {
    process.stderr.write("TypescriptBridge: " + message + "\n");
    process.exit(1);
}

function info(message) {
    process.stderr.write("TypescriptBridge: " + message + "\n");
}

function readJson(filePath, description) {
    if (!fs.existsSync(filePath)) {
        fatal(description + " not found: " + filePath);
    }
    let raw;
    try {
        raw = fs.readFileSync(filePath, "utf8");
    } catch (err) {
        fatal("failed to read " + description + ": " + err.message);
    }
    try {
        // Windows PowerShell can save UTF-8 JSON with a leading BOM.
        // Strip it before parsing so a valid configuration still loads.
        return JSON.parse(raw.replace(/^\uFEFF/, ""));
    } catch (err) {
        fatal("invalid JSON in " + description + ": " + err.message);
    }
}

function cleanDirectory(dir) {
    if (fs.existsSync(dir)) {
        try {
            fs.rmSync(dir, { recursive: true, force: true });
        } catch (err) {
            fatal("failed to clean " + dir + ": " + err.message);
        }
    }
    fs.mkdirSync(dir, { recursive: true });
}

function locateEsbuild() {
    if (fs.existsSync(stableEsbuildPath)) {
        return stableEsbuildPath;
    }
    if (fs.existsSync(legacyEsbuildPath)) {
        return legacyEsbuildPath;
    }
    const envPath = process.env.TS_HANDLER_ESBUILD_PATH;
    if (envPath && fs.existsSync(envPath)) {
        return envPath;
    }
    fatal("esbuild not found. Looked at:\n  " + stableEsbuildPath + "\n  " + legacyEsbuildPath);
}

function buildHtml() {
    return "<!DOCTYPE html>\n<html>\n<head>\n    <meta charset=\"utf-8\" />\n    <title>TypescriptBridge Debug</title>\n</head>\n<body>\n    <h1>TypescriptBridge Debug Session</h1>\n    <script src=\"debug.js\"></script>\n</body>\n</html>\n";
}

function locateBrowser(browser) {
    const candidates = [];
    const pf = process.env["ProgramFiles"] || "C:\\Program Files";
    const pf86 = process.env["ProgramFiles(x86)"] || "C:\\Program Files (x86)";
    const localAppData = process.env["LOCALAPPDATA"] || "";

    if (browser === "edge") {
        candidates.push(
            path.join(pf86, "Microsoft", "Edge", "Application", "msedge.exe"),
            path.join(pf, "Microsoft", "Edge", "Application", "msedge.exe")
        );
    } else if (browser === "chrome") {
        candidates.push(
            path.join(pf, "Google", "Chrome", "Application", "chrome.exe"),
            path.join(pf86, "Google", "Chrome", "Application", "chrome.exe")
        );
        if (localAppData) {
            candidates.push(path.join(localAppData, "Google", "Chrome", "Application", "chrome.exe"));
        }
    } else {
        fatal("unknown browser: " + browser);
    }

    for (const candidate of candidates) {
        if (fs.existsSync(candidate)) {
            return candidate;
        }
    }
    fatal(browser + " is not installed on this machine.");
}

// Select only the application entry requested for F5; test mode has its own source.
function resolveEntryPoint(config) {
    const selected = config.run?.entrypoint ?? "main";
    const source = config.isTestProject
        ? (config.testEntrypoint ?? "tests/index.ts")
        : config.entrypoints
            ? config.entrypoints[selected]?.source
            : (config.entrypoint ?? "src/app.ts");
    if (typeof source !== "string" || !source.trim()) {
        throw new Error("config.json: unknown run.entrypoint: " + selected);
    }
    const entryPoint = path.resolve(projectRoot, "ts", source);
    const tsRoot = path.join(projectRoot, "ts") + path.sep;
    if (!entryPoint.startsWith(tsRoot)) throw new Error("config.json: entrypoint must be under ts/.");
    return entryPoint;
}

function loadConfig() {
    const config = readJson(configPath, "config.json");
    if (!config || typeof config !== "object") fatal("config.json must be an object.");
    if (!config.run || typeof config.run !== "object") fatal("config.json: missing 'run' section.");
    const browser = config.run.browser;
    if (browser !== "edge" && browser !== "chrome") fatal("config.json: run.browser must be 'edge' or 'chrome'.");
    const port = config.run.port;
    if (typeof port !== "number" || port < 1 || port > 65535) fatal("config.json: run.port must be 1-65535.");
    if (!config.bundle || typeof config.bundle !== "object") fatal("config.json: missing 'bundle' section.");
    const format = config.bundle.format;
    if (format !== "iife" && format !== "esm" && format !== "cjs") fatal("config.json: bundle.format invalid.");
    const entryPoint = resolveEntryPoint(config);
    return { browser, port, format, entryPoint, isTestProject: config.isTestProject === true };
}

function loadTsTarget() {
    const tsconfig = readJson(tsconfigPath, "tsconfig.json");
    if (!tsconfig.compilerOptions || typeof tsconfig.compilerOptions !== "object") fatal("tsconfig.json: missing 'compilerOptions'.");
    const target = tsconfig.compilerOptions.target;
    if (typeof target !== "string" || target.length === 0) fatal("tsconfig.json: missing 'compilerOptions.target'.");
    return target;
}

// Source maps are served from the HTTP root, while esbuild writes source
// paths relative to the map's physical directory under obj/. Rewrite
// project sources so the debugger resolves them against webRoot.
function normalizeSourceMapSources(sourceMap) {
    sourceMap.sources = sourceMap.sources.map((source) => {
        const absolute = path.resolve(debugRoot, source);
        const relative = path.relative(projectRoot, absolute);
        if (relative === ".." || relative.startsWith(".." + path.sep) || path.isAbsolute(relative)) {
            return source;
        }
        return relative.split(path.sep).join("/");
    });
    return sourceMap;
}

function prepareDebugArtifacts(target, format, appEntryPoint, isTestProject) {
    if (!fs.existsSync(appEntryPoint)) {
        fatal("entry point not found: " + appEntryPoint);
    }
    cleanDirectory(debugRoot);
    const esbuild = locateEsbuild();
    const esbuildArgs = [
        appEntryPoint,
        "--bundle",
        "--format=" + format,
        "--target=" + target,
        "--outfile=" + debugJsPath,
        "--define:__TYPESCRIPT_BRIDGE_BUILD_MODE__=\"DEBUG\"",
        "--define:__TYPESCRIPT_BRIDGE_PROJECT_STATUS__=\"Application\"",
        `--define:__TYPESCRIPT_BRIDGE_IS_TEST_PROJECT__=${isTestProject}`,
        "--keep-names",
        "--sourcemap=linked",
        "--sources-content=true",
        "--log-level=warning"
    ];
    info("running esbuild...");
    const result = spawnSync(esbuild, esbuildArgs, { cwd: projectRoot, stdio: "inherit" });
    if (result.error) fatal("failed to start esbuild: " + result.error.message);
    if (result.status !== 0) fatal("esbuild failed with exit code " + result.status);
    if (!fs.existsSync(debugJsPath)) fatal("esbuild did not produce " + debugJsPath);
    if (!fs.existsSync(debugMapPath)) fatal("esbuild did not produce " + debugMapPath);
    const sourceMap = JSON.parse(fs.readFileSync(debugMapPath, "utf8"));
    fs.writeFileSync(debugMapPath, JSON.stringify(normalizeSourceMapSources(sourceMap)), "utf8");
    fs.writeFileSync(debugHtmlPath, buildHtml(), "utf8");
    info("debug artifacts prepared at " + debugRoot);
}

function mimeType(ext) {
    switch (ext.toLowerCase()) {
        case ".html": return "text/html; charset=utf-8";
        case ".js":   return "application/javascript; charset=utf-8";
        case ".map":  return "application/json; charset=utf-8";
        case ".ts":   return "application/typescript; charset=utf-8";
        case ".json": return "application/json; charset=utf-8";
        case ".css":  return "text/css; charset=utf-8";
        default:      return "application/octet-stream";
    }
}

function startServer(port) {
    const server = http.createServer((req, res) => {
        let urlPath;
        try {
            urlPath = decodeURIComponent(req.url.split("?")[0]);
        } catch {
            res.writeHead(400); res.end("Bad request"); return;
        }
        if (urlPath === "/" || urlPath.length === 0) urlPath = "/index.html";
        const requested = path.resolve(debugRoot, "." + urlPath);
        if (requested !== debugRoot && !requested.startsWith(debugRoot + path.sep)) {
            res.writeHead(403); res.end("Forbidden"); return;
        }
        fs.readFile(requested, (err, data) => {
            if (err) { res.writeHead(404); res.end("Not found"); return; }
            const mime = mimeType(path.extname(requested));
            res.writeHead(200, { "Content-Type": mime });
            res.end(data);
        });
    });

    return new Promise((resolve, reject) => {
        server.on("error", (err) => {
            if (err.code === "EADDRINUSE") {
                reject(new Error("port " + port + " is already in use."));
            } else {
                reject(err);
            }
        });
        server.listen(port, "127.0.0.1", () => {
            info("HTTP server listening on http://127.0.0.1:" + port + "/");
            resolve(server);
        });
    });
}

function launchBrowser(browserName, url, userDataDir) {
    if (!fs.existsSync(userDataDir)) fs.mkdirSync(userDataDir, { recursive: true });
    const browserPath = locateBrowser(browserName);
    info("launching " + browserName + " at " + url);
    const args = ["--user-data-dir=" + userDataDir, "--no-first-run", "--no-default-browser-check", url];
    const child = spawn(browserPath, args, { detached: true, stdio: "ignore" });
    child.unref();
    return child.pid;
}

function killProcessTree(pid) {
    if (!pid) return;
    try {
        spawnSync("taskkill", ["/T", "/F", "/PID", String(pid)], { stdio: "ignore" });
    } catch { /* ignore */ }
}

function uniqueProfileDir() {
    const now = new Date();
    const ts = now.getUTCFullYear().toString() +
        String(now.getUTCMonth() + 1).padStart(2, "0") +
        String(now.getUTCDate()).padStart(2, "0") + "-" +
        String(now.getUTCHours()).padStart(2, "0") +
        String(now.getUTCMinutes()).padStart(2, "0") +
        String(now.getUTCSeconds()).padStart(2, "0");
    const guid8 = Math.random().toString(16).slice(2, 10);
    return path.join(objRoot, "browser-profile", ts + "-" + guid8);
}

async function main() {
    const config = loadConfig();
    const target = loadTsTarget();
    prepareDebugArtifacts(target, config.format, config.entryPoint, config.isTestProject);

    let server;
    try {
        server = await startServer(config.port);
    } catch (err) {
        fatal(err.message);
    }

    let browserPid = null;
    if (!isF5Mode) {
        const profileDir = uniqueProfileDir();
        const url = "http://127.0.0.1:" + config.port + "/index.html";
        browserPid = launchBrowser(config.browser, url, profileDir);
    } else {
        info("running in F5 mode; browser launch is owned by launch.json");
    }

    const shutdown = (signal) => {
        info("received " + signal + ", shutting down");
        try { server.close(); } catch { /* ignore */ }
        if (browserPid) killBrowserSafely(browserPid);
        process.exit(0);
    };

    process.on("SIGINT", () => shutdown("SIGINT"));
    process.on("SIGTERM", () => shutdown("SIGTERM"));
}

function killBrowserSafely(pid) {
    killProcessTree(pid);
}

if (require.main === module) {
    main().catch((err) => {
        fatal(err.message || String(err));
    });
}

module.exports = { normalizeSourceMapSources, resolveEntryPoint };
