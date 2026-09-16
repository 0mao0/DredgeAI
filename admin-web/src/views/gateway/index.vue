<template>
  <div class="page-container">
    <PageHeader title="网关配置" description="管理 YARP 代理路由、目标集群与限流策略，保存后即时生效" />

    <a-tabs v-model:active-key="activeTab" class="gateway-tabs">
      <a-tab-pane key="routes" tab="路由管理">
        <RoutesTab
          :routes="routes"
          :loading="routeLoading"
          :total="routeTotal"
          :page="routePage"
          :query="routeQuery"
          :cluster-options="clusterOptions"
          :can="can"
          @update:query="routeQuery = $event"
          @change="handleRouteTableChange"
          @create="openRouteCreate"
          @edit="openRouteEdit"
          @remove="handleRouteDelete"
        />
      </a-tab-pane>
      <a-tab-pane key="clusters" tab="集群管理">
        <ClustersTab
          :clusters="clusters"
          :loading="clusterLoading"
          :route-counts="routeCounts"
          :can="can"
          @create="openClusterCreate"
          @edit="openClusterEdit"
          @remove="handleClusterDelete"
          @view-routes="handleViewRoutes"
        />
      </a-tab-pane>
      <a-tab-pane key="policies" tab="限流策略">
        <PoliciesTab
          :policies="policies"
          :loading="policyLoading"
          :total="policyTotal"
          :page="policyPage"
          :query="policyQuery"
          :route-options="routeOptions"
          :can="can"
          @update:query="policyQuery = $event"
          @change="handlePolicyTableChange"
          @create="openPolicyCreate"
          @edit="openPolicyEdit"
          @remove="handlePolicyDelete"
        />
      </a-tab-pane>
    </a-tabs>

    <RouteFormModal
      v-model:open="routeModalVisible"
      :saving="routeSaving"
      :editing="editingRoute"
      :cluster-options="clusterOptions"
      @submit="handleRouteSubmit"
    />

    <ClusterFormModal
      v-model:open="clusterModalVisible"
      :saving="clusterSaving"
      :editing="editingCluster"
      @submit="handleClusterSubmit"
    />

    <PolicyFormModal
      v-model:open="policyModalVisible"
      :saving="policySaving"
      :editing="editingPolicy"
      :route-options="routeOptions"
      :global-policy-exists="globalPolicyExists"
      @submit="handlePolicySubmit"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PageHeader } from '@shared/web'
import {
  getProxyRoutes,
  createProxyRoute,
  updateProxyRoute,
  deleteProxyRoute,
  getProxyClusters,
  createProxyCluster,
  updateProxyCluster,
  deleteProxyCluster,
  getRateLimitPolicies,
  createRateLimitPolicy,
  updateRateLimitPolicy,
  deleteRateLimitPolicy,
} from '@/api/modules/gateway'
import type {
  ProxyRouteItem,
  ProxyRouteFormData,
  ProxyClusterItem,
  ProxyClusterFormData,
  RateLimitScopeValue,
  RateLimitPolicyItem,
  RateLimitPolicyFormData,
} from '@/api/modules/gateway'
import { usePagePermissions } from '@/composables/usePagePermissions'
import RoutesTab from './components/RoutesTab.vue'
import RouteFormModal from './components/RouteFormModal.vue'
import ClustersTab from './components/ClustersTab.vue'
import ClusterFormModal from './components/ClusterFormModal.vue'
import PoliciesTab from './components/PoliciesTab.vue'
import PolicyFormModal from './components/PolicyFormModal.vue'

const { can } = usePagePermissions()

const activeTab = ref('routes')

// ---- 路由列表 ----
const routePageSize = 15
const routes = ref<ProxyRouteItem[]>([])
const routeTotal = ref(0)
const routePage = ref(1)
const routeLoading = ref(false)
const routeQuery = ref<{ keyword?: string, clusterId?: string }>({})

async function loadRoutes(): Promise<void> {
  routeLoading.value = true
  try {
    const res = await getProxyRoutes({
      keyword: routeQuery.value.keyword?.trim() || undefined,
      clusterId: routeQuery.value.clusterId || undefined,
      skipCount: (routePage.value - 1) * routePageSize,
      maxResultCount: routePageSize,
    })
    routes.value = res.items
    routeTotal.value = res.totalCount
  } catch {
    // 错误提示由 request 拦截器统一 toast（后端错误码已中文化）
  } finally {
    routeLoading.value = false
  }
}

