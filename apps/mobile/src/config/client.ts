// Keep Expo public environment lookups explicit so Metro can inline them.
export const apiUrl =
  process.env.EXPO_PUBLIC_API_URL ?? "http://localhost:5217";
export const identityUrl =
  process.env.EXPO_PUBLIC_IDENTITY_URL ?? "http://localhost:5218";

// A public flag selects UI only; credentials are entered locally and the
// separate development host enforces authentication. Release bundles ignore it.
export const developmentSignInEnabled =
  typeof __DEV__ !== "undefined" &&
  __DEV__ &&
  process.env.EXPO_PUBLIC_DEVELOPMENT_SIGN_IN === "1";

export const requestTimeoutMs = {
  signInConfiguration: 10_000,
  renewal: 15_000,
  api: 15_000,
  export: 30_000,
} as const;

export const signInRedirect = {
  scheme: "pocketquests",
  path: "callback",
} as const;

export const completionNoticeDurationMs = 8_000;
