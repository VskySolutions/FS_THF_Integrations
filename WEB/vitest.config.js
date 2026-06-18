import { defineConfig } from "vitest/config";
import path from "path";
import { fileURLToPath } from "url";
import { createRequire } from "module";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const src = (p) => path.join(__dirname, "src", p);

// @vitejs/plugin-vue is provided transitively by @quasar/app-vite (not a direct dependency), so
// resolve and load it from there for the .vue SFC transform that component tests rely on.
const require = createRequire(import.meta.url);
const vuePlugin = require(require.resolve("@vitejs/plugin-vue", {
  paths: [path.join(__dirname, "node_modules/@quasar/app-vite")]
}));
const vue = vuePlugin.default || vuePlugin;

// Unit-test config (Vitest). Covers the foundation services, stores and composables in plain JS,
// plus component tests for SFCs (the @vitejs/plugin-vue transform handles .vue files).
export default defineConfig({
  plugins: [vue()],
  test: {
    environment: "happy-dom",
    globals: true,
    include: ["test/**/*.spec.js"]
  },
  resolve: {
    alias: {
      services: src("services"),
      stores: src("stores"),
      composables: src("composables"),
      boot: src("boot"),
      components: src("components"),
      modules: src("modules"),
      layouts: src("layouts"),
      src: path.join(__dirname, "src")
    }
  }
});
