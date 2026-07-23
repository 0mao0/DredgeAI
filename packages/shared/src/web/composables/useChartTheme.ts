export function useChartTheme() {
  function chartTheme() {
    const el = document.documentElement
    const isDark = el.getAttribute('data-theme') === 'dark'
    const get = (name: string) => getComputedStyle(el).getPropertyValue(name).trim()
    return {
      axisColor: get('--color-text-tertiary'),
      splitColor: isDark ? 'rgba(148, 163, 184, 0.08)' : 'rgba(0, 0, 0, 0.06)',
      tooltipBg: isDark ? 'rgba(15, 23, 42, 0.92)' : 'rgba(255, 255, 255, 0.92)',
      tooltipBorder: isDark ? 'rgba(148, 163, 184, 0.15)' : 'rgba(0, 0, 0, 0.06)',
      tooltipColor: get('--color-text-primary'),
      legendColor: get('--color-text-secondary'),
    }
  }

  return { chartTheme }
}
