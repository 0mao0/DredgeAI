# AI 投标 · 比标模块设计文档

- 日期：2026-07-29
- 状态：待评审
- 范围：**仅设计文档**，不含实现代码。前端原型与后端算法服务的开发均在本文档评审通过后另行排期。

## 1. 背景与目标

比标是「AI 投标」的四个子模块之一（读标/写标/清标为占位）。用户上传多份标书文件（A~E，2~5 份，PDF+Word，单份通常 100~500 页），系统自动完成对比分析，形成对比报告并支持导出 PDF/Word。

模块承担**双目标**：

1. **围串标嫌疑检测**（招标方/评审视角）：多份标书两两查重，发现异常雷同、同源痕迹、报价规律等问题；
2. **投标方案比选**（采购/评标视角）：对比关键指标、报价、技术方案，辅助评标决策。

此外支持**强制性条款对比**：以条款为基准校验每份标书是否实质响应。条款来源两者结合——从用户上传的招标文件中 AI 自动提取 + 用户手动维护的个人条款库。

## 2. 非目标（YAGNI）

- 不做写标、清标模块的功能（各自独立设计）；
- 不做公式语义等价判断（换符号重写公式的识别属于 AI 语义层的远期能力，不在解析契约中约束）；
- 不要求解析提供方暴露其内部流水线细节（MinerU 版本、后端、OCR 策略均为其内部决策）；
- 不在浏览器端生成 PDF/Word 导出文件。

## 3. 总体架构（方案 A：分层流水线）

核心原则：**查重出证据用确定性算法（可复现、可解释、可定位），AI 只做语义判断与自然语言表达**。

```
┌─────────────┐   ┌──────────────┐   ┌─────────────────┐   ┌────────────┐
│  上传与任务   │ → │  解析层       │ → │  分析层          │ → │  报告层     │
│  管理        │   │  (MinerU)    │   │  算法 + AI       │   │  生成/导出  │
└─────────────┘   └──────────────┘   └─────────────────┘   └────────────┘
```

### 3.1 后端模块划分

| 模块 | 职责 | 说明 |
|------|------|------|
| `compare-task` 任务服务 | 接收上传、创建比标任务、驱动状态机（`parsing`→`parsed`→（待条款确认）→`comparing`→`analyzing`→`done`，异常态 `failed` / `partial`）、任务持久化 | 挂在 ABP 主服务内，走标准 PagedResultDto 契约 |
| `doc-parse` 解析服务 | 调用外部 MinerU 解析流水线，接收并校验 IR 产物，落库存储 | 独立服务，可水平扩展 |
| `compare-algo` 算法服务 | 两两查重（n-gram shingling + MinHash 粗筛 → 块级精确对齐）、雷同簇聚类（≥3 份共同雷同单独标记）、报价规律分析（等差/尾数/贴近度）、元数据与排版痕迹比对（同作者/同模板/相同错别字） | 纯确定性，输入 IR 输出结构化证据项 |
| `compare-ai` 语义服务 | 强制性条款提取（从招标文件 doc.md）、逐份标书响应判定、关键指标/技术方案要点抽取、报告文案生成 | LLM 调用集中在此，带缓存与重试 |
| `report` 报告服务 | 汇总证据项 + AI 结论 → 结构化报告 JSON；按模板导出 Word / PDF | 报告 JSON 与导出文件分离，前端展示只读 JSON |

### 3.2 关键设计决策

- **证据项（Evidence）是全系统的核心数据结构**。每条问题 = 类型 + 严重度 + 涉及文档 + 涉及块 bbox + 量化指标 + 说明文字。算法、AI、前端高亮、报告导出四方消费同一份证据，保证「报告里说的」与「页面上标的位置」严格一致。
- **IR 与证据项均持久化**。重新生成报告、追加标书增量对比时不重跑解析。
- **算法服务与 AI 服务解耦**：算法跑完即可先展示查重结果，AI 结论异步补齐，前端分块加载。
- **强制性条款必须用户确认后锁定**：AI 提取结果为草案，用户增删改并合并条款库后形成任务内快照，AI 提取不当黑盒。
- **Evidence 带 `aiGenerated` 标记**：算法证据与 AI 结论在 UI 上可区分，误判可追溯责任方。

