<template>
  <div class="page-container">
    <PageHeader title="字典管理" description="维护字典类型与字典数据" />

    <div class="dict-layout">
      <!-- 左：字典类型树 -->
      <SectionCard class="dict-type-pane">
        <template #title>字典类型</template>
        <template #extra>
          <AppButton v-if="can('createType')" variant="link" size="sm" @click="openTypeCreate(null)">新增</AppButton>
        </template>
        <a-input
          v-model:value="typeKeyword"
          allow-clear
          placeholder="搜索名称/编码"
          class="dict-type-pane__search"
        />
        <a-spin :spinning="typeLoading">
          <a-tree
            v-if="filteredTypeTree.length"
            v-model:selected-keys="selectedTypeKeys"
            block-node
            :tree-data="filteredTypeTree"
            :field-names="{ children: 'children', title: 'name', key: 'id' }"
            @select="handleSelectType"
          >
            <template #title="nodeData">
              <span class="dict-tree-node">
                <span class="dict-tree-node__name">{{ nodeData.name }}</span>
                <span class="dict-tree-node__actions" @click.stop>
                  <a-dropdown :trigger="['click']" placement="bottomRight">
                    <AppButton variant="link" size="sm"><MoreOutlined /></AppButton>
                    <template #overlay>
                      <a-menu>
                        <a-menu-item
                          v-if="can('createType')"
                          key="create"
                          @click="openTypeCreate(nodeData as DictTypeNode)"
                        >新增子级</a-menu-item>
                        <a-menu-item
                          v-if="can('updateType') && !nodeData.isStatic"
                          key="edit"
                          @click="openTypeEdit(nodeData as DictTypeNode)"
                        >编辑</a-menu-item>
                        <a-menu-item
                          v-if="can('deleteType') && !nodeData.isStatic"
                          key="delete"
                          danger
                          @click="confirmDeleteType(nodeData as DictTypeNode)"
                        >删除</a-menu-item>
                      </a-menu>
                    </template>
                  </a-dropdown>
                </span>
              </span>
            </template>
          </a-tree>
          <EmptyState v-else-if="!typeLoading" title="暂无字典类型" />
        </a-spin>
      </SectionCard>

      <!-- 右：字典数据列表 -->
      <div class="dict-data-pane">
        <SectionCard v-if="!currentType">
          <EmptyState title="请先在左侧选择字典类型" />
        </SectionCard>
        <DataTable
          v-else
          v-model:query="dataQuery"
          storage-key="admin-dict-data"
          :columns="columns"
          :data-source="filteredDataTree"
          row-key="id"
          :loading="dataLoading"
          :filters="dataFilters"
          :expandable="{ expandedRowKeys: dataExpandedKeys, onExpandedRowsChange: (keys: (string | number)[]) => { dataExpandedKeys = keys.map(String) } }"
        >
          <template #toolbarExtra>
            <AppButton v-if="can('createData')" variant="primary" size="sm" @click="openDataCreate(null)">新增数据</AppButton>
          </template>
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'isEnabled'">
              <a-tag :color="record.isEnabled ? 'green' : 'red'">{{ record.isEnabled ? '启用' : '停用' }}</a-tag>
            </template>
            <template v-else-if="column.key === 'action'">
              <AppButton v-if="can('createData')" variant="link" size="sm" @click="openDataCreate(record as DictDataNode)">新增子级</AppButton>
              <template v-if="!record.isStatic">
                <AppButton v-if="can('updateData')" variant="link" size="sm" @click="openDataEdit(record)">编辑</AppButton>
                <a-popconfirm
                  v-if="can('deleteData')"
                  title="确认删除该字典数据？"
                  placement="left"
                  @confirm="handleDeleteData(record as DictDataNode)"
                >
                  <AppButton variant="link" size="sm" danger>删除</AppButton>
                </a-popconfirm>
              </template>
            </template>
          </template>
        </DataTable>
      </div>
    </div>

    <!-- 字典类型表单 -->
    <a-modal
      v-model:open="typeModalVisible"
      :title="editingType ? '编辑字典类型' : '新增字典类型'"
      width="440px"
      :confirm-loading="typeSaving"
      @ok="handleSaveType"
    >
      <a-form layout="vertical">
        <a-form-item label="名称" required>
          <a-input v-model:value="typeForm.name" placeholder="请输入类型名称" />
        </a-form-item>
        <a-form-item label="编码">
          <a-input v-model:value="typeForm.code" placeholder="留空自动生成" />
        </a-form-item>
        <a-form-item label="上级类型">
          <a-tree-select
            v-model:value="typeForm.parentId"
            :tree-data="typeParentOptions"
            :field-names="{ children: 'children', label: 'name', value: 'id' }"
            allow-clear
            tree-default-expand-all
            placeholder="不选则为根节点"
          />
        </a-form-item>
        <a-form-item label="模块编码">
          <a-input v-model:value="typeForm.moduleCode" placeholder="如 SYS_USER，可选" />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="typeForm.sort" :min="0" :max="9999" class="dict-form__number" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="typeForm.remark" :rows="2" placeholder="可选" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 字典数据表单 -->
    <a-modal
      v-model:open="dataModalVisible"
      :title="editingData ? '编辑字典数据' : '新增字典数据'"
      width="440px"
      :confirm-loading="dataSaving"
      @ok="handleSaveData"
    >
      <a-spin :spinning="dataTreeLoading">
        <a-form layout="vertical">
          <a-form-item label="名称" required>
            <a-input v-model:value="dataForm.name" placeholder="请输入数据名称" />
          </a-form-item>
          <a-form-item label="值" required>
            <a-input v-model:value="dataForm.value" placeholder="请输入数据值" />
          </a-form-item>
          <a-form-item label="上级数据">
            <a-tree-select
              v-model:value="dataForm.parentId"
              :tree-data="dataParentOptions"
              :field-names="{ children: 'children', label: 'name', value: 'id' }"
              allow-clear
              tree-default-expand-all
              placeholder="不选则为根级"
            />
          </a-form-item>
          <a-form-item label="排序">
            <a-input-number v-model:value="dataForm.sort" :min="0" :max="9999" class="dict-form__number" />
          </a-form-item>
          <a-form-item label="启用">
            <a-switch v-model:checked="dataForm.isEnabled" />
          </a-form-item>
          <a-form-item label="备注">
            <a-textarea v-model:value="dataForm.remark" :rows="2" placeholder="可选" />
          </a-form-item>
        </a-form>
      </a-spin>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { AppButton, DataTable, EmptyState } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { message, Modal } from 'ant-design-vue'
