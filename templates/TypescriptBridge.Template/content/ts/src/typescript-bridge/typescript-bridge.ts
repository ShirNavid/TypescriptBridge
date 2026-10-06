// Build values are replaced by TypescriptBridge while bundling.
// Import the exported values explicitly from this module.
export type BUILD_MODE = "DEBUG" | "RELEASE";
export type PROJECT_STATUS = "Library" | "Application";

declare const __TYPESCRIPT_BRIDGE_BUILD_MODE__: BUILD_MODE;
declare const __TYPESCRIPT_BRIDGE_PROJECT_STATUS__: PROJECT_STATUS;
declare const __TYPESCRIPT_BRIDGE_IS_TEST_PROJECT__: boolean;

export const BUILD_MODE: BUILD_MODE = __TYPESCRIPT_BRIDGE_BUILD_MODE__;
export const PROJECT_STATUS: PROJECT_STATUS = __TYPESCRIPT_BRIDGE_PROJECT_STATUS__;
export const IS_TEST_PROJECT: boolean = __TYPESCRIPT_BRIDGE_IS_TEST_PROJECT__;