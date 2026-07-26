import { ref } from 'vue'
import type { Ref } from 'vue'
import { message } from 'ant-design-vue'

export interface UseAsyncOptions {
  /** 静默模式：出错时不弹全局 toast */
  silent?: boolean
  /** 是否在创建时立即执行一次 */
  immediate?: boolean
}

/**
 * 统一异步操作状态管理：data / loading / error 三态 + 全局错误提示。
 * 消除页面级数据获取散落的 try/catch 模板代码。
 */
export function useAsync<T>(fn: () => Promise<T>, opts?: UseAsyncOptions) {
  const data: Ref<T | null> = ref(null)
  const loading = ref(false)
  const error: Ref<Error | null> = ref(null)

  async function execute(): Promise<T | null> {
    loading.value = true
    error.value = null
    try {
      const result = await fn()
      data.value = result
      return result
    } catch (e) {
      error.value = e as Error
      if (!opts?.silent) message.error((e as Error).message || '操作失败')
      return null
    } finally {
      loading.value = false
    }
  }

  if (opts?.immediate) void execute()

  return { data, loading, error, execute }
}