import { MoreOutlined } from '@ant-design/icons-vue'
import { ref, computed, watch, onMounted } from 'vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import { usePagePermissions } from '@/composables/usePagePermissions'
import {
  getDictTypeTree,
  getDictType,
  createDictType,
  updateDictType,
  deleteDictType,
  createDictData,
  updateDictData,
  deleteDictData,
  getDictDataTree,
} from '@/api/modules/dict'
import type { DictTypeNode, DictDataNode } from '@/api/modules/dict'

const { can } = usePagePermissions()

// ── 字典类型树（左 pane）───────────────────────────────
const typeLoading = ref(false)
const typeTree = ref<DictTypeNode[]>([])
const typeKeyword = ref('')
const selectedTypeKeys = ref<string[]>([])
const currentType = ref<DictTypeNode | null>(null)
// 提前声明：类型树的选中/删除逻辑会引用右侧数据状态
const dataTree = ref<DictDataNode[]>([])
const dataExpandedKeys = ref<string[]>([])

/** 剥空 children（避免 antd tree/tree-select 对空数组渲染展开箭头） */
function normalizeTypeNodes(nodes: DictTypeNode[]): DictTypeNode[] {
  return nodes.map((n) => ({
    ...n,
    children: n.children?.length ? normalizeTypeNodes(n.children) : undefined,
  })) as DictTypeNode[]
}

/** 客户端过滤：按 name/code 匹配，保留命中节点的祖先链 */
function filterTypeTree(nodes: DictTypeNode[], kw: string): DictTypeNode[] {
  const lower = kw.toLowerCase()
  const walk = (list: DictTypeNode[]): DictTypeNode[] =>
    list
      .map((n) => {
        const children = n.children?.length ? walk(n.children) : []
        const hit = n.name.toLowerCase().includes(lower) || n.code.toLowerCase().includes(lower)
        return hit || children.length ? { ...n, children: children.length ? children : undefined } : null
      })
      .filter((n): n is DictTypeNode => n !== null)
  return walk(nodes)
}

const filteredTypeTree = computed<DictTypeNode[]>(() => {
  const kw = typeKeyword.value.trim()
  return kw ? filterTypeTree(typeTree.value, kw) : typeTree.value
})

