<template>
  <div class="page-container">
    <PageHeader title="组织机构" description="维护组织单位层级结构">
      <template #extra>
        <AppButton v-if="can('create')" variant="primary" size="sm" @click="openCreateModal(null)">新增组织</AppButton>
      </template>
    </PageHeader>

    <DataTable
      :columns="columns"
      :data-source="treeData"
      row-key="id"
      :pagination="false"
      :loading="loading"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'action'">
          <AppButton v-if="can('create')" variant="link" size="sm" @click="openCreateModal(record)">新增子级</AppButton>
          <AppButton v-if="can('update')" variant="link" size="sm" @click="openEditModal(record)">编辑</AppButton>
          <a-popconfirm
            v-if="can('delete')"
            title="确认删除该组织？存在子级时将被拒绝"
            placement="left"
            @confirm="handleDelete(record.id)"
          >
            <AppButton variant="link" size="sm" danger>删除</AppButton>
          </a-popconfirm>
        </template>
      </template>
    </DataTable>

    <a-modal
      v-model:open="formModalVisible"
      :title="editingNode ? '编辑组织' : '新增组织'"
      width="440px"
      @ok="handleSaveForm"
    >
      <a-form layout="vertical">
        <a-form-item label="上级组织">
          <a-tree-select
            v-model:value="formParentId"
            :tree-data="parentOptions"
            :field-names="{ children: 'children', label: 'displayName', value: 'id' }"
            allow-clear
            tree-default-expand-all
            placeholder="不选则为根节点"
          />
        </a-form-item>
        <a-form-item label="组织名称">
          <a-input v-model:value="formName" placeholder="请输入组织名称" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import type { OrgUnit } from '@/types'
import { getOrgUnitTree, createOrgUnit, updateOrgUnit, deleteOrgUnit } from '@/api/modules/org-units'
import { usePagePermissions } from '@/composables/usePagePermissions'
import { useAppStore } from '@/stores/app'
import { formatDateTime, resolveAppConfigTimeZone } from '@shared/web/utils/format'

const appStore = useAppStore()
/** 显示时区：应用配置 timing.timeZone（ABP 时钟为 UTC），缺省 Asia/Shanghai */
const displayTz = computed(() => resolveAppConfigTimeZone(appStore.appConfig))

const { can } = usePagePermissions()
const loading = ref(false)
const treeData = ref<OrgUnit[]>([])

const columns = computed<DataTableColumn[]>(() => {
  const base: DataTableColumn[] = [
    { title: '组织名称', dataIndex: 'displayName', key: 'displayName', width: 240, minWidth: 180, resizable: true, className: 'cell-left' },
    { title: '层级编码', dataIndex: 'code', key: 'code', width: 160, minWidth: 120, resizable: true, className: 'cell-left' },
    { title: '创建时间', dataIndex: 'creationTime', key: 'creationTime', width: 170, minWidth: 150 },
    { title: '操作', key: 'action', width: 180, minWidth: 180, fixed: 'right' },
  ]
  return can('create') || can('update') || can('delete') ? base : base.filter((c) => c.key !== 'action')
})

/** 剥空 children（避免 antd table/tree-select 对空数组渲染展开箭头）+ 创建时间按配置时区格式化 */
function normalizeNodes(nodes: OrgUnit[], tz: string): OrgUnit[] {
  return nodes.map((n) => ({
    ...n,
    creationTime: formatDateTime(n.creationTime, tz),
    children: n.children.length > 0 ? normalizeNodes(n.children, tz) : undefined,
  })) as OrgUnit[]
}

async function fetchTree(): Promise<void> {
  loading.value = true
  try {
    treeData.value = normalizeNodes(await getOrgUnitTree(), displayTz.value)
  } catch {
    message.error('加载组织机构失败')
  } finally {
    loading.value = false
  }
}

const formModalVisible = ref(false)
const formName = ref('')
const formParentId = ref<string | undefined>(undefined)
const editingNode = ref<OrgUnit | null>(null)

/** 收集节点自身及全部后代 id（编辑时从上级选项中剔除，防循环引用） */
function collectSubtreeIds(node: OrgUnit, out: Set<string>): Set<string> {
  out.add(node.id)
  for (const child of node.children ?? []) collectSubtreeIds(child, out)
  return out
}

const parentOptions = computed<OrgUnit[]>(() => {
  if (!editingNode.value) return treeData.value
  const excluded = collectSubtreeIds(editingNode.value, new Set())
  const prune = (nodes: OrgUnit[]): OrgUnit[] =>
    nodes
      .filter((n) => !excluded.has(n.id))
      .map((n) => ({ ...n, children: n.children ? prune(n.children) : undefined })) as OrgUnit[]
  return prune(treeData.value)
})

function openCreateModal(parent: OrgUnit | null): void {
  editingNode.value = null
  formName.value = ''
  formParentId.value = parent?.id
  formModalVisible.value = true
}

function openEditModal(node: OrgUnit): void {
  editingNode.value = node
  formName.value = node.displayName
  formParentId.value = node.parentId ?? undefined
  formModalVisible.value = true
}

async function handleSaveForm(): Promise<void> {
  if (!formName.value.trim()) {
    message.warning('请输入组织名称')
    return
  }
  try {
    if (editingNode.value) {
      // parentId 为空不传：后端语义 null/缺省=保持不变（编辑不支持移动为根节点）
      await updateOrgUnit(editingNode.value.id, {
        displayName: formName.value.trim(),
        ...(formParentId.value ? { parentId: formParentId.value } : {}),
      })
      message.success('已保存')
    } else {
      await createOrgUnit({ displayName: formName.value.trim(), parentId: formParentId.value ?? null })
      message.success('已创建')
    }
    formModalVisible.value = false
    fetchTree()
  } catch {
    message.error('操作失败')
  }
}

async function handleDelete(id: string): Promise<void> {
  try {
    await deleteOrgUnit(id)
    message.success('已删除')
    fetchTree()
  } catch {
    message.error('删除失败')
  }
}

onMounted(async () => {
  // 先确保应用配置（含时区）就绪再渲染时间；失败时退回默认时区，不阻塞列表
  await appStore.fetchAppConfig().catch(() => undefined)
  fetchTree()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.page-container :deep(.page-header-left) {
  display: flex;
  align-items: baseline;
  gap: @spacing-sm;
}
.page-container :deep(.page-desc) {
  margin-top: 0;
  color: @text-tertiary;
}

// global.less 对表格单元格强制 text-align: center !important，此处对名称/编码列覆盖为左对齐
.page-container :deep(.cell-left) {
  text-align: left !important;
}
.page-container :deep(.page-header) {
  margin-bottom: @spacing-md;
}
</style>
