import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  base: "/admin/",
  plugins: [react()],
  build: {
    outDir: "../../src/PocketQuests.Api/wwwroot/admin",
    emptyOutDir: true,
    sourcemap: false,
  },
  // Development uses the same browser origin for API/session cookies. Provider
  // callbacks must be registered for this origin if testing live sign-in here.
  server: {
    host: "127.0.0.1",
    proxy: {
      "/api": "http://localhost:5217",
      "/admin/login": "http://localhost:5217",
      "/admin/signin-oidc": "http://localhost:5217",
    },
  },
});
