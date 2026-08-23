<template>
  <a-drawer
    :open="open"
    title="标准详情"
    width="440px"
    :body-style="{ padding: 0 }"
    :push="{ distance: 440 }"
    @update:open="(v: boolean) => emit('update:open', v)"
  >
    <div v-if="standard" class="standard-detail-panel">
      <h3 class="standard-detail-panel__title">{{ standard.name }}</h3>
      <dl class="standard-detail-panel__meta">
        <div class="standard-detail-panel__row">
          <dt>编号</dt>
          <dd>{{ standard.code }}</dd>
        </div>
        <div class="standard-detail-panel__row">
          <dt>行业</dt>
          <dd>{{ standard.industry || '-' }}</dd>
        </div>
        <div class="standard-detail-panel__row">
          <dt>性质</dt>
          <dd>{{ standard.nature || '-' }}</dd>
        </div>
        <div class="standard-detail-panel__row">
          <dt>级别</dt>
          <dd>{{ standard.level || '-' }}</dd>
        </div>
        <div class="standard-detail-panel__row">
          <dt>状态</dt>
          <dd>{{ standard.status || '-' }}</dd>
        </div>
        <div class="standard-detail-panel__row">
          <dt>发布部门</dt>
          <dd>{{ standard.issuer || '-' }}</dd>
        </div>
        <div class="standard-detail-panel__row">
          <dt>发布年份</dt>
          <dd>{{ standard.publishYear ?? '-' }}</dd>
        </div>
      </dl>
      <p v-if="standard.description" class="standard-detail-panel__desc">{{ standard.description }}</p>
      <div v-if="highlights.length" class="standard-detail-panel__parsed">
        <a-tag color="green">已解析</a-tag>
        <span>{{ highlights.length }} 个定位块</span>
      </div>
    </div>
    <a-empty v-else description="暂无详情" />
  </a-drawer>
</template>

<script setup lang="ts">
import type { StandardHighlight, StandardProperty } from '../../core/types/standard'

withDefaults(defineProps<{
  open: boolean
  standard?: StandardProperty | null
  highlights?: StandardHighlight[]
}>(), {
  standard: null,
  highlights: () => [],
})

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.standard-detail-panel {
  padding: @spacing-lg;
}
.standard-detail-panel__title {
  margin: 0 0 @spacing-lg;
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}
.standard-detail-panel__meta {
  margin: 0 0 @spacing-lg;
}
.standard-detail-panel__row {
  display: flex;
  gap: @spacing-md;
  padding: 6px 0;
  font-size: @font-size-sm;
  dt {
    width: 72px;
    flex-shrink: 0;
    color: @text-tertiary;
  }
  dd {
    margin: 0;
    color: @text-secondary;
    word-break: break-all;
  }
}
.standard-detail-panel__desc {
  margin: 0 0 @spacing-lg;
  font-size: @font-size-sm;
  line-height: 1.7;
  color: @text-secondary;
}
.standard-detail-panel__parsed {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  font-size: @font-size-xs;
  color: @text-secondary;
}
</style>
