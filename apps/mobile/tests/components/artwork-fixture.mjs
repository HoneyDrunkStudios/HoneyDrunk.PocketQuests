// Node component tests do not execute Metro's asset transform or native font loading.
import Module, { createRequire } from "node:module";
import { basename } from "node:path";
const require = createRequire(import.meta.url);

const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "@react-native/assets-registry/registry")
    return require("react-native-web").AssetRegistry;
  if (name.endsWith("/resolveAssetUri"))
    return require("react-native-svg/lib/commonjs/lib/resolveAssetUri.web.js");
  if (name === "react-native-svg")
    return require("react-native-svg/lib/commonjs/elements.web.js");
  if (name === "@expo/vector-icons/Ionicons") return () => null;
  return load.call(this, name, ...rest);
};
require.extensions[".png"] = (module, file) => {
  module.exports = { uri: basename(file), width: 100, height: 100 };
};
require.extensions[".jpg"] = require.extensions[".png"];
