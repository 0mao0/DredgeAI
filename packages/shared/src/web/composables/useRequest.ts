import { ref, onMounted } from 'vue'
import type { Ref } from 'vue'

/**
 * SWR 请求缓存：TTL 内直接返回缓存；过期后先展示旧数据并后台重新校验；
 * 同 key 并发请求自动去重（共享同一个 in-flight Promise）。
 */

interface CacheEntry { data: unknown, ts: number }

const cache = new Map<string, CacheEntry>()
const inflight = new Map<string, Promise<unknown>>()

const DEFAULT_TTL = 60_000

/** 使指定 key 的缓存失效（数据变更操作后调用） */
export function invalidateRequest(key: string): void {
  cache.delete(key)
}

/** 低层 API：带缓存的请求，适用于 store 等无组件生命周期的场景 */
export async function cachedFetch<T>(key: string, fn: () => Promise<T>, ttl = DEFAULT_TTL): Promise<T> {
  const cached = cache.get(key)
  if (cached && Date.now() - cached.ts < ttl) return cached.data as T

  let p = inflight.get(key) as Promise<T> | undefined
  if (!p) {
    p = fn()
    inflight.set(key, p)
  }
  try {
    const result = await p
    cache.set(key, { data: result, ts: Date.now() })
    return result
  } finally {
    inflight.delete(key)
  }
}

export interface UseRequestOptions {
  /** 缓存有效期 ms，默认 60s */
  ttl?: number
}

export interface UseRequestReturn<T> {
  data: Ref<T | null>
  /** 仅无缓存的首次加载为 true（过期缓存的后台校验不触发，避免闪烁） */
  loading: Ref<boolean>
  error: Ref<Error | null>
  /** 强制绕过缓存重新请求 */
  refresh: () => Promise<void>
}

/** 组件内使用的 SWR composable：挂载时自动执行 */
export function useRequest<T>(key: string, fn: () => Promise<T>, opts?: UseRequestOptions): UseRequestReturn<T> {
  const data: Ref<T | null> = ref(null)
  const loading = ref(false)
  const error: Ref<Error | null> = ref(null)
  const ttl = opts?.ttl ?? DEFAULT_TTL

  async function execute(force = false): Promise<void> {
    const cached = cache.get(key)
    const fresh = cached && Date.now() - cached.ts < ttl
    if (!force && cached) {
      data.value = cached.data as T
      if (fresh) return
    } else {
      loading.value = true
    }

    let p = inflight.get(key) as Promise<T> | undefined
    if (!p) {
      p = fn()
      inflight.set(key, p)
    }
    try {
      const result = await p
      data.value = result
      error.value = null
      cache.set(key, { data: result, ts: Date.now() })
    } catch (e) {
      error.value = e as Error
    } finally {
      inflight.delete(key)
      loading.value = false
    }
  }

  onMounted(() => { void execute() })

  return { data, loading, error, refresh: () => execute(true) }
}
