import eslint from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";
import pluginReact from "eslint-plugin-react";
import pluginReactHooks from "eslint-plugin-react-hooks";
import pluginReactRefresh from "eslint-plugin-react-refresh";

export default tseslint.config(
  {
    // Global ignores
    ignores: ["dist/**", "eslint.config.js", "vite.config.js", "vite.config.d.ts"],
  },
  // Base recommended configurations
  eslint.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
  {
    languageOptions: {
      parserOptions: {
        project: ["./tsconfig.json", "./tsconfig.node.json"],
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
  {
    // Configuration for React files (TSX)
    files: ["src/**/*.tsx"],
    plugins: {
      react: pluginReact,
      "react-hooks": pluginReactHooks,
      "react-refresh": pluginReactRefresh,
    },
    languageOptions: {
      globals: {
        ...globals.browser,
        ...globals.es2020,
      },
    },
    rules: {
      ...pluginReact.configs.recommended.rules,
      ...pluginReact.configs["jsx-runtime"].rules, // Enable new JSX transform
      ...pluginReactHooks.configs.recommended.rules,
      "react-refresh/only-export-components": [
        "warn",
        { allowConstantExport: true },
      ],
    },
    settings: {
      react: {
        version: "detect",
      },
    },
  },
  {
    // Configuration for other TS files (non-JSX)
    files: ["src/**/*.ts"],
    languageOptions: {
      globals: {
        ...globals.browser,
        ...globals.es2020,
      },
    },
  }
);
