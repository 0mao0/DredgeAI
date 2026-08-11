# DredgeAI 对接 AnGIneer 解析产物：消费与适配要求

> 日期：2026-08-07（v2，取代 v1 的 ir.json/薄转化层方案）
> 读者：DredgeAI 比标系统（frontend / abp-backend / algo-service）
> 关联：AnGIneer 侧 TODO 见 `D:/AI/AnGIneer/docs/superpowers/plans/2026-08-07-jsonl-meta-enrich-todo.md`

## 决策

1. DredgeAI 直接消费 AnGIneer 的 `doc_blocks_graph.jsonl` + `doc_blocks_graph_meta.json` +
   `content.md` + `images/`，不再要求 ir.json，也不做薄转化层。
2. PDF 高亮复用 AnGIneer `PDF_Viewer.vue` 的归一化 bbox 逻辑，不要求像素坐标。
3. 本文件是 DredgeAI 侧的适配要求；AnGIneer 侧字段补齐见上述 TODO 文档。

## 一、数据源

每份文档的 `parsed/` 目录：

- `doc_blocks_graph.jsonl`：结构化块（block_uid / block_type / page_idx / plain_text / derived_level / bbox / table_html / math_content / image_path / source / confidence）
- `doc_blocks_graph_meta.json`：edges / stats / build_id，以及 `outlines` / `docMeta` / `pages`
- `content.md`：阅读流 Markdown（供 AI 语义层）
- `images/`：表格/公式/图片截图（`image_path` 为相对路径）
- `mineru_raw/middle.json`：真实页面尺寸（`pdf_info[].page_size`）与像素 bbox（如需）

## 二、字段映射

| DredgeAI 概念 | 取值 |
| --- | --- |
| `blockId` | 直接用 `block_uid`（如 `doc-406e43e8:3:1`，唯一稳定） |
| `pageIdx` | `page_idx`（0-based） |
| `bbox` | graph 节点 `bbox`（0~1 归一化），由 PDF_Viewer 还原，不做像素换算 |
| `pages[].width/height` | meta `pages`（真实尺寸，AnGIneer 新增） |
| `type` | 按第三节映射表转换 |
| `text` | `plain_text`；公式块用 `math_content` / `formula_body`（LaTeX） |
| `textLevel` | 标题块 = `derived_level`；非标题块固定 0 |
| `source` / `confidence` | 见第四节；AnGIneer 补齐前允许 null |
| `table.html` / `imgPath` | `table_html` / `image_path` |
| `imgPath`（image/seal/equation） | `image_path` |
| `outline` | 从 meta `outlines` 读取（AnGIneer 新增）；每条含 `outline_id/title/level/page_idx/anchor_block_id/parent_outline_id/printed_page_label` |
| `outline 页码` | 跳转用 `page_idx`（0-based 实际页次）+ `anchor_block_id`（block_uid 精确定位）；`printed_page_label` 为纸面页码（如“14”），仅展示用，可 null |
| `meta.*` | 从 meta `docMeta` 读取（AnGIneer 新增） |

## 三、类型映射表

| AnGIneer `block_type` | DredgeAI 类型 |
| --- | --- |
| `title` | `title` |
| `paragraph` | `para` |
| `list` | `list` |
| `table` | `table` |
| `equation_interline` | `equation` |
| `image` / `figure` | `image` |
| `chart` | `image`（2026-08-08 实测补充：AnGIneer 产物中真实存在，有截图、文本为空） |
| `page_header` | `header` |
| `page_footer` | `footer` |
| `page_number` | 归入 `header`/`footer` 或忽略 |

## 四、source / confidence 约定

- `source=text` → `confidence=1.0`（原生文本；AnGIneer 实现以 `text` 表达 native 语义，2026-08-08 实测源码确认，本节原措辞 `native` 以实测为准）；
- `source=ocr` → `confidence=对应识别分数`；
- 表格/公式/图片 → `confidence=1.0` 或对应识别置信；
- 在 AnGIneer 新增字段之前，DredgeAI 允许 `source/confidence` 为 null，OCR 降权与低置信提示降级关闭。

## 五、DredgeAI 侧配合点

1. 复用 AnGIneer `PDF_Viewer.vue` 的归一化 bbox 高亮逻辑，不再要求像素 bbox。
2. `blockId` 直接用 `block_uid`，不自造 id。
3. 类型枚举按第三节映射。
4. `textLevel` 仅标题块有层级，非标题一律 0。
5. `source/confidence` 接受 nullable；AnGIneer 补齐后启用 OCR 降权。
6. outline 从 meta `outlines` 读取；AnGIneer 给扁平结构（`parent_outline_id`）时转嵌套 `children`，或直接给嵌套。跳转以 `page_idx`/`anchor_block_id` 为准，`printed_page_label` 仅用于展示。
7. `docMeta` 字段可 null 但字段必须存在。

## 六、与现有 spec §4 的关系

- 原 `ir.json` 交付契约不再作为跨系统交付物；DredgeAI 按本文件字段映射消费 AnGIneer 产物。
- `algo-service` 的 pydantic IR schema、`abp-backend` 的 IrValidator、`frontend` 的 IR 类型需要按本映射改造或做内部适配类型。
