<template>
  <div class="page-container">
    <div class="page-header">
      <h2>AI 审标</h2>
      <p>上传招标文件，智能识别风险条款与偏差</p>
    </div>

    <a-tabs v-model:activeKey="activeTab" class="app-tabs">
      <a-tab-pane key="current" tab="当前任务">
        <a-steps :current="currentStep" style="margin-bottom: 32px">
          <a-step v-for="s in steps" :key="s.title" :title="s.title" :description="s.description" />
        </a-steps>

        <a-row :gutter="24">
          <a-col :span="12">
            <a-card title="文档区域" class="bid-card">
              <div class="upload-area" @click="currentStep = 1">
                <CloudUploadOutlined style="font-size: 48px; color: #ccc" />
                <p style="margin-top: 12px; color: #999">点击或拖拽上传招标文件</p>
                <p style="font-size: 12px; color: #ccc">支持 PDF、Word 格式</p>
              </div>
              <div v-if="currentStep >= 1" class="file-info">
                <a-divider />
                <a-list size="small">
                  <a-list-item>
                    <a-list-item-meta title="XX_项目_招标文件.pdf" description="2.4 MB · 上传完成" />
                    <FilePdfOutlined style="fontSize: 24px; color: #ff4d4f" />
                  </a-list-item>
                </a-list>
              </div>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card title="风险分析面板" class="bid-card">
              <template v-if="currentStep < 2">
                <a-empty description="请先上传文档进行识别" />
              </template>
              <template v-else>
                <div v-for="risk in risks" :key="risk.content" class="risk-item">
                  <div class="risk-header">
                    <a-tag :color="risk.level === '高风险' ? 'red' : risk.level === '中风险' ? 'orange' : 'blue'">
                      {{ risk.level }}
                    </a-tag>
                    <span class="risk-source">{{ risk.source }}</span>
                  </div>
                  <p class="risk-content">{{ risk.content }}</p>
                  <a-button type="link" size="small" @click="currentStep = 3">确认</a-button>
                  <a-divider style="margin: 8px 0" />
                </div>
                <a-button type="primary" style="margin-top: 12px" @click="currentStep = 3">生成分析报告</a-button>
              </template>
            </a-card>
          </a-col>
        </a-row>

        <a-row v-if="currentStep >= 3" :gutter="24" style="margin-top: 24px">
          <a-col :span="24">
            <a-card title="追问区" class="bid-card">
              <a-space direction="vertical" style="width: 100%">
                <a-input-search placeholder="对分析结果进行追问..." enter-button="发送" size="large" />
                <div class="qa-item">
                  <a-comment>
                    <template #author><span>我</span></template>
                    <template #content><p>这些高风险条款是否影响投标资格？</p></template>
                  </a-comment>
                  <a-comment>
                    <template #author><span style="color: #00c9b7">AI</span></template>
                    <template #content>
                      <p>第3章2.1节的投标截止时间冲突可能导致投标被拒收，属于实质性条款偏差，建议在投标前向招标方提出澄清申请。</p>
                    </template>
                  </a-comment>
                </div>
              </a-space>
            </a-card>
          </a-col>
        </a-row>
      </a-tab-pane>

      <a-tab-pane key="history" tab="历史记录">
        <a-card class="bid-card">
          <a-table :dataSource="historyData" :columns="historyColumns" :pagination="{ pageSize: 10 }" row-key="id">
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'riskCount'">
                <a-badge :count="record.riskCount" :number-style="{ backgroundColor: record.riskCount > 3 ? '#ff4d4f' : '#00c9b7' }" />
              </template>
              <template v-if="column.key === 'status'">
                <a-tag color="success">{{ record.status }}</a-tag>
              </template>
              <template v-if="column.key === 'action'">
                <a-button type="link" size="small">查看报告</a-button>
                <a-button type="link" size="small">再次分析</a-button>
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
import { CloudUploadOutlined, FilePdfOutlined } from '@ant-design/icons-vue'
import { bidReviewSteps, riskItems, bidReviewHistory } from '@/mock/data'

const activeTab = ref('current')
const currentStep = ref(0)
const steps = bidReviewSteps
const risks = riskItems
const historyData = ref(bidReviewHistory)

const historyColumns = [
  { title: '文档名称', dataIndex: 'document', key: 'document' },
  { title: '分析日期', dataIndex: 'date', key: 'date' },
  { title: '风险数', dataIndex: 'riskCount', key: 'riskCount' },
  { title: '状态', dataIndex: 'status', key: 'status' },
  { title: '操作', key: 'action' },
]
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.bid-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}

.upload-area {
  border: 2px dashed #e5e7eb;
  border-radius: @border-radius;
  padding: 48px;
  text-align: center;
  cursor: pointer;
  transition: all 0.2s;
  &:hover {
    border-color: @accent-color;
    background: rgba(0, 201, 183, 0.02);
  }
}

.risk-item {
  margin-bottom: 8px;
}
.risk-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}
.risk-source {
  font-size: 12px;
  color: @text-secondary;
}
.risk-content {
  font-size: 14px;
  color: @text-primary;
  margin: 4px 0;
  line-height: 1.5;
}

.qa-item {
  margin-top: 16px;
}
</style>