function findTypeNode(nodes: DictTypeNode[], id: string): DictTypeNode | null {
  for (const n of nodes) {
    if (n.id === id) return n
    const hit = n.children?.length ? findTypeNode(n.children, id) : null
    if (hit) return hit
  }
  return null
}

async function fetchTypeTree(): Promise<void> {
  typeLoading.value = true
  try {
    typeTree.value = normalizeTypeNodes(await getDictTypeTree())
  } catch {
    typeTree.value = []
  } finally {
    typeLoading.value = false
  }
}

function handleSelectType(keys: (string | number)[]): void {
  const id = keys[0] as string | undefined
  const node = id ? findTypeNode(typeTree.value, id) : null
  currentType.value = node
  if (node) {
    fetchData()
  } else {
    dataTree.value = []
    dataExpandedKeys.value = []
  }
}

// ── 字典类型表单 ─────────────────────────────────────
const typeModalVisible = ref(false)
const typeSaving = ref(false)
const editingType = ref<DictTypeNode | null>(null)
const typeForm = ref({
  name: '',
  code: '',
  parentId: undefined as string | undefined,
  moduleCode: '',
  sort: 0,
  remark: '',
})

/** 收集节点自身及全部后代 id（编辑时从上级选项中剔除，防循环引用） */
function collectSubtreeIds(node: DictTypeNode, out: Set<string>): Set<string> {
  out.add(node.id)
  for (const child of node.children ?? []) collectSubtreeIds(child, out)
  return out
}

const typeParentOptions = computed<DictTypeNode[]>(() => {
  if (!editingType.value) return typeTree.value
  const excluded = collectSubtreeIds(editingType.value, new Set())
  const prune = (nodes: DictTypeNode[]): DictTypeNode[] =>
    nodes
      .filter((n) => !excluded.has(n.id))
      .map((n) => ({ ...n, children: n.children ? prune(n.children) : undefined })) as DictTypeNode[]
  return prune(typeTree.value)
})

function openTypeCreate(parent: DictTypeNode | null): void {
  editingType.value = null
  typeForm.value = { name: '', code: '', parentId: parent?.id, moduleCode: '', sort: 0, remark: '' }
  typeModalVisible.value = true
}

async function openTypeEdit(node: DictTypeNode): Promise<void> {
  try {
    // 树节点无 sort/moduleCode/remark 字段，编辑前取详情回填
    const detail = await getDictType(node.id)
    editingType.value = node
    typeForm.value = {
      name: detail.name,
      code: detail.code,
      parentId: detail.parentId ?? undefined,
      moduleCode: detail.moduleCode ?? '',
      sort: detail.sort,
      remark: detail.remark ?? '',
    }
    typeModalVisible.value = true
  } catch {
    // 错误 toast 由全局 request 拦截器呈现
  }
}

async function handleSaveType(): Promise<void> {
  const name = typeForm.value.name.trim()
  if (!name) {
    message.warning('请输入类型名称')
    return
  }
  typeSaving.value = true
  try {
    const payload = {
      name,
      code: typeForm.value.code.trim() || undefined,
      parentId: typeForm.value.parentId ?? null,
      moduleCode: typeForm.value.moduleCode.trim() || undefined,
      sort: typeForm.value.sort,
      remark: typeForm.value.remark.trim() || undefined,
    }
    let savedId: string
    if (editingType.value) {
      const res = await updateDictType(editingType.value.id, payload)
      savedId = res.id
      message.success('已保存')
    } else {
      const res = await createDictType(payload)
      savedId = res.id
      message.success('已创建')
    }
    typeModalVisible.value = false
    await fetchTypeTree()
    // 保持/恢复选中
    const node = findTypeNode(typeTree.value, savedId)
    if (node) {
      selectedTypeKeys.value = [savedId]
      currentType.value = node
      fetchData()
    }
  } catch {
    // 错误 toast 由全局 request 拦截器呈现
  } finally {
    typeSaving.value = false
  }
}

async function handleDeleteType(node: DictTypeNode): Promise<void> {
  // 树数据已知有无子级：有子级则级联删除
  const cascade = !!node.children?.length
  try {
    await deleteDictType(node.id, cascade)
    message.success('已删除')
    // 删的是当前选中类型（或其祖先）时清空选中
    if (currentType.value && findTypeNode([node], currentType.value.id)) {
      currentType.value = null
      selectedTypeKeys.value = []
      dataTree.value = []
      dataExpandedKeys.value = []
    }
    fetchTypeTree()
  } catch {
    // 错误 toast 由全局 request 拦截器呈现
  }
}

