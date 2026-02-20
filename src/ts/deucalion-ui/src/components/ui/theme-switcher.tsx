import * as React from "react";

import { MdDarkMode, MdLightMode } from "react-icons/md";

export const ThemeSwitcher: React.FC = () => {
  const [isDark, setIsDark] = React.useState<boolean>(false);

  React.useEffect(() => {
    const root = document.documentElement;
    const savedTheme = localStorage.getItem("theme");
    const systemDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
    const dark = savedTheme ? savedTheme === "dark" : root.classList.contains("dark") || systemDark;

    setIsDark(dark);
  }, []);

  const toggleTheme = React.useCallback(() => {
    const nextIsDark = !isDark;
    const nextTheme = nextIsDark ? "dark" : "light";
    document.documentElement.classList.toggle("dark", nextIsDark);
    localStorage.setItem("theme", nextTheme);
    setIsDark(nextIsDark);
  }, [isDark]);

  return (
    <button
      type="button"
      className="rounded-full p-2 transition-colors hover:bg-gray-200 dark:hover:bg-gray-700"
      aria-label={`Switch to ${isDark ? "light" : "dark"} mode`}
      onClick={toggleTheme}
    >
      {isDark ? <MdLightMode className="icon-size-5" /> : <MdDarkMode className="icon-size-5" />}
    </button>
  );
};