let filterTimer: ReturnType<typeof setTimeout> | undefined
watch(routeQuery, () => {
  clearTimeout(filterTimer)
  filterTimer = setTimeout(() => {
    routePage.value = 1
    void loadRoutes()
  }, 300)
}, { deep: true })

function handleRouteTableChange(paginationInfo: unknown): void {
  routePage.value = (paginationInfo as { current?: number } | null)?.current || 1
  void loadRoutes()
}

// ---- 集群列表（不分页） ----
const clusters = ref<ProxyClusterItem[]>([])
const clusterLoading = ref(false)

async function loadClusters(): Promise<void> {
  clusterLoading.value = true
  try {
    clusters.value = await getProxyClusters()
  } catch {
    // 错误提示由 request 拦截器统一 toast
  } finally {
    clusterLoading.value = false
  }
}

const clusterOptions = computed(() =>
  clusters.value.map((c) => ({
    value: c.clusterId,
    label: c.isEnabled ? c.clusterId : `${c.clusterId}（已禁用）`,
  })),
)

// ---- 各集群关联路由计数（删除门禁 + 跳转过滤用） ----
const routeCounts = ref<Record<string, number>>({})
/** 全量路由（限流策略 routeId 下拉选项数据源，与计数同一响应顺带赋值） */
const allRoutes = ref<ProxyRouteItem[]>([])

async function loadRouteCounts(): Promise<void> {
  try {
    const res = await getProxyRoutes({ skipCount: 0, maxResultCount: 1000 })
    const counts: Record<string, number> = {}
    for (const r of res.items) {
      counts[r.clusterId] = (counts[r.clusterId] ?? 0) + 1
    }
    routeCounts.value = counts
    allRoutes.value = res.items
  } catch {
    // 计数失败不阻塞页面；删除门禁退化为后端 ClusterInUse 校验
  }
}

const routeOptions = computed(() =>
  allRoutes.value.map((r) => ({
    value: r.routeId,
    label: r.isEnabled ? r.routeId : `${r.routeId}（已禁用）`,
  })),
)

// ---- 路由弹窗 ----
const routeModalVisible = ref(false)
const routeSaving = ref(false)
const editingRoute = ref<ProxyRouteItem | null>(null)

function openRouteCreate(): void {
  editingRoute.value = null
  routeModalVisible.value = true
}

function openRouteEdit(record: ProxyRouteItem): void {
  editingRoute.value = record
  routeModalVisible.value = true
}

async function handleRouteSubmit(data: ProxyRouteFormData): Promise<void> {
  routeSaving.value = true
  try {
    if (editingRoute.value) {
      await updateProxyRoute(editingRoute.value.id, data)
    } else {
      await createProxyRoute(data)
    }
    message.success('已保存，网关配置即时生效')
    routeModalVisible.value = false
    await Promise.all([loadRoutes(), loadRouteCounts()])
  } catch {
    // 弹窗保持打开，错误由拦截器 toast
  } finally {
    routeSaving.value = false
  }
}

async function handleRouteDelete(record: ProxyRouteItem): Promise<void> {
  try {
    await deleteProxyRoute(record.id)
    message.success('已删除，网关配置即时生效')
    if (routes.value.length === 1 && routePage.value > 1) routePage.value -= 1
    await Promise.all([loadRoutes(), loadRouteCounts()])
  } catch {
    // 错误由拦截器 toast
  }
}

// ---- 集群弹窗 ----
const clusterModalVisible = ref(false)
const clusterSaving = ref(false)
const editingCluster = ref<ProxyClusterItem | null>(null)

function openClusterCreate(): void {
  editingCluster.value = null
  clusterModalVisible.value = true
}

function openClusterEdit(record: ProxyClusterItem): void {
  editingCluster.value = record
  clusterModalVisible.value = true
}

