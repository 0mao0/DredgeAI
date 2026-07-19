import { defineStore } from 'pinia'
import 'pinia-plugin-persistedstate'
import { ref } from 'vue'

/** 侧边栏折叠状态 store：双端共用，替代各端 app store 中混合的 sidebarCollapsed */
export const useSidebarStore = defineStore('sidebar', () => {
  const collapsed = ref(false)

  function toggle(): void {
    collapsed.value = !collapsed.value
  }

  function setCollapsed(val: boolean): void {
    collapsed.value = val
  }

  return { collapsed, toggle, setCollapsed }
}, {
  persist: { pick: ['collapsed'] },
})
