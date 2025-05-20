const { heroui } = require("@heroui/react");

/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
    "./node_modules/@heroui/theme/dist/**/*.{js,ts,jsx,tsx}"
  ],
  theme: {
    extend: {
      colors: {
        monitor: {
          unknown: "#A0AEC0",  // gray.400
          down: "#F56565",     // red.400
          up: "#48BB78",       // green.400
          warn: "#ECC94B",     // yellow.400
          degraded: "#C6F6D5", // green.100
        },
        flash: {
          light: "#DBEAFE", // blue.100
          dark: "#1E3A8A",  // blue.900
        },
      },
    },
  },
  darkMode: "class",
  plugins: [heroui()],

  // Tailwind only generates CSS for class names it can statically find in your source files.
  // Use the `safelist` option to guarantee the custom color classes are always generated and available.
  safelist: [
    'bg-monitor-up',
    'bg-monitor-warn',
    'bg-monitor-degraded',
    'bg-monitor-down',
    'bg-monitor-unknown',
    'text-monitor-up',
    'text-monitor-warn',
    'text-monitor-degraded',
    'text-monitor-down',
    'text-monitor-unknown',
  ],
}
