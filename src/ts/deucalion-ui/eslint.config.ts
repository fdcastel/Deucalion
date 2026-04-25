import eslint from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";
import pluginSolid from "eslint-plugin-solid/configs/typescript";

export default tseslint.config(
  {
    ignores: ["dist/**", "eslint.config.ts", "vite.config.ts", "vite.config.d.ts"],
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
    files: ["dist/**", "eslint.config.ts"],
    extends: [tseslint.configs.disableTypeChecked],
  },
);
