<template>
  <div class="standard-page">
    <PageHeader title="标准查询">
      <template #extra>
        <a-button size="small" @click="historyDrawer = true">
          <history-outlined /> 历史记录
        </a-button>
      </template>
    </PageHeader>

    <div class="standard-body">
      <!-- 左侧：分类 -->
      <aside class="standard-left">
        <SectionCard title="标准分类" nopad class="left-tree">
          <a-tree :tree-data="categoryTree" :default-expand-all="true" class="category-tree">
            <template #title="{ name, count }">
              <span class="tree-node">
                <span class="tree-name">{{ name }}</span>
                <span class="tree-count">{{ count }}</span>
              </span>
            </template>
          </a-tree>
        </SectionCard>
      </aside>

      <!-- 中间：阅读器 -->
      <main class="standard-center">
        <div v-if="results.length" class="reader-body">
          <SectionCard title="规范阅读器" nopad class="reader-card">
            <template #extra>
              <a-segmented
                v-model:value="readerMode"
                :options="modeOptions"
                class="reader-segmented"
              />
            </template>
            <!-- 文本模式 -->
          <div v-if="readerMode === 'text'" class="reader-content">
            <div class="reader-outline">
              <div class="outline-title">目录</div>
              <div
                v-for="(item, i) in results"
                :key="item.id"
                class="outline-item"
                :class="{ 'outline-item--active': i === activeResultIdx }"
                @click="activeResultIdx = i"
              >
                <div class="outline-code">{{ item.code }}</div>
                <div class="outline-name">{{ item.title }}</div>
              </div>
            </div>
            <div class="reader-article">
              <div class="article-header">
                <div class="article-code">{{ currentResult?.code }}</div>
                <h2 class="article-title">{{ currentResult?.title }}</h2>
                <div class="article-source">{{ currentResult?.source }}</div>
              </div>
              <div class="article-section">
                <div class="article-section-tag">{{ currentResult?.match }}</div>
              </div>
              <div class="article-body">
                {{ currentResult?.excerpt }}
              </div>
              <div class="article-foot">
                <a-button type="primary" ghost size="small">下载原文</a-button>
                <a-button type="link" size="small">引用到报告</a-button>
              </div>
            </div>
          </div>

          <!-- PDF 模式 -->
          <div v-if="readerMode === 'pdf'" class="reader-content reader-content--pdf">
            <div class="pdf-viewer">
              <div class="pdf-toolbar">
                <div class="pdf-info">{{ currentResult?.code }} — {{ currentResult?.title }}</div>
                <div class="pdf-actions">
                  <file-pdf-outlined /> 预览 {{ activeResultIdx + 1 }}/{{ results.length }}
                </div>
              </div>
              <div class="pdf-pages">
                <div class="pdf-page" v-for="p in 3" :key="p">
                  <div class="pdf-page-header">{{ currentResult?.code }} — {{ p }}</div>
                  <div class="pdf-page-body">
                    <p>第 {{ p }} 页内容：{{ currentResult?.excerpt }}</p>
                    <p v-if="p === 1">本标准规定了组织建立、实施、维护和持续改进质量管理体系的要求。</p>
                    <p v-if="p === 2">本标准适用于各种类型、不同规模和提供不同产品和服务的组织。</p>
                    <p v-if="p === 3">本标准所代替标准的历次版本发布情况为：——GB/T 19001-2008。</p>
                  </div>
                  <div class="pdf-page-footer">{{ currentResult?.source }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- 图谱模式 -->
          <div v-if="readerMode === 'graph'" class="reader-content reader-content--graph">
            <div class="graph-canvas">
              <div class="graph-root">
                <div class="graph-node graph-node--root">{{ currentResult?.code }}</div>
                <div class="graph-children">
                  <div class="graph-node" v-for="ch in graphChildren" :key="ch">{{ ch }}</div>
                </div>
              </div>
              <div class="graph-hint">图谱模式：展现标准的结构层级与关联关系</div>
            </div>
          </div>
          </SectionCard>
        </div>

        <div v-else class="reader-empty">
          <div class="empty-icon">
            <FileSearchOutlined />
          </div>
          <p>输入关键词或选择左侧分类，开始查阅标准规范</p>
        </div>
      </main>

      <!-- 右侧：AI 对话 -->
      <aside class="standard-right">
        <div class="chat-panel">
          <div class="chat-header">
            <div class="chat-header-icon">
              <RobotOutlined />
            </div>
            <span>AI 助手</span>
          </div>
          <div class="chat-body">
            <div class="chat-messages" ref="chatBox">
              <div
                v-for="(msg, i) in messages"
                :key="i"
                class="chat-msg"
                :class="`chat-msg--${msg.role}`"
              >
                <div class="chat-avatar">{{ msg.role === 'user' ? '我' : 'AI' }}</div>
                <div class="chat-bubble">{{ msg.content }}</div>
              </div>
              <div v-if="messages.length <= 1" class="chat-hint">
                我是你的标准规范助手，输入问题即可获取解答。
              </div>
            </div>
            <div class="chat-foot">
              <a-input
                v-model:value="chatInput"
                placeholder="向 AI 提问..."
                @pressEnter="handleChat"
              >
                <template #suffix>
                  <SendOutlined class="send-btn" @click="handleChat" />
                </template>
              </a-input>
            </div>
          </div>
        </div>
      </aside>
    </div>

    <a-drawer
      v-model:open="historyDrawer"
      title="查询历史"
      placement="right"
      width="400"
      destroy-on-close
    >
      <div class="drawer-list">
        <div
          v-for="item in history"
          :key="item.id"
          class="drawer-item"
          @click="queryInput = item.query; historyDrawer = false; handleSearch()"
        >
          <div class="drawer-query">{{ item.query }}</div>
          <div class="drawer-meta">{{ item.date }} · {{ item.resultCount }} 条</div>
        </div>
        <div v-if="!history.length" class="drawer-empty">
          暂无查询历史
        </div>
      </div>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  FileSearchOutlined, RobotOutlined, SendOutlined,
  HistoryOutlined, FilePdfOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import { getStandardResult, getStandardCategories, getStandardHistory } from '@/api/modules/standard'
import type { StandardResult, StandardCategory, StandardSearchHistory } from '@/types'

interface ChatMessage { role: 'user' | 'ai'; content: string }

type ReaderMode = 'text' | 'pdf' | 'graph'

const queryInput = ref('')
const results = ref<StandardResult[]>([])
const history = ref<StandardSearchHistory[]>([])
const categories = ref<StandardCategory[]>([])
const chatInput = ref('')
const historyDrawer = ref(false)
const chatBox = ref<HTMLElement>()
const messages = ref<ChatMessage[]>([
  { role: 'ai', content: '你好！我可以帮你查询行业标准规范。' },
])

const readerMode = ref<ReaderMode>('text')
const activeResultIdx = ref(0)

const modeOptions = [
  { value: 'text', label: '文本' },
  { value: 'pdf', label: 'PDF' },
  { value: 'graph', label: '图谱' },
]

const currentResult = computed(() => results.value[activeResultIdx.value])

const graphChildren = computed(() => {
  if (!currentResult.value) return []
  return [
    `总则 — ${currentResult.value.title}`,
    '术语和定义',
    currentResult.value.match,
    '实施要求',
    '评价与改进',
  ]
})

interface CategoryTreeNode { key: string; name: string; count: number; children?: CategoryTreeNode[] }

const categoryTree = computed(() => categories.value.map(function mapCategory(c: StandardCategory): CategoryTreeNode {
  return { key: c.id, name: c.name, count: c.count, children: c.children?.map(mapCategory) }
}))

function handleSearch(): void {
  if (!queryInput.value.trim()) return
}

function handleChat(): void {
  if (!chatInput.value.trim()) return
  messages.value.push({ role: 'user', content: chatInput.value })
  chatInput.value = ''
  setTimeout(() => {
    messages.value.push({ role: 'ai', content: '收到你的问题，正在查阅相关规范...' })
  }, 600)
  setTimeout(() => {
    chatBox.value?.scrollTo({ top: chatBox.value.scrollHeight, behavior: 'smooth' })
  }, 100)
}

onMounted(async () => {
  const [r, c, h] = await Promise.all([getStandardResult(), getStandardCategories(), getStandardHistory()])
  results.value = r
  categories.value = c
  history.value = h
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

/* ═══════════════════════════════════════════
   Body — 三栏布局
   ═══════════════════════════════════════════ */
.standard-body {
  flex: 1;
  min-height: 0;
  display: flex;
  gap: @spacing-xl;
}

/* ═══════════════════════════════════════════
   左栏 — 分类树
   ═══════════════════════════════════════════ */
.standard-left {
  width: 220px;
  flex-shrink: 0;
}

.left-tree {
  height: 100%;
  display: flex;
  flex-direction: column;
}
.left-tree :deep(.section-card-body) {
  flex: 1;
  min-height: 0;
}
.category-tree {
  flex: 1;
  overflow-y: auto;
  padding: @spacing-sm 0;
}

.tree-node {
  display: inline-flex;
  align-items: center;
  gap: @spacing-sm;
}

.tree-name { font-size: @font-size-sm; color: @text-primary; }

.tree-count {
  font-size: 11px;
  color: @text-tertiary;
  background: @divider-color;
  padding: 0 6px;
  border-radius: 8px;
  line-height: 18px;
}

/* 历史记录抽屉 */
.drawer-list {
  display: flex;
  flex-direction: column;
}
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

/* ═══════════════════════════════════════════
   中栏 — 阅读器
   ═══════════════════════════════════════════ */
.standard-center {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: @spacing-lg;
  min-height: 0;
}

.reader-card {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.reader-card :deep(.section-card-body) {
  flex: 1;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

.reader-segmented {
  :deep(.ant-segmented-item) {
    padding: 2px 8px;
  }
}

.reader-content {
  flex: 1;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

/* 文本模式 — 左目录右正文 */
.reader-outline {
  width: 200px;
  flex-shrink: 0;
  border-right: 1px solid @divider-color;
  overflow-y: auto;
  padding: @spacing-sm 0;
}
.outline-title {
  font-size: @font-size-xs;
  font-weight: @font-weight-semibold;
  color: @text-tertiary;
  padding: @spacing-sm @spacing-xl @spacing-sm;
  text-transform: uppercase;
  letter-spacing: 1px;
}
.outline-item {
  padding: @spacing-sm @spacing-xl;
  cursor: pointer;
  border-left: 2px solid transparent;
  transition: all @transition-fast;
  &:hover { background: @surface-hover; }
  &--active {
    background: color-mix(in srgb, @brand-primary 6%, transparent);
    border-left-color: @brand-primary;
  }
}
.outline-code { font-size: 11px; font-weight: @font-weight-semibold; color: @brand-primary; margin-bottom: 2px; }
.outline-name { font-size: @font-size-sm; color: @text-primary; }

.reader-article {
  flex: 1;
  overflow-y: auto;
  padding: @spacing-xl @spacing-2xl;
}
.article-header { margin-bottom: @spacing-lg; padding-bottom: @spacing-lg; border-bottom: 1px solid @divider-color; }
.article-code { font-size: @font-size-sm; font-weight: @font-weight-semibold; color: @brand-primary; margin-bottom: @spacing-xs; }
.article-title { font-size: @font-size-xl; font-weight: @font-weight-semibold; color: @text-primary; margin: 0 0 @spacing-xs; }
.article-source { font-size: @font-size-xs; color: @text-tertiary; }
.article-section { margin-bottom: @spacing-lg; }
.article-section-tag {
  display: inline-block;
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @success;
  background: color-mix(in srgb, @success 10%, transparent);
  padding: 2px 10px;
  border-radius: @radius-sm;
}
.article-body {
  font-size: @font-size-base;
  color: @text-primary;
  line-height: 1.8;
  padding: @spacing-lg;
  background: @content-bg;
  border-radius: @radius-base;
  margin-bottom: @spacing-lg;
  border-left: 3px solid @brand-primary;
}
.article-foot {
  display: flex;
  gap: @spacing-sm;
}

/* PDF 模式 */
.reader-content--pdf {
  overflow-y: auto;
  padding: @spacing-lg @spacing-2xl;
  justify-content: flex-start;
}
.pdf-viewer {
  width: 100%;
  max-width: 700px;
  margin: 0 auto;
}
.pdf-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: @spacing-sm @spacing-md;
  background: @content-bg;
  border-radius: @radius-base @radius-base 0 0;
  border: 1px solid @border-color;
  border-bottom: none;
  font-size: @font-size-sm;
  color: @text-secondary;
}
.pdf-actions { color: @text-tertiary; }
.pdf-pages { display: flex; flex-direction: column; gap: 2px; }
.pdf-page {
  background: @card-bg;
  border: 1px solid @border-color;
  padding: @spacing-2xl @spacing-2xl @spacing-xl;
  box-shadow: @shadow-sm;
}
.pdf-page-header {
  font-size: @font-size-xs;
  color: @text-tertiary;
  text-align: center;
  border-bottom: 1px solid @divider-color;
  padding-bottom: @spacing-sm;
  margin-bottom: @spacing-lg;
}
.pdf-page-body {
  min-height: 300px;
  p { font-size: @font-size-base; line-height: 1.8; color: @text-primary; margin-bottom: @spacing-md; }
}
.pdf-page-footer {
  font-size: @font-size-xs;
  color: @text-tertiary;
  text-align: center;
  border-top: 1px solid @divider-color;
  padding-top: @spacing-sm;
  margin-top: @spacing-lg;
}

/* 图谱模式 */
.reader-content--graph {
  justify-content: center;
  align-items: center;
  padding: @spacing-2xl;
}
.graph-canvas {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: @spacing-xl;
  width: 100%;
  max-width: 500px;
}
.graph-root {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: @spacing-lg;
  width: 100%;
}
.graph-node {
  padding: @spacing-md @spacing-xl;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  box-shadow: @shadow-sm;
  font-size: @font-size-sm;
  color: @text-primary;
  text-align: center;
  &--root {
    background: @brand-gradient;
    color: white;
    font-weight: @font-weight-semibold;
    border: none;
    font-size: @font-size-base;
  }
}
.graph-children {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: @spacing-sm;
  position: relative;
  &::before {
    content: '';
    position: absolute;
    top: -@spacing-lg;
    left: 50%;
    width: 1px;
    height: @spacing-lg;
    background: @divider-color;
  }
}
.graph-hint {
  font-size: @font-size-xs;
  color: @text-tertiary;
  text-align: center;
}

.reader-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: @spacing-md;
  color: @text-tertiary;
}
.empty-icon { font-size: 48px; color: @text-tertiary; opacity: 0.4; }

/* ═══════════════════════════════════════════
   右栏 — AI 对话
   ═══════════════════════════════════════════ */
.standard-right { width: 300px; flex-shrink: 0; }

.chat-panel {
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  box-shadow: @shadow-sm;
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.chat-header {
  display: flex; align-items: center; gap: @spacing-sm;
  padding: @spacing-md @spacing-xl;
  border-bottom: 1px solid @divider-color;
  font-size: @font-size-base;
  font-weight: @font-weight-semibold;
  color: @text-primary;
  flex-shrink: 0;
}
.chat-header-icon {
  width: 28px; height: 28px; border-radius: @radius-sm;
  background: @brand-gradient;
  display: flex; align-items: center; justify-content: center;
  font-size: 14px; color: white;
}
.chat-body { flex: 1; min-height: 0; display: flex; flex-direction: column; }
.chat-messages {
  flex: 1; min-height: 0; overflow-y: auto;
  padding: @spacing-lg; display: flex; flex-direction: column; gap: @spacing-md;
}
.chat-hint { text-align: center; font-size: @font-size-xs; color: @text-tertiary; padding: @spacing-xl 0; }
.chat-msg { display: flex; gap: @spacing-sm; animation: msg-in 0.3s ease both; &--user { flex-direction: row-reverse; } }
@keyframes msg-in { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: translateY(0); } }
.chat-avatar {
  width: 26px; height: 26px; border-radius: 50%; background: @brand-gradient; color: white;
  display: flex; align-items: center; justify-content: center;
  font-size: 11px; font-weight: @font-weight-semibold; flex-shrink: 0;
}
.chat-bubble {
  background: @content-bg; padding: 8px 12px;
  border-radius: 14px 14px 14px 4px;
  font-size: @font-size-sm; max-width: 80%; line-height: 1.55; word-break: break-word;
  .chat-msg--user & {
    background: @brand-primary; color: white; border-radius: 14px 14px 4px 14px;
  }
}
.chat-foot { padding: @spacing-md @spacing-lg; border-top: 1px solid @divider-color; flex-shrink: 0; }
.send-btn { font-size: 16px; color: @brand-primary; cursor: pointer; &:hover { opacity: 0.7; } }
</style>