## 4. 文档解析产物交付契约

比标系统不约束提供方内部如何使用 MinerU，只定义交付产物与格式。

### 4.1 交付物清单（每份文档一个目录）

```
{docId}/
├── origin.{pdf|docx}      # 原始文件
├── ir.json                # ★ 核心产物：结构化文档
├── doc.md                 # 阅读流 Markdown，供 AI 语义层使用
└── images/                # 文中图片/表格/印章/公式截图，ir.json 中用相对路径引用
```

### 4.2 ir.json 格式

```jsonc
{
  "schemaVersion": "1.0",
  "docId": "string",
  "meta": {
    "fileName": "string",
    "pageCount": 122,
    // 提取不到给 null，不省略字段（元数据比对要用）
    "author": null, "creatorTool": "Microsoft Word",
    "createdAt": null, "modifiedAt": null
  },
  "pages": [
    { "pageIdx": 0, "width": 1190, "height": 1684 }  // 页面实际像素尺寸
  ],
  "outline": [
    { "title": "第三章 技术方案", "level": 1, "blockId": "b0042", "children": [] }
  ],
  "blocks": [                                // 按阅读顺序排列
    {
      "blockId": "b0043",                    // 文档内唯一、稳定
      "pageIdx": 12,
      "bbox": [0, 0, 0, 0],                  // 页面实际像素坐标 [x0,y0,x1,y1]，左上角原点
      "type": "title|para|table|list|image|equation|seal|header|footer",
      "text": "块的纯文本内容",
      "textLevel": 2,                        // 标题层级，非标题为 0
      "source": "native|ocr",                // 原生文本 or OCR 识别
      "confidence": 0.97,                    // 0~1；native 文本给 1.0
      // 仅 table 类型：
      "table": {
        "html": "<table><tr><td rowspan=\"2\">...</td></tr></table>",  // 必需，保留合并单元格，纯净结构（仅 table/tr/td/th，无样式无 class）
        "imgPath": "images/xxx.jpg"                                     // 必需，整表截图（高亮用）
      },
      // 仅 image / seal / equation 类型：
      "imgPath": "images/yyy.jpg"
    }
  ]
}
```

### 4.3 硬性要求

1. bbox 必须是页面实际像素坐标（配合 `pages[].width/height`），**不接受 0-1000 或 0-1 归一化坐标**，换算在提供方流水线内完成；
2. 每个块必须带 `source` 与 `confidence`；低置信（<0.5）块照常交付，不得静默丢弃，由比标系统降权处理；
3. blocks 按阅读顺序排列；`blockId` 在同一文档重跑之间保持稳定（或明确告知不稳定，改用 bbox 匹配）；
4. 表格必须同时给 `html`（忠实还原合并单元格）与整表截图；不要求额外提供拍平的 rows 数组（避免双结构不一致）；
5. 印章单独标 `seal` 类型，不混入普通 image；
6. 行间公式必须独立成块且 `text` 给 LaTeX 源码（参与查重），不允许只给截图；行内公式不强制拆块，允许以 `$...$` 内联在段落文本中；
7. 文本按语义块（段落级）聚合，不按物理行切碎——查重对齐以段落为单位。

### 4.4 质量验收标准（提供方自测）

- 原生 PDF 文本覆盖率 ≥ 99%（无漏块）；
- 标题层级识别准确率抽测（每文档抽 20 个标题）；
- 扫描页 OCR 后全文可读，无大面积乱码。

### 4.5 OCR 置信度的用途

标书中盖章页、资质证书多为扫描件，OCR 噪声会污染查重：原生文本中「错得一样」是围标强证据，OCR 文本中「错得一样」可能只是识别器犯了同样的错。因此：

- 算法层对 `source=ocr` 且低置信的块降权或单独标注；
- UI 上此类证据提示「来自扫描件识别，准确率可能受影响」；
- 概览区对 OCR 低置信页占比过高的文档给出醒目提示。

