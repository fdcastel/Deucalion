import * as React from "react";

import { useTheme } from "next-themes";
import { MdDarkMode, MdLightMode } from "react-icons/md";

export const ThemeSwitcher: React.FC = () => {
  const { setTheme, resolvedTheme } = useTheme();
  const isDark = resolvedTheme === "dark";

  return (
    <button
      type="button"
      className="rounded-full p-2 transition-colors hover:bg-gray-200 dark:hover:bg-gray-700"
      aria-label={`Switch to ${isDark ? "light" : "dark"} mode`}
      onClick={() => setTheme(isDark ? "light" : "dark")}
    >
      {isDark ? <MdLightMode className="h-5 w-5" /> : <MdDarkMode className="h-5 w-5" />}
    </button>
  );
};
