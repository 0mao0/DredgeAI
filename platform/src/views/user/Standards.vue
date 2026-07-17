<template>
  <div class="page-container">
    <div class="page-header">
      <h2>标准查询</h2>
      <p>自然语言检索行业标准与规范条款</p>
    </div>

    <a-tabs v-model:activeKey="activeTab" class="app-tabs">
      <a-tab-pane key="current" tab="当前任务">
        <a-card class="query-card">
          <a-input-search
            v-model:value="queryText"
            placeholder="输入关键词、标准编号或自然语言描述..."
            enter-button="查询"
            size="large"
            @search="handleSearch"
          />
          <div class="quick-tags">
            <span class="tag-label">常用标准：</span>
            <a-tag
              v-for="tag in quickTags"
              :key="tag"
              color="default"
              style="cursor: pointer"
              @click="queryText = tag; handleSearch()"
            >
              {{ tag }}
            </a-tag>
          </div>
        </a-card>

        <div v-if="hasSearched" style="margin-top: 24px">
          <a-card v-for="item in results" :key="item.code" class="result-card">
            <div class="result-header">
              <span class="result-code">{{ item.code }}</span>
              <a-tag color="#00c9b7">{{ item.title }}</a-tag>
            </div>
            <div class="result-match">
              <span class="match-label">命中条款：</span>{{ item.match }}
            </div>
            <div class="result-excerpt">
              <span class="match-label">原文摘要：</span>{{ item.excerpt }}
            </div>
            <div class="result-actions">
              <a-button type="link" size="small">查看原文</a-button>
              <a-button type="link" size="small">引用追溯</a-button>
              <a-button type="link" size="small">继续追问</a-button>
            </div>
          </a-card>

          <a-card class="followup-card">
            <template #title>继续追问</template>
            <a-space direction="vertical" style="width: 100%">
              <a-input-search placeholder="针对查询结果继续提问..." enter-button="发送" />
              <div class="suggested-questions">
                <span style="font-size: 13px; color: #999; margin-right: 8px">推荐问题：</span>
                <a-button size="small" type="dashed" v-for="q in suggestedQuestions" :key="q">{{ q }}</a-button>
              </div>
            </a-space>
          </a-card>
        </div>
      </a-tab-pane>

      <a-tab-pane key="history" tab="历史记录">
        <a-card class="query-card">
          <a-table :dataSource="historyData" :columns="historyColumns" :pagination="{ pageSize: 10 }" row-key="id">
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'resultCount'">
                <a-tag color="#00c9b7">{{ record.resultCount }} 条</a-tag>
              </template>
              <template v-if="column.key === 'action'">
                <a-button type="link" size="small">查看结果</a-button>
                <a-button type="link" size="small">再次查询</a-button>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { standardsResult, standardsSearchHistory } from '@/mock/data'

const activeTab = ref('current')
const queryText = ref('')
const hasSearched = ref(false)
const results = ref(standardsResult)

const quickTags = ['GB/T 19001', 'GB 50300', '质量管理体系', '施工质量验收']
const suggestedQuestions = ['适用范围有哪些？', '与其他条款的关联？', '最新修订版本？']

const historyData = ref(standardsSearchHistory)
const historyColumns = [
  { title: '查询内容', dataIndex: 'query', key: 'query' },
  { title: '查询日期', dataIndex: 'date', key: 'date' },
  { title: '结果数', dataIndex: 'resultCount', key: 'resultCount' },
  { title: '操作', key: 'action' },
]

function handleSearch() {
  hasSearched.value = true
}
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.query-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}

.quick-tags {
  margin-top: 16px;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
}
.tag-label {
  font-size: 13px;
  color: @text-secondary;
  margin-right: 4px;
}

.result-card {
  margin-bottom: 16px;
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}
.result-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}
.result-code {
  font-weight: 700;
  font-size: 15px;
  color: @primary-color;
}
.match-label {
  font-weight: 600;
  font-size: 13px;
  color: @text-secondary;
}
.result-match, .result-excerpt {
  font-size: 14px;
  color: @text-primary;
  margin-bottom: 8px;
  line-height: 1.6;
}
.result-actions {
  border-top: 1px solid @border-color;
  margin-top: 12px;
  padding-top: 8px;
}

.followup-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}
.suggested-questions {
  margin-top: 12px;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}
</style>