function confirmDeleteType(node: DictTypeNode): void {
  Modal.confirm({
    title: node.children?.length
      ? '该类型包含子级，删除将级联删除所有子类型及其字典数据'
      : '确认删除该字典类型？',
    okButtonProps: { danger: true },
    onOk: () => handleDeleteType(node),
  })
}

// ── 字典数据列表（右 pane）─────────────────────────────
const dataLoading = ref(false)
const dataQuery = ref({ keyword: '' })

const dataFilters: DataTableFilter[] = [
  { key: 'keyword', type: 'input', placeholder: '搜索名称/值', width: 240 },
]

const columns = computed<DataTableColumn[]>(() => {
  const base: DataTableColumn[] = [
    { title: '名称', dataIndex: 'name', key: 'name', width: 160, minWidth: 120, resizable: true, className: 'cell-left' },
    { title: '值', dataIndex: 'value', key: 'value', width: 140, minWidth: 100, resizable: true, className: 'cell-left' },
    { title: '编码', dataIndex: 'code', key: 'code', width: 200, minWidth: 140, resizable: true, className: 'cell-left' },
    { title: '排序', dataIndex: 'sort', key: 'sort', width: 70, minWidth: 70 },
    { title: '启用', dataIndex: 'isEnabled', key: 'isEnabled', width: 80, minWidth: 80 },
    { title: '备注', dataIndex: 'remark', key: 'remark', minWidth: 120, resizable: true, className: 'cell-left' },
    { title: '操作', key: 'action', width: 240, minWidth: 240, fixed: 'right' },
  ]
  return can('createData') || can('updateData') || can('deleteData') ? base : base.filter((c) => c.key !== 'action')
})

async function fetchData(): Promise<void> {
  if (!currentType.value) return
  dataLoading.value = true
  try {
    dataTree.value = normalizeDataNodes(await getDictDataTree(currentType.value.id))
    // 默认全展开
    dataExpandedKeys.value = collectDataIds(dataTree.value)
  } catch {
    dataTree.value = []
    dataExpandedKeys.value = []
  } finally {
    dataLoading.value = false
  }
}

/** 递归收集全树节点 id（默认全展开用） */
function collectDataIds(nodes: DictDataNode[]): string[] {
  const ids: string[] = []
  const walk = (list: DictDataNode[]): void => {
    for (const n of list) {
      ids.push(n.id)
      if (n.children?.length) walk(n.children)
    }
  }
  walk(nodes)
  return ids
}

/** 客户端过滤：按 name/value 匹配，保留命中节点的祖先链 */
function filterDataTree(nodes: DictDataNode[], kw: string): DictDataNode[] {
  const lower = kw.toLowerCase()
  const walk = (list: DictDataNode[]): DictDataNode[] =>
    list
      .map((n) => {
        const children = n.children?.length ? walk(n.children) : []
        const hit = n.name.toLowerCase().includes(lower) || n.value.toLowerCase().includes(lower)
        return hit || children.length ? { ...n, children: children.length ? children : undefined } : null
      })
      .filter((n): n is DictDataNode => n !== null)
  return walk(nodes)
}

const filteredDataTree = computed<DictDataNode[]>(() => {
  const kw = dataQuery.value.keyword?.trim()
  return kw ? filterDataTree(dataTree.value, kw) : dataTree.value
})

// 搜索时命中节点沿祖先链展开可见（用户随后可手动折叠）
watch(filteredDataTree, (tree) => {
  dataExpandedKeys.value = collectDataIds(tree)
})

// ── 字典数据表单 ─────────────────────────────────────
const dataModalVisible = ref(false)
const dataSaving = ref(false)
const dataTreeLoading = ref(false)
const editingData = ref<DictDataNode | null>(null)
const dataParentTree = ref<DictDataNode[]>([])
const dataForm = ref({
  name: '',
  value: '',
  parentId: undefined as string | undefined,
  sort: 0,
  isEnabled: true,
  remark: '',
})

function normalizeDataNodes(nodes: DictDataNode[]): DictDataNode[] {
  return nodes.map((n) => ({
    ...n,
    children: n.children?.length ? normalizeDataNodes(n.children) : undefined,
  })) as DictDataNode[]
}

