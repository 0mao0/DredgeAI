import { ref, computed, watch, onUnmounted } from 'vue'

/**
 * 数值 CountUp 动画：目标值变化时从旧值缓动滚动到新值（easeOutCubic）。
 * 自动跟随 target 的整数/小数决定显示精度；prefers-reduced-motion 时直达终值。
 */
export function useCountUp(target: () => number, duration = 800) {
  const current = ref(0)
  let raf = 0

  const reducedMotion = typeof window !== 'undefined'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches

  function animate(from: number, to: number): void {
    cancelAnimationFrame(raf)
    if (reducedMotion || from === to) {
      current.value = to
      return
    }
    const start = performance.now()
    const tick = (now: number): void => {
      const p = Math.min((now - start) / duration, 1)
      const eased = 1 - (1 - p) ** 3
      current.value = from + (to - from) * eased
      if (p < 1) raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
  }

  watch(target, (to, from) => animate(from ?? 0, to), { immediate: true })
  onUnmounted(() => cancelAnimationFrame(raf))

  const display = computed(() => {
    const t = target()
    const dec = Number.isInteger(t) ? 0 : 2
    return current.value.toLocaleString('zh-CN', {
      minimumFractionDigits: dec,
      maximumFractionDigits: dec,
    })
  })

  return { display }
}
