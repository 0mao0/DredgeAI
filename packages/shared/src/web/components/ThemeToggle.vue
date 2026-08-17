<template>
  <a-tooltip
    :title="isDark ? '切换亮色模式' : '切换暗色模式'"
    placement="bottomRight"
  >
    <AppButton
      class="theme-toggle"
      shape="circle"
      size="sm"
      variant="text"
      @click="toggleTheme"
    >
      <template #icon>
        <BulbFilled v-if="isDark" />
        <BulbOutlined v-else />
      </template>
    </AppButton>
  </a-tooltip>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { BulbFilled, BulbOutlined } from '@ant-design/icons-vue'
import AppButton from './AppButton.vue'
import { useTheme } from '../composables/useTheme'

const { currentTheme, toggleTheme } = useTheme()
const isDark = computed(() => currentTheme.value === 'dark')
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.theme-toggle {
  font-size: 16px;
  color: @header-text-secondary;
  &:hover {
    color: @header-text !important;
    background: @surface-hover !important;
  }
}
</style>
