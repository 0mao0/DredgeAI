<template>
  <div class="page-container">
    <PageHeader title="标准查询" description="自然语言检索行业标准与规范条款" />

    <SectionCard class="mb-16">
      <a-input-search
        v-model:value="queryInput"
        placeholder="输入关键词、标准编号或自然语言查询..."
        enter-button="查询"
        size="large"
        @search="handleSearch"
      />
      <div class="quick-questions">
        <span class="quick-label">推荐问题：</span>
        <a-tag
          v-for="(q, i) in recommendedQuestions"
          :key="i"
          class="quick-tag"
          @click="queryInput = q; handleSearch()"
        >
          {{ q }}
        </a-tag>
      </div>
    </SectionCard>

    <a-row :gutter="[16, 16]">
      <a-col :span="16">
        <SectionCard title="查询结果" class="mb-16">
          <template #extra>
            <a-tag color="blue">命中 {{ results.length }} 条</a-tag>
          </template>
          <div class="result-list">
            <div
              v-for="item in results"
              :key="item.id"
              class="result-card"
            >
              <div class="result-header">
                <span class="result-code">{{ item.code }}</span>
                <span class="result-title">{{ item.title }}</span>
              </div>
              <div class="result-match">
                <tag-outlined />
                <span>{{ item.match }}</span>
              </div>
              <div class="result-excerpt">{{ item.excerpt }}</div>
              <div class="result-source">
                <link-outlined />
                <span>{{ item.source }}</span>
              </div>
            </div>
          </div>
        </SectionCard>
      </a-col>

      <a-col :span="8">
        <SectionCard title="查询历史" class="mb-16">
          <a-list :data-source="history" size="small">
            <template #renderItem="{ item }">
              <a-list-item class="history-item" @click="queryInput = item.query; handleSearch()">
                <a-list-item-meta>
                  <template #title>
                    <span class="history-query">{{ item.query }}</span>
                  </template>
                  <template #description>
                    {{ item.date }} · 命中 {{ item.resultCount }} 条
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
        </SectionCard>

        <SectionCard title="标准分类">
          <a-tree :tree-data="categoryTree" :default-expand-all="true">
            <template #title="{ name, count }">
              <span class="tree-name">{{ name }}</span>
              <span class="tree-count">{{ count }}</span>
            </template>
          </a-tree>
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { TagOutlined, LinkOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
import { getStandardResult, getStandardHistory, getStandardCategories, getRecommendedQuestions } from '@/api/modules/standard'
import type { StandardResult, StandardSearchHistory, StandardCategory } from '@/types'

const queryInput = ref('')
const results = ref<StandardResult[]>([])
const history = ref<StandardSearchHistory[]>([])
const recommendedQuestions = ref<string[]>([])
const categories = ref<StandardCategory[]>([])

const categoryTree = computed(() => categories.value.map(mapCategory))

interface CategoryTreeNode { key: string; name: string; count: number; children?: CategoryTreeNode[] }

function mapCategory(c: StandardCategory): CategoryTreeNode {
  return {
    key: c.id,
    name: c.name,
    count: c.count,
    children: c.children?.map(mapCategory),
  }
}

function handleSearch(): void {
  if (!queryInput.value.trim()) return
}

onMounted(async () => {
  const [r, h, q, c] = await Promise.all([
    getStandardResult(), getStandardHistory(), getRecommendedQuestions(), getStandardCategories(),
  ])
  results.value = r
  history.value = h
  recommendedQuestions.value = q
  categories.value = c
})
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.mb-16 { margin-bottom: @spacing-lg; }

.quick-questions {
  display: flex; align-items: center; flex-wrap: wrap; gap: @spacing-sm;
  margin-top: @spacing-md;
}
.quick-label { font-size: @font-size-sm; color: @text-secondary; }
.quick-tag {
  cursor: pointer;
  &:hover { color: @brand-primary; border-color: @brand-primary; }
}

.result-list { display: flex; flex-direction: column; gap: @spacing-md; }
.result-card {
  background: @content-bg;
  border-radius: @radius-base;
  padding: @spacing-lg;
  border: 1px solid @border-color;
  transition: all @transition-base;
  &:hover { border-color: @brand-primary; box-shadow: @shadow-sm; }
}
.result-header {
  display: flex; align-items: center; gap: @spacing-sm; margin-bottom: @spacing-sm;
}
.result-code {
  font-size: @font-size-sm; font-weight: @font-weight-semibold;
  color: @brand-primary;
  background: fade(@brand-primary, 10%);
  padding: 2px @spacing-sm; border-radius: @radius-sm;
}
.result-title { font-size: @font-size-base; font-weight: @font-weight-medium; color: @text-primary; }
.result-match {
  display: flex; align-items: center; gap: @spacing-xs;
  font-size: @font-size-xs; color: @text-secondary;
  margin-bottom: @spacing-sm;
}
.result-excerpt {
  font-size: @font-size-sm; color: @text-primary;
  line-height: 1.6; margin-bottom: @spacing-sm;
  padding-left: @spacing-md;
  border-left: 2px solid @divider-color;
}
.result-source {
  display: flex; align-items: center; gap: @spacing-xs;
  font-size: @font-size-xs; color: @text-tertiary;
}

.history-item { cursor: pointer; &:hover .history-query { color: @brand-primary; } }
.history-query { font-size: @font-size-sm; }

.tree-name { margin-right: @spacing-sm; }
.tree-count {
  font-size: @font-size-xs; color: @text-tertiary;
  background: @divider-color; padding: 0 @spacing-xs; border-radius: @radius-sm;
}
</style>