async function loadDataParentTree(): Promise<void> {
  if (!currentType.value) return
  dataTreeLoading.value = true
  try {
    dataParentTree.value = normalizeDataNodes(await getDictDataTree(currentType.value.id))
  } catch {
    dataParentTree.value = []
  } finally {
    dataTreeLoading.value = false
  }
}

const dataParentOptions = computed<DictDataNode[]>(() => {
  if (!editingData.value) return dataParentTree.value
  const editingId = editingData.value.id
  const collect = (node: DictDataNode, out: Set<string>): Set<string> => {
    out.add(node.id)
    for (const child of node.children ?? []) collect(child, out)
    return out
  }
  const self = (function find(nodes: DictDataNode[]): DictDataNode | null {
    for (const n of nodes) {
      if (n.id === editingId) return n
      const hit = n.children?.length ? find(n.children) : null
      if (hit) return hit
    }
    return null
  })(dataParentTree.value)
  if (!self) return dataParentTree.value
  const excluded = collect(self, new Set())
  const prune = (nodes: DictDataNode[]): DictDataNode[] =>
    nodes
      .filter((n) => !excluded.has(n.id))
      .map((n) => ({ ...n, children: n.children ? prune(n.children) : undefined })) as DictDataNode[]
  return prune(dataParentTree.value)
})

function openDataCreate(parent: DictDataNode | null): void {
  editingData.value = null
  dataForm.value = { name: '', value: '', parentId: parent?.id, sort: 0, isEnabled: true, remark: '' }
  dataModalVisible.value = true
  loadDataParentTree()
}

function openDataEdit(record: DictDataNode): void {
  editingData.value = record
  dataForm.value = {
    name: record.name,
    value: record.value,
    parentId: record.parentId ?? undefined,
    sort: record.sort,
    isEnabled: record.isEnabled,
    remark: record.remark ?? '',
  }
  dataModalVisible.value = true
  loadDataParentTree()
}

async function handleSaveData(): Promise<void> {
  const name = dataForm.value.name.trim()
  const value = dataForm.value.value.trim()
  if (!name) {
    message.warning('请输入数据名称')
    return
  }
  if (!value) {
    message.warning('请输入数据值')
    return
  }
  dataSaving.value = true
  try {
    const payload = {
      parentId: dataForm.value.parentId ?? null,
      value,
      name,
      sort: dataForm.value.sort,
      isEnabled: dataForm.value.isEnabled,
      remark: dataForm.value.remark.trim() || undefined,
    }
    if (editingData.value) {
      await updateDictData(editingData.value.id, payload)
      message.success('已保存')
    } else {
      await createDictData({ ...payload, typeId: currentType.value!.id })
      message.success('已创建')
    }
    dataModalVisible.value = false
    fetchData()
  } catch {
    // 错误 toast 由全局 request 拦截器呈现
  } finally {
    dataSaving.value = false
  }
}

async function handleDeleteData(record: DictDataNode): Promise<void> {
  try {
    await deleteDictData(record.id)
    message.success('已删除')
    fetchData()
  } catch {
    // 删除有子级的数据被后端拒绝（DictDataCannotDeleteWithChildren），错误 toast 由全局拦截器呈现
  }
}

onMounted(() => {
  fetchTypeTree()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.page-container :deep(.page-header) {
  margin-bottom: @spacing-md;
}

.dict-layout {
  display: flex;
  gap: @spacing-xl;
  align-items: flex-start;
}

.dict-type-pane {
  width: 280px;
  flex: none;

  &__search {
    margin-bottom: @spacing-base;
  }
}

.dict-tree-node {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  width: 100%;
  min-width: 0;

  &__name {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__actions {
    display: none;
    align-items: center;
    margin-left: auto;
    flex: none;
  }

  &:hover &__actions {
    display: inline-flex;
  }
}

// 让树节点内容占满整行，actions 才能靠右对齐
.dict-type-pane :deep(.ant-tree-node-content-wrapper) {
  flex: 1;
  min-width: 0;
}

.dict-data-pane {
  flex: 1;
  min-width: 0;
}

.dict-form__number {
  width: 100%;
}

// global.less 对表格单元格强制 text-align: center !important，此处对文本列覆盖为左对齐
.page-container :deep(.cell-left) {
  text-align: left !important;
}
</style>
