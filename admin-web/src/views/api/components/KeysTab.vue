<template>
  <div class="keys-tab">
    <DataTable
      :columns="columns"
      :data-source="models"
      :pagination="{ pageSize: 15 }"
      row-key="id"
    >
      <template #toolbarExtra>
        <AppButton variant="primary" @click="emit('openCreate')">
          <PlusOutlined />
          添加模型
        </AppButton>
      </template>
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.key === 'index'">
          {{ index + 1 }}
        </template>
        <template v-else-if="column.key === 'status'">
          <a-tag :color="record.status === '启用' ? 'green' : 'default'">{{ record.status }}</a-tag>
        </template>
        <template v-else-if="column.key === 'consumption'">
          {{ formatConsumption(record.consumption) }}
        </template>
        <template v-else-if="column.key === 'doc'">
          <AppButton variant="link" size="sm" @click="openDoc(record.docUrl)">
            <FileTextOutlined /> 文档
          </AppButton>
        </template>
        <template v-else-if="column.key === 'action'">
          <AppButton variant="link" size="sm" @click="emit('edit', record)">编辑</AppButton>
          <a-popconfirm title="确认删除？" @confirm="emit('delete', record.id)">
            <AppButton variant="link" size="sm" danger>删除</AppButton>
          </a-popconfirm>
        </template>
      </template>
    </DataTable>

    <!-- Create Modal -->
    <a-modal :open="createOpen" title="添加模型" @update:open="emit('update:createOpen', $event)" @ok="emit('create')" @cancel="emit('cancelCreate')">
      <a-form layout="horizontal" :label-col="{ span: 7 }" :wrapper-col="{ span: 17 }">
        <a-form-item label="模型名称" required>
          <a-input v-model:value="newModel.name" placeholder="用户自定义名称" />
        </a-form-item>
        <a-form-item label="实际模型" required>
          <a-select v-model:value="newModel.actualModel" :options="deployedModelOptions" placeholder="选择已部署的模型" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="newModel.modelType" :options="modelTypeOptions" placeholder="选择模型分类" />
        </a-form-item>
        <a-form-item label="IP 地址">
          <a-input v-model:value="newModel.ipAddress" placeholder="如：192.168.1.100" />
        </a-form-item>
        <a-form-item label="API 文档链接">
          <a-input v-model:value="newModel.docUrl" placeholder="https://docs.example.com/model" />
        </a-form-item>
        <a-form-item label="状态">
          <a-select v-model:value="newModel.status" :options="statusOptions" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- Edit Modal -->
    <a-modal :open="editOpen" title="编辑模型" @update:open="emit('update:editOpen', $event)" @ok="emit('editOk')">
      <a-form v-if="editTarget" layout="horizontal" :label-col="{ span: 7 }" :wrapper-col="{ span: 17 }">
        <a-form-item label="模型名称" required>
          <a-input v-model:value="editForm.name" placeholder="用户自定义名称" />
        </a-form-item>
        <a-form-item label="实际模型" required>
          <a-select v-model:value="editForm.actualModel" :options="deployedModelOptions" placeholder="选择已部署的模型" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="editForm.modelType" :options="modelTypeOptions" placeholder="选择模型分类" />
        </a-form-item>
        <a-form-item label="IP 地址">
          <a-input v-model:value="editForm.ipAddress" placeholder="如：192.168.1.100" />
        </a-form-item>
        <a-form-item label="API 文档链接">
          <a-input v-model:value="editForm.docUrl" placeholder="https://docs.example.com/model" />
        </a-form-item>
        <a-form-item label="状态">
          <a-select v-model:value="editForm.status" :options="statusOptions" />
        </a-form-item>
        <a-form-item label="创建日期">
          <span class="readonly-field">{{ editTarget.createdAt }}</span>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import { PlusOutlined, FileTextOutlined } from '@ant-design/icons-vue'
import { formatConsumption } from '../utils'
import type { ModelItem, ModelForm, SelectOption } from '../types'

defineProps<{
  models: ModelItem[]
  createOpen: boolean
  editOpen: boolean
  editTarget: { id: string, createdAt: string } | null
  deployedModelOptions: SelectOption[]
  modelTypeOptions: SelectOption[]
  statusOptions: SelectOption[]
}>()

const emit = defineEmits<{
  'update:createOpen': [value: boolean]
  'update:editOpen': [value: boolean]
  'openCreate': []
  'create': []
  'editOk': []
  'cancelCreate': []
  'edit': [record: ModelItem]
  'delete': [id: string]
}>()

const newModel = defineModel<ModelForm>('newModel', { required: true })
const editForm = defineModel<ModelForm>('editForm', { required: true })

const columns: DataTableColumn[] = [
  { title: '序号', key: 'index', width: 70, minWidth: 60, resizable: true },
  { title: '模型名称', dataIndex: 'name', key: 'name', width: 200, minWidth: 140, resizable: true },
  { title: '模型类型', dataIndex: 'modelType', key: 'modelType', width: 120, minWidth: 100, resizable: true },
  { title: '消耗额度', key: 'consumption', width: 140, minWidth: 110, resizable: true },
  { title: '状态', key: 'status', width: 100, minWidth: 80, resizable: true },
  { title: 'API 文档', key: 'doc', width: 110, minWidth: 90, resizable: true },
  { title: '操作', key: 'action', width: 130, minWidth: 130, fixed: 'right', resizable: true },
]

function openDoc(url: string): void {
  window.open(url, '_blank')
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.readonly-field {
  color: @text-secondary;
  padding: 4px 0;
  display: inline-block;
  line-height: 32px;
}
</style>
