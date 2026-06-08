import { defineConfig } from "vitest/config";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const src = (p) => path.join(__dirname, "src", p);

// Unit-test config (Vitest). Component/E2E suites are documented as follow-up;
// this covers the foundation services, stores and composables in plain JS.
export default defineConfig({
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
      src: path.join(__dirname, "src")
    }
  }
});
