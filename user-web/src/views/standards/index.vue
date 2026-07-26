<template>
  <div class="standard-page">
    <PageHeader title="标准查询">
      <template #extra>
        <a-button size="small" @click="historyDrawer = true">历史记录</a-button>
      </template>
    </PageHeader>

    <div class="standard-body">
      <!-- 左栏：标准树 -->
      <aside class="standard-body__left">
        <SectionCard title="标准库" flush>
          <template #extra>
            <a-button type="primary" size="small" @click="handleAdd">新增</a-button>
          </template>
          <a-input-search v-model:value="treeSearch" placeholder="搜索标准名称" allow-clear class="tree-search" />
          <a-tree
            :tree-data="treeData"
            :default-expand-all="true"
            :field-names="{ title: 'name', key: 'id' }"
            class="standard-tree"
            @select="(keys: any) => keys[0] && selectStandard(keys[0])"
          />
        </SectionCard>
      </aside>

      <!-- 中栏：阅读器 -->
      <main class="standard-body__center">
        <StandardReader :doc="doc" :loading="docLoading" :error="docError" />
      </main>

      <!-- 右栏：属性 + AI -->
      <aside class="standard-body__right">
        <StandardPropertyPanel
          :property="property"
          :loading="propLoading"
          :error="propError"
          :submitting="submitting"
          @submit="handleSubmit"
        />
      </aside>
    </div>

    <a-drawer v-model:open="historyDrawer" title="查询历史" placement="right" width="400" destroy-on-close>
      <div class="drawer-list">
        <div v-for="item in history" :key="item.id" class="drawer-item" @click="historyDrawer = false">
          <div class="drawer-query">{{ item.query }}</div>
          <div class="drawer-meta">{{ item.date }} · {{ item.resultCount }} 条</div>
        </div>
        <div v-if="!history.length" class="drawer-empty">暂无查询历史</div>
      </div>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import StandardReader from './components/StandardReader.vue'
import StandardPropertyPanel from './components/StandardProperty.vue'
import { getStandardList, getStandardProperty, getStandardDocument, updateStandardProperty, getStandardHistory } from '@/api/modules/standard'
import type { StandardListItem, StandardProperty, StandardDocument, StandardSearchHistory } from '@/types'

const treeSearch = ref('')
const standardList = ref<StandardListItem[]>([])
const listLoading = ref(false)
const listError = ref<string | null>(null)

const currentId = ref<string | null>(null)
const doc = ref<StandardDocument | null>(null)
const docLoading = ref(false)
const docError = ref<string | null>(null)
const property = ref<StandardProperty | null>(null)
const propLoading = ref(false)
const propError = ref<string | null>(null)
const submitting = ref(false)

const history = ref<StandardSearchHistory[]>([])
const historyDrawer = ref(false)

const treeData = computed(() => {
  const q = treeSearch.value.trim().toLowerCase()
  return q
    ? standardList.value.filter(v => v.name.toLowerCase().includes(q) || v.code.toLowerCase().includes(q))
    : standardList.value
})

function handleAdd(): void {
  message.info('新增功能开发中')
}

async function selectStandard(id: string): Promise<void> {
  currentId.value = id
  propLoading.value = true
  docLoading.value = true
  docError.value = null
  propError.value = null
  try {
    const [d, p] = await Promise.all([getStandardDocument(id), getStandardProperty(id)])
    doc.value = d
    property.value = p
  } catch {
    docError.value = '加载失败'
    propError.value = '加载失败'
  } finally {
    docLoading.value = false
    propLoading.value = false
  }
}

async function handleSubmit(data: Partial<StandardProperty>): Promise<void> {
  if (!currentId.value) return
  submitting.value = true
  try {
    await updateStandardProperty(currentId.value, data)
    property.value = { ...property.value!, ...data }
    message.success('已保存')
  } catch {
    message.error('保存失败')
  } finally {
    submitting.value = false
  }
}

onMounted(async () => {
  listLoading.value = true
  try {
    const [list, h] = await Promise.all([getStandardList(), getStandardHistory()])
    standardList.value = list
    history.value = h
  } catch {
    listError.value = '加载失败'
  } finally {
    listLoading.value = false
  }
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.standard-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: @page-padding;
  box-sizing: border-box;
}

.standard-body {
  flex: 1;
  min-height: 0;
  display: flex;
  gap: @spacing-md;

  &__left { width: 240px; flex-shrink: 0; }
  &__center { flex: 1; min-width: 0; }
  &__right { width: 360px; flex-shrink: 0; }
}

.tree-search { margin-bottom: @spacing-md; }

.standard-tree {
  :deep(.ant-tree-node-content-wrapper) { padding-right: 8px; }
}

.drawer-list { display: flex; flex-direction: column; }
.drawer-item {
  padding: @spacing-md @spacing-lg;
  border-bottom: 1px solid @divider-color;
  cursor: pointer;
  transition: background @transition-fast;
  border-radius: @radius-base;
  &:hover { background: @surface-hover; }
}
.drawer-query { font-size: @font-size-sm; font-weight: @font-weight-medium; color: @text-primary; margin-bottom: 2px; }
.drawer-meta { font-size: @font-size-xs; color: @text-tertiary; }
.drawer-empty { text-align: center; font-size: @font-size-sm; color: @text-tertiary; padding: @spacing-3xl 0; }
</style>
