// Node component tests do not execute Metro's asset transform or native font loading.
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);

const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "@expo/vector-icons/Ionicons") return () => null;
  return load.call(this, name, ...rest);
};
require.extensions[".png"] = (module) => {
  module.exports = { uri: "test-art.png", width: 100, height: 100 };
};
