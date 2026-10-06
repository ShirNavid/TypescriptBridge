// Sample TypeScript entry point.
// Edit this file and rebuild. The compiled JavaScript is embedded into Bridge.cs.

import { BUILD_MODE } from "./typescript-bridge/typescript-bridge";

// BUILD_MODE is injected by TypescriptBridge based on the build configuration.
// It is a compile-time constant: "DEBUG" in Debug builds, "RELEASE" in Release.
if (BUILD_MODE === "DEBUG") {
    console.log("TypescriptBridge: Debug mode");
} else {
    console.log("TypescriptBridge: Release mode");
}

export function greet(name: string): string {
    return `Hello, ${name}!`;
}

export function add(a: number, b: number): number {
    return a + b;
}

console.log(greet("TypescriptBridge"));
console.log("2 + 3 =", add(2, 3));
