import js from "@eslint/js";
import pluginVue from "eslint-plugin-vue";
import neostandard from "neostandard";
import globals from "globals";

// Flat config (ESLint 9+). Replaces the legacy .eslintrc.cjs / .eslintignore.
// neostandard is the flat-config successor to eslint-config-standard; semi:true
// keeps the project's "semicolons required" convention. Double quotes and
// 2-space indent are enforced via the explicit overrides below.
export default [
  {
    ignores: [
      "dist/**",
      ".quasar/**",
      "node_modules/**",
      "src-capacitor/**",
      "src-cordova/**",
      "quasar.config.*.temporary.compiled*"
    ]
  },

  js.configs.recommended,

  ...pluginVue.configs["flat/recommended"],

  ...neostandard({ semi: true }),

  {
    languageOptions: {
      ecmaVersion: 2021,
      sourceType: "module",
      globals: {
        ...globals.browser,
        ...globals.node,
        ga: "readonly", // Google Analytics
        cordova: "readonly",
        __statics: "readonly",
        __QUASAR_SSR__: "readonly",
        __QUASAR_SSR_SERVER__: "readonly",
        __QUASAR_SSR_CLIENT__: "readonly",
        __QUASAR_SSR_PWA__: "readonly",
        process: "readonly",
        Capacitor: "readonly",
        chrome: "readonly"
      }
    },

    // Custom rules carried over from the original .eslintrc.cjs.
    rules: {
      "vue/max-attributes-per-line": "off",
      "one-var": "off",
      "no-void": "off",
      "@stylistic/generator-star-spacing": "off",
      "@stylistic/arrow-parens": "off",
      "@stylistic/multiline-ternary": "off",

      "no-unused-vars": "error",

      "@stylistic/indent": ["error", 2],
      "@stylistic/semi": ["error", "always"],
      "@stylistic/quotes": ["error", "double"],

      "vue/singleline-html-element-content-newline": "off",
      "vue/multiline-html-element-content-newline": "off",
      "vue/multi-word-component-names": "off",
      "vue/no-undef-properties": "error",

      // allow debugger during development only
      "no-debugger": process.env.NODE_ENV === "production" ? "error" : "off"
    }
  }
];
