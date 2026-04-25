import eslint from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";
import pluginSolid from "eslint-plugin-solid/configs/typescript";

export default tseslint.config(
  {
    ignores: [
      "dist/**",
      "eslint.config.ts",
      "vite.config.ts",
      "vite.config.d.ts",
      "playwright.config.ts",
      "tests/e2e/**",
      "test-results/**",
      "playwright-report/**",
    ],
  },
  eslint.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
  {
    languageOptions: {
      parserOptions: {
        project: ["./tsconfig.json", "./tsconfig.node.json"],
        tsconfigRootDir: import.meta.dirname,
      },
      globals: {
        ...globals.browser,
        ...globals.es2020,
      },
    },
  },
  {
    files: ["src/**/*.{ts,tsx}"],
    ...pluginSolid,
  },
  {
    // Test files: relax the strictest checks — `!` assertions and
    // function-binding warnings are common (and fine) in test code.
    files: ["src/**/*.test.{ts,tsx}", "src/test/**/*.{ts,tsx}"],
    rules: {
      "@typescript-eslint/no-non-null-assertion": "off",
      "@typescript-eslint/unbound-method": "off",
      "@typescript-eslint/no-unnecessary-condition": "off",
      "@typescript-eslint/require-await": "off",
      "@typescript-eslint/no-base-to-string": "off",
      "@typescript-eslint/no-unsafe-assignment": "off",
      "@typescript-eslint/no-unsafe-call": "off",
      "@typescript-eslint/no-unsafe-return": "off",
    },
  },
  {
    files: ["dist/**", "eslint.config.ts"],
    extends: [tseslint.configs.disableTypeChecked],
  },
);
