import { ref, watch, nextTick } from 'vue'
import type { CSSProperties } from 'vue'

/**
 * 列表/卡片 stagger 入场：数据就绪后首个元素立即出现，后续按 index * delay 错峰。
 * 用法：:style="itemStyle(index)" 绑定到每个卡片根元素。
 *
 * @param itemCount 列表长度 getter（异步数据在长度 > 0 时才触发入场）
 * @param delay 相邻元素延迟 ms
 * @param extraTransition 附加的 transition 声明（保留元素原有 hover 过渡，如 'background 0.2s ease'）
 */
export function useStaggerReveal(itemCount: () => number, delay = 60, extraTransition = '') {
  const visible = ref(false)

  // 注意：immediate 回调会在 watch() 返回前同步执行，此时 stop 尚未初始化（TDZ），
  // 因此 stop() 必须放到 nextTick 中延迟调用。
  const stop = watch(itemCount, (n) => {
    if (n > 0) {
      void nextTick(() => {
        visible.value = true
        stop()
      })
    }
  }, { immediate: true })

  function itemStyle(index: number): CSSProperties {
    const offset = index * delay
    const extra = extraTransition ? `, ${extraTransition}` : ''
    return {
      opacity: visible.value ? 1 : 0,
      transform: visible.value ? 'translateY(0)' : 'translateY(16px)',
      transition: `opacity 0.5s cubic-bezier(0.22,1,0.36,1) ${offset}ms, transform 0.5s cubic-bezier(0.22,1,0.36,1) ${offset}ms${extra}`,
    }
  }

  return { visible, itemStyle }
}
