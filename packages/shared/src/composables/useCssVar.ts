import { ref, watchEffect } from 'vue'
import { useTheme } from './useTheme'

export function useCssVar(name: string) {
  const val = ref('')
  const { currentTheme } = useTheme()

  watchEffect(() => {
    currentTheme.value
    val.value = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  })

  return val
}

export function cssVarValue(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim()
}
