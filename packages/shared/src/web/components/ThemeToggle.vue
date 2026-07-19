<template>
  <a-tooltip
    :title="isDark ? '切换亮色模式' : '切换暗色模式'"
    placement="bottomRight"
  >
    <a-button
      class="theme-toggle"
      shape="circle"
      size="small"
      type="text"
      @click="toggleTheme"
    >
      <template #icon>
        <BulbFilled v-if="isDark" />
        <BulbOutlined v-else />
      </template>
    </a-button>
  </a-tooltip>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { BulbFilled, BulbOutlined } from '@ant-design/icons-vue'
import { useTheme } from '../composables/useTheme'

const { currentTheme, toggleTheme } = useTheme()
const isDark = computed(() => currentTheme.value === 'dark')
</script>

<style scoped lang="less">
.theme-toggle {
  font-size: 16px;
  color: rgba(255, 255, 255, 0.65);
  &:hover {
    color: #fff !important;
    background: rgba(255, 255, 255, 0.08) !important;
  }
}
</style>