## 5. 数据流

```
1. 上传    用户拖入 2~5 份标书 (+可选招标文件) → 文件落对象存储，创建任务(status=parsing)
2. 解析    doc-parse 接收各文档 IR → 校验、落库，逐份回报进度 → 全部完成(status=parsed)
3. 条款    若有招标文件：AI 提取强制性条款草案 → 用户确认/补充/从条款库勾选 → 锁定条款快照
4. 分析    compare-algo 全量两两比对 → 证据项落库(status=comparing)
           完成后 compare-ai 异步做响应判定/指标抽取/文案 → 逐步补齐(status=analyzing → done)
5. 展示    前端轮询任务状态；查重证据先到先展示，AI 结论到达后合并
6. 导出    用户点导出 → report 服务按报告 JSON 渲染 Word/PDF → 返回下载地址
```

## 6. API 契约（ABP 风格，camelCase，列表走 PagedResultDto）

```
POST   /api/compare/tasks                       创建任务（含条款清单快照）
GET    /api/compare/tasks/{id}                  任务详情 + 状态机状态 + 各阶段进度
GET    /api/compare/tasks                       任务列表（分页）
POST   /api/compare/tasks/{id}/documents        上传文档（标书/招标文件，区分 role）
GET    /api/compare/tasks/{id}/ir/{docId}       某文档的 IR（前端对比视图画 bbox 用）
GET    /api/compare/tasks/{id}/evidences        证据项列表（按类型/严重度/文档对过滤）
GET    /api/compare/tasks/{id}/report           结构化报告 JSON
POST   /api/compare/tasks/{id}/export           生成导出文件 { format: 'pdf'|'word' } → 异步 → 下载 URL
GET    /api/compare/tasks/{id}/matrix           两两相似度矩阵（N×N，热力图用）

GET    /api/compare/clause-templates            个人条款库（分页）
POST   /api/compare/clause-templates            新增条款模板
POST   /api/compare/tasks/{id}/clauses/extract  触发从招标文件提取条款草案
PUT    /api/compare/tasks/{id}/clauses          确认后的条款清单（锁定快照）
```

### 6.1 核心数据模型

```ts
CompareTask   { id, name, status, docIds[], tenderDocId?, clauseSnapshot[], progress, createdAt }
Clause        { clauseId, source: 'extracted'|'manual'|'template', text, mandatory, category }
Evidence      { id, taskId, type: 'similarity'|'pricing'|'metadata'|'clause'|'indicator',
                severity: 'high'|'mid'|'low', docIds[], locations: { docId, blockIds[] }[],
                metrics: { similarity? }, title, description, aiGenerated: boolean }
CompareReport { taskId, summary, matrix, sections: [围标风险 | 条款响应 | 指标比选], generatedAt }
```

说明：IR 的块结构（Block）以第 4 节的交付契约为准，后端落库时可原样存储。

### 6.2 契约决策

- **条款清单做成任务内快照**：任务创建后条款库再被改动不影响历史任务，报告可复现；
- **导出异步化**：大报告生成慢，`export` 返回任务句柄，前端轮询获取下载链接。

## 7. 前端设计（user-web `/ai-bid/compare`，替换现有占位页）

### 7.1 页面流程

```
任务列表 → 创建任务(上传) → 条款确认 → 分析进度 → 结果工作台 → 报告导出
```

1. **任务列表页**：标准管理列表（AGENTS.md 表格规范），列：任务名、标书份数、状态、高风险数、创建时间、操作（查看/导出/删除）；
2. **创建任务**：`a-upload-dragger` 上传 2~5 份标书 + 可选招标文件（交互参考 `VoiceRegisterUploadTab`），填任务名；
3. **条款确认页**（有招标文件时）：左侧 AI 提取的条款草案（勾选/编辑/删除），右侧从条款库追加；用户点「确认锁定」才进入分析；
4. **分析进度页**：复用读标交互语言——左侧 DocViewer 预览 + 分步进度（解析→查重→条款校验→AI 分析），右侧实时出现已产出证据；查重完成即可进入结果页，AI 部分后台补齐；
5. **结果工作台**（核心页面）：

