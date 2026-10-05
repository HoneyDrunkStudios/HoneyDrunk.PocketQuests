function validateReleaseConfig(env) {
  const profile = env.EAS_BUILD_PROFILE;
  const target = env.EXPO_PUBLIC_APP_ENV;
  if (target && !["development", "preview", "production"].includes(target))
    throw new Error(
      "EXPO_PUBLIC_APP_ENV must be development, preview or production.",
    );
  const release =
    ["preview", "production"].includes(profile) ||
    ["preview", "production"].includes(target) ||
    (env.NODE_ENV === "production" && target !== "development");
  if (!release) return;
  for (const name of ["EXPO_PUBLIC_API_URL", "EXPO_PUBLIC_IDENTITY_URL"]) {
    let url;
    try {
      url = new URL(env[name]);
    } catch {
      throw new Error(
        `${name} must be an explicit HTTPS URL for a release build.`,
      );
    }
    const host = url.hostname.toLowerCase().replace(/\.$/, "");
    if (
      url.protocol !== "https:" ||
      url.username ||
      url.password ||
      url.search ||
      url.hash ||
      !host.includes(".") ||
      /^(localhost|127\.|0\.|10\.|192\.168\.|169\.254\.|172\.(1[6-9]|2\d|3[01])\.)/.test(
        host,
      ) ||
      /\.(localhost|local|internal)$/.test(host) ||
      host.includes(":")
    )
      throw new Error(
        `${name} must use HTTPS and a public host without credentials, query or fragment.`,
      );
  }
}
module.exports = { validateReleaseConfig };
