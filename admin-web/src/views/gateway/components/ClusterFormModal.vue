<template>
  <a-modal
    :open="open"
    :title="editing ? '编辑集群' : '新增集群'"
    :width="640"
    :confirm-loading="saving"
    @ok="handleOk"
    @cancel="emit('update:open', false)"
  >
    <a-form ref="formRef" :model="form" :rules="rules" layout="vertical">
      <a-form-item label="集群 ID" name="clusterId">
        <a-input v-model:value="form.clusterId" :maxlength="128" placeholder="如 bidcompare-cluster" />
      </a-form-item>
      <a-form-item label="描述" name="description">
        <a-textarea v-model:value="form.description" :maxlength="256" :rows="2" placeholder="可选" />
      </a-form-item>
      <a-form-item label="目标节点" required>
        <div class="cluster-form__destinations">
          <div v-for="(row, index) in destinations" :key="index" class="cluster-form__destination-row">
            <a-input v-model:value="row.name" class="cluster-form__destination-name" placeholder="名称，如 d1" />
            <a-input v-model:value="row.address" class="cluster-form__destination-address" placeholder="https://localhost:44361/" />
            <a-input v-model:value="row.health" class="cluster-form__destination-health" placeholder="健康检查地址（可选）" />
            <AppButton variant="link" size="sm" danger :disabled="destinations.length <= 1" @click="removeDestination(index)">
              <DeleteOutlined />
            </AppButton>
          </div>
          <AppButton variant="link" size="sm" class="cluster-form__add" @click="addDestination">
            <PlusOutlined />
            添加节点
          </AppButton>
        </div>
      </a-form-item>
      <a-form-item label="启用" name="isEnabled">
        <a-switch v-model:checked="form.isEnabled" />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons-vue'
import { AppButton } from '@shared/web'
import type { ProxyClusterFormData, ProxyClusterItem } from '@/api/modules/gateway'

interface DestinationRow {
  name: string
  address: string
  health: string
}

const props = defineProps<{
  open: boolean
  saving: boolean
  editing: ProxyClusterItem | null
}>()

const emit = defineEmits<{
  'update:open': [open: boolean]
  'submit': [data: ProxyClusterFormData]
}>()

const formRef = ref<FormInstance>()
const form = reactive({
  clusterId: '',
  description: '',
  isEnabled: true,
})
const destinations = ref<DestinationRow[]>([{ name: '', address: '', health: '' }])

const rules = {
  clusterId: [{ required: true, message: '请输入集群 ID', trigger: 'blur' }],
}

const ADDRESS_PATTERN = /^https?:\/\/.+/

watch(() => props.open, (open) => {
  if (!open) return
  const c = props.editing
  form.clusterId = c?.clusterId ?? ''
  form.description = c?.description ?? ''
  form.isEnabled = c?.isEnabled ?? true
  destinations.value = c
    ? Object.entries(c.destinations).map(([name, d]) => ({
        name,
        address: d.address,
        health: d.health ?? '',
      }))
    : [{ name: '', address: '', health: '' }]
  if (destinations.value.length === 0) {
    destinations.value = [{ name: '', address: '', health: '' }]
  }
  formRef.value?.clearValidate()
})

function addDestination(): void {
  destinations.value.push({ name: '', address: '', health: '' })
}

function removeDestination(index: number): void {
  destinations.value.splice(index, 1)
}

async function handleOk(): Promise<void> {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  // 目标节点行级校验：a-form 无法直接托管动态行数组，提交前统一校验
  if (destinations.value.length === 0) {
    message.error('至少需要一个目标节点')
    return
  }
  const names = new Set<string>()
  for (const row of destinations.value) {
    const name = row.name.trim()
    if (!name) {
      message.error('目标节点名称不能为空')
      return
    }
    if (names.has(name)) {
      message.error(`目标节点名称重复：${name}`)
      return
    }
    names.add(name)
    if (!ADDRESS_PATTERN.test(row.address.trim())) {
      message.error(`节点 ${name} 的地址必须是 http/https 绝对地址`)
      return
    }
  }
  const destinationsMap: ProxyClusterFormData['destinations'] = {}
  for (const row of destinations.value) {
    destinationsMap[row.name.trim()] = {
      address: row.address.trim(),
      health: row.health.trim() || undefined,
    }
  }
  emit('submit', {
    clusterId: form.clusterId.trim(),
    description: form.description.trim() || undefined,
    destinations: destinationsMap,
    isEnabled: form.isEnabled,
  })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.cluster-form__destinations {
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;
}

.cluster-form__destination-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}

.cluster-form__destination-name {
  width: 120px;
  flex-shrink: 0;
}

.cluster-form__destination-address {
  flex: 1;
  min-width: 0;
}

.cluster-form__destination-health {
  width: 180px;
  flex-shrink: 0;
}

.cluster-form__add {
  align-self: flex-start;
  padding: 0;
}
</style>