| 区块 | 内容 |
|------|------|
| 概览 | MetricCard 行（标书份数/高风险/中风险/条款不响应数）+ N×N 相似度热力图（ECharts heatmap，点单元格跳到对应文档对的证据） |
| 证据清单 | 表格：类型（雷同/报价/元数据/条款）筛选 + 严重度 tag + 涉及文档 + 摘要；点行进入左右对比 |
| 条款响应矩阵 | 行=强制性条款，列=标书 A~E，单元格=响应/部分响应/未响应 tag；点单元格看 AI 判定理由 + 原文定位 |
| 指标比选表 | 行=关键指标（报价、工期、资质等），列=标书，AI 抽取要点摘要 |

6. **左右对比视图**：双栏 PDF 渲染（引入 pdf.js，替换 DocViewer 现有假分页），bbox 覆盖层高亮——点击证据，两侧各自跳到对应页并把涉及块画框，雷同块用同色配对；支持「逐块对齐模式」（左右块锁定滚动）。

### 7.2 前端状态与工程约束

- 所有 API 调用集中在 `src/api/modules/compare.ts`，组件不直接 import request（AGENTS.md 2.0 清单）；
- `index.vue` 唯一持有业务状态，子组件 props down / events up；
- 每个数据域覆盖 loading / empty / error 三态；
- 图表遵循 `docs/chart-conventions.md`；主题色一律引用 CSS/Less 变量。

## 8. 报告结构（Word/PDF，后端模板渲染）

```
1. 封面：任务名、文档清单、生成时间、总体结论一句话
2. 摘要：高/中/低风险计数 + Top 5 最重要发现
3. 相似度矩阵：热力图 + 两两相似度数值表
4. 围标风险详情：每条证据一节——结论、涉及文档、量化指标、
   原文截图(带高亮框)、页码引用；AI 生成的判断标注「AI 分析」
5. 强制性条款响应：响应矩阵总表 + 未响应项逐条说明(含原文定位)
6. 关键指标比选：指标对比表 + AI 综合评述
7. 附录：条款清单快照、解析质量说明(OCR 低置信页清单)、免责声明
```

一致性原则：**报告里每条证据都能在结果工作台找到同一证据 ID 对应的交互视图**，截图即从 bbox 渲染。

## 9. 错误处理与降级

- 单份解析失败 → 任务降级为「部分完成」，其余文档照常对比，失败文档标注原因，支持单独重传重解析；
- MinerU 解析服务不可用 → 任务挂起 + 明确提示，**不静默降级到弱解析**（弱解析的查重结果会误导，宁可不做）；
- AI 服务失败/超时 → 算法证据照常展示，AI 区块显示「AI 分析暂不可用」可重试，不阻塞整体；
- OCR 低置信页占比过高 → 概览区醒目提示「该文档为扫描件，查重结果可能偏差」；
- 导出失败可重试。

## 10. 测试策略（面向后续实现）

- **算法层**：构造样例文档对（全文雷同/部分雷同/同模板不同内容/同错别字/OCR 噪声对），验证相似度分值排序与证据定位准确性；
- **规范化校验层**：对 IR 做 schema 校验（必填字段、坐标范围、阅读顺序、表格 HTML 纯净度），不合格即拒收并报具体原因；
- **契约测试**：API 响应符合 ABP 格式标准（`docs/ABP接口响应格式标准.md`）；
- **前端**：沿用项目 typecheck + 手动走查；关键交互（条款确认锁定、证据跳转 bbox 高亮）纳入验收用例。

## 11. 待决事项

1. MinerU 解析服务的部署形态与调用方式（提供方给 API 还是消息队列），影响 doc-parse 服务的对接细节；
2. LLM 供应商与模型选型（影响 compare-ai 的成本与上下文策略）;
3. 对象存储选型（原始文件 + IR + 导出文件的存放）；
4. 报告 Word 模板的具体样式（商务风格需设计稿）。
