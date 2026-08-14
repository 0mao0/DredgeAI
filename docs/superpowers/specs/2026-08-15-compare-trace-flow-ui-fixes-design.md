# 比标溯源过程流 UI 重构与 IR 修复设计

> 日期：2026-08-15
> 范围：`user-web` 比标模块右侧面板、PDF 溯源交互、顶部工具栏；`DredgeAI.BidCompare` IR 元数据解析；`user-web` 前端 API 错误静默。

## 1. 背景与目标

用户在海港 1 / 海港 2 对比中发现 7 个问题：

1. 对比过程中大量弹出“对不起，在处理您的请求期间产生了一个服务器内部错误！！”。
2. 右侧内容应按过程从上往下依次出结果，完成一类折叠一类；“文档解析”应排第一，完成折叠后标题写摘要。
3. 既然按溯源过程展示，不应再有最终结果 Tab，总体“结果摘要”也应移除。
4. 串标条目应整卡点击即溯源，不应只点“溯源”文字；两个 PDF 应能对应跳转、定位、高亮。
5. 最终页的“过程记录”不是过程，应删除。
6. 顶部“已完成”标签应放在项目名（标题）前面。
7. “收起面板”应收起左侧 PDF 面板，而不是收起右侧内容。

## 2. 已确认的设计决策

### 2.1 右侧改为统一“溯源过程流”面板

采用方案一（合并 `ProcessPanel` / `ResultPanel`）加方案 A/B 的组合：

- 删除“处理进度”卡，右侧阶段顺序固定为：
  1. `文档解析`
  2. `条款确认`（仅上传招标文件且尚未锁定条款快照时出现）
  3. `两两对比`
  4. `AI 分析`
- 每个阶段进行中保持展开；阶段完成后自动折叠成一行摘要。
- 折叠摘要格式：
  - `文档解析完成 · N 份 · X 页`（有失败时追加 `· N 份失败`，展开可重试）
  - `条款确认完成 · N 条`
  - `两两对比完成 · N/N 对 · 最高相似度 X%`
  - `AI 分析完成 · 共 N 条发现`
- 证据挂在产生它的阶段下：
  - 雷同 / 报价 / 元数据证据 + 相似度热力图 → `两两对比` 折叠区。
  - 条款 / 指标证据 + 条款响应矩阵 + 指标比选表 → `AI 分析` 折叠区。
- 删除“结果摘要”卡、类型筛选（全部/串标/条款/指标）、“过程记录”和最终结果 Tab。
- 任务 `failed` 时保留现有 `FailurePanel` 失败重试逻辑。

### 2.2 证据卡片与 PDF 溯源

- `EvidenceCard` 整卡可点击，移除单独的“溯源”按钮；点击即触发溯源。
- 溯源动作自动展开双栏 PDF（若已收起左面板则先恢复），左右分别设为证据对应文档、页码与高亮。
- 有 bbox 的证据按具体位置高亮；元数据证据（无 blockIds）跳对应文档第 1 页并整页高亮。

### 2.3 顶部工具栏

- 状态标签（如“已完成”）移动到项目名输入框左侧。
- “收起”按钮改为收起左侧 PDF 面板；收起后右侧过程流占满整宽，再次点击恢复。

### 2.4 IR 500 与高亮失效修复

- 根因：PDF 元数据 `createdAt` 为 PDF 日期格式（如 `D:20251229164720+08'00'`），后端 `DocumentIrDto` 按 `DateTime?` 反序列化，导致 `GET /ir/{docId}` 每次 500。
- 修复：`IrMetaDto.CreatedAt` / `ModifiedAt` 改为字符串透传，兼容 AnGIneer 原始元数据格式。
- 前端 `getIr` 请求统一静默（`X-Silent-Request`），IR 属增强型数据，失败不再弹全局错误；证据列表与兜底跳转不受影响。
- 旧任务无需重新解析，已存储的 IR JSON 修复后即可正常读取。

## 3. 涉及文件

### 前端

- `user-web/src/views/ai-bid/compare/index.vue`
  - 移除 `panel === 'result'` 分支与 `ResultPanel` 引用。
  - 右侧始终渲染统一溯源过程流；终端状态时不再切换结果视图。
  - 向过程流传入 `overview`，供热力图使用。
  - `workspaceCollapsed` 语义改为“左侧 PDF 面板收起/展开”，样式与 tooltip 同步调整。
  - 状态标签移到项目名输入框之前。
- `user-web/src/views/ai-bid/compare/components/ProcessPanel.vue`
  - 原地重构为统一溯源过程流（沿用文件名，避免大范围改名）：阶段顺序、折叠摘要、阶段内证据与全局视图。
  - 保留现有 retry / reparse / clause 相关事件。
- `user-web/src/views/ai-bid/compare/components/ResultPanel.vue`
  - 从页面引用中移除；确认无其他引用后删除文件。
- `user-web/src/views/ai-bid/compare/components/EvidenceCard.vue`
  - 整卡可点击并触发 `trace`，移除“溯源”按钮，增加 hover 提示。
- `user-web/src/views/ai-bid/compare/components/PdfWorkspace.vue`
  - `locate` 对无 refs 证据提供第 1 页整页高亮兜底。
  - 配合 `workspaceCollapsed` 新语义：收起时隐藏左侧，右侧占满。
- `user-web/src/api/modules/compare.ts`
  - `getIr` 改为始终静默请求。

### 后端

- `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application.Contracts/Ir/DocumentIrDtos.cs`
  - `IrMetaDto.CreatedAt` / `ModifiedAt` 从 `DateTime?` 改为 `string?`。

## 4. 状态与错误处理

- 阶段进行中：展开显示进度/列表；阶段完成：折叠显示摘要，可展开查看明细。
- 解析失败：文档解析阶段摘要包含失败数，展开后可单独/全部重新解析。
- 比对失败：两两对比阶段摘要包含失败对数，展开后可重试失败对。
- IR 获取失败：静默降级，证据仍展示，点击溯源使用第 1 页兜底。
- `partial`：已完成阶段照常折叠，失败文档/比对对内联重试。
- `failed`：保留现有失败面板与重试入口。

## 5. 验证方式

1. `pnpm run typecheck` 通过。
2. 打开海港 1 / 海港 2 对比任务：
   - 无“服务器内部错误”弹窗。
   - 右侧第一项为“文档解析”，完成后折叠为摘要，再依次出现“两两对比”“AI 分析”。
   - 无最终结果 Tab、无“结果摘要”、无“过程记录”。
   - 点击任意串标证据卡片，双栏 PDF 自动展开并跳转/高亮。
   - 顶部状态标签在项目名前；收起按钮收起左侧 PDF 面板。
3. 回归：新建任务、上传、解析、条款确认（如适用）、比对、AI 分析、导出。