async function handleClusterSubmit(data: ProxyClusterFormData): Promise<void> {
  clusterSaving.value = true
  try {
    if (editingCluster.value) {
      await updateProxyCluster(editingCluster.value.id, data)
    } else {
      await createProxyCluster(data)
    }
    message.success('已保存，网关配置即时生效')
    clusterModalVisible.value = false
    await loadClusters()
  } catch {
    // 弹窗保持打开，错误由拦截器 toast
  } finally {
    clusterSaving.value = false
  }
}

async function handleClusterDelete(record: ProxyClusterItem): Promise<void> {
  try {
    await deleteProxyCluster(record.id)
    message.success('已删除，网关配置即时生效')
    await Promise.all([loadClusters(), loadRouteCounts()])
  } catch {
    // 错误由拦截器 toast
  }
}

// ---- 限流策略列表 ----
const policyPageSize = 15
const policies = ref<RateLimitPolicyItem[]>([])
const policyTotal = ref(0)
const policyPage = ref(1)
const policyLoading = ref(false)
const policyQuery = ref<{ keyword?: string, scope?: string, routeId?: string }>({})

async function loadPolicies(): Promise<void> {
  policyLoading.value = true
  try {
    const res = await getRateLimitPolicies({
      keyword: policyQuery.value.keyword?.trim() || undefined,
      scope: policyQuery.value.scope === undefined ? undefined : Number(policyQuery.value.scope) as RateLimitScopeValue,
      routeId: policyQuery.value.routeId || undefined,
      skipCount: (policyPage.value - 1) * policyPageSize,
      maxResultCount: policyPageSize,
    })
    policies.value = res.items
    policyTotal.value = res.totalCount
  } catch {
    // 错误提示由 request 拦截器统一 toast（后端错误码已中文化）
  } finally {
    policyLoading.value = false
  }
}

let policyFilterTimer: ReturnType<typeof setTimeout> | undefined
watch(policyQuery, () => {
  clearTimeout(policyFilterTimer)
  policyFilterTimer = setTimeout(() => {
    policyPage.value = 1
    void loadPolicies()
  }, 300)
}, { deep: true })

function handlePolicyTableChange(paginationInfo: unknown): void {
  policyPage.value = (paginationInfo as { current?: number } | null)?.current || 1
  void loadPolicies()
}

/** 是否已存在全局策略（仅当前页，宽松提示用途；真正约束靠后端） */
const globalPolicyExists = computed(() => policies.value.some((p) => p.scope === 0))

// ---- 限流策略弹窗 ----
const policyModalVisible = ref(false)
const policySaving = ref(false)
const editingPolicy = ref<RateLimitPolicyItem | null>(null)

function openPolicyCreate(): void {
  editingPolicy.value = null
  policyModalVisible.value = true
}

function openPolicyEdit(record: RateLimitPolicyItem): void {
  editingPolicy.value = record
  policyModalVisible.value = true
}

async function handlePolicySubmit(data: RateLimitPolicyFormData): Promise<void> {
  policySaving.value = true
  try {
    if (editingPolicy.value) {
      await updateRateLimitPolicy(editingPolicy.value.id, data)
    } else {
      await createRateLimitPolicy(data)
    }
    message.success('已保存，网关配置即时生效')
    policyModalVisible.value = false
    await loadPolicies()
  } catch {
    // 弹窗保持打开，错误由拦截器 toast
  } finally {
    policySaving.value = false
  }
}

async function handlePolicyDelete(record: RateLimitPolicyItem): Promise<void> {
  try {
    await deleteRateLimitPolicy(record.id)
    message.success('已删除，网关配置即时生效')
    if (policies.value.length === 1 && policyPage.value > 1) policyPage.value -= 1
    await loadPolicies()
  } catch {
    // 错误由拦截器 toast
  }
}

// ---- 集群 → 路由跳转过滤 ----
function handleViewRoutes(clusterId: string): void {
  activeTab.value = 'routes'
  routePage.value = 1
  routeQuery.value = { ...routeQuery.value, clusterId }
}

onMounted(() => {
  void loadClusters()
  void loadRoutes()
  void loadRouteCounts()
  void loadPolicies()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.gateway-tabs {
  :deep(.ant-tabs-nav) {
    margin-bottom: @spacing-sm;
  }

  :deep(.ant-tabs-tab) {
    padding: 6px 10px;
  }
}

.page-container :deep(.page-header) {
  margin-bottom: @spacing-md;
}
</style>
