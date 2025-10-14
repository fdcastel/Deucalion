const { heroui } = require("@heroui/react");

export default {
  theme: {
    extend: {
      colors: {
        'monitor-unknown': 'var(--color-monitor-unknown)',
        'monitor-down': 'var(--color-monitor-down)',
        'monitor-up': 'var(--color-monitor-up)',
        'monitor-warn': 'var(--color-monitor-warn)',
        'monitor-degraded': 'var(--color-monitor-degraded)',
        'flash-light': 'var(--color-flash-light)',
        'flash-dark': 'var(--color-flash-dark)',
      },
    },
  },
  safelist: [
    'bg-monitor-unknown',
    'bg-monitor-down',
    'bg-monitor-up',
    'bg-monitor-warn',
    'bg-monitor-degraded',
    'text-monitor-unknown',
    'text-monitor-down',
    'text-monitor-up',
    'text-monitor-warn',
    'text-monitor-degraded',
  ],
  plugins: [heroui()],
}