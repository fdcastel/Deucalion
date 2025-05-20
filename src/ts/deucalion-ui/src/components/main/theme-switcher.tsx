import * as React from "react";
import { useTheme } from "@heroui/use-theme";
import { MdDarkMode, MdLightMode } from "react-icons/md";

export const ThemeSwitcher: React.FC = () => {
  const { theme, setTheme } = useTheme();
  const isDark = theme === "dark";

  return (
    <button
      type="button"
      className="p-2 rounded-full hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors"
      aria-label={`Switch to ${isDark ? "light" : "dark"} mode`}
      onClick={() => setTheme(isDark ? "light" : "dark")}
    >
      {isDark ? (
        <MdLightMode className="w-5 h-5" />
      ) : (
        <MdDarkMode className="w-5 h-5" />
      )}
    </button>
  );
};
