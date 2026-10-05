const { validateReleaseConfig } = require("./scripts/release-config.cjs");

module.exports = ({ config }) => {
  validateReleaseConfig(process.env);
  return config;
};
