namespace DredgeAI.BidCompare;

/// <summary>
/// v2 消费要求的样例数据：ValidGraphJsonl / ValidMetaJson 为 AnGIneer 原始产物
/// （doc_blocks_graph.jsonl / doc_blocks_graph_meta.json），Valid 为 AnGineerIrMapper
/// 映射后的内部适配 IR（bbox 0~1 归一化、blockId=block_uid）。
/// </summary>
public static class SampleIr
{
    /// <summary>AnGIneer doc_blocks_graph.jsonl 样例（3 行，块级字段见 v2 §1）。</summary>
    public const string ValidGraphJsonl = """
    {"block_uid":"b0001","block_type":"title","page_idx":0,"plain_text":"第三章 技术方案","derived_level":1,"bbox":[0.0672,0.0594,0.9244,0.095],"source":"text","confidence":1.0}
    {"block_uid":"b0002","block_type":"table","page_idx":1,"plain_text":"报价表","derived_level":0,"bbox":[0.0672,0.1188,0.9244,0.2969],"page_bboxes":[{"page_idx":1,"bbox":[0.0672,0.1188,0.9244,0.2969]},{"page_idx":2,"bbox":[0.05,0.1,0.95,0.3]}],"merged_from":["b0002-p2"],"table_html":"<table><tr><td>总价</td></tr></table>","image_path":"images/t1.jpg","source":"table","confidence":1.0}
    {"block_uid":"b0003","block_type":"paragraph","page_idx":1,"plain_text":"盖章扫描文字","derived_level":0,"bbox":[0.0672,0.3563,0.9244,0.4157],"source":"ocr","confidence":0.3}
    """;

    /// <summary>AnGIneer doc_blocks_graph_meta.json 样例（outlines / docMeta / pages，v2 §1）。</summary>
    public const string ValidMetaJson = """
    {
      "build_id": "demo-build",
      "outlines": [
        { "title": "第三章 技术方案", "level": 1, "block_uid": "b0001", "children": [] }
      ],
      "docMeta": {
        "fileName": "标书A.pdf", "pageCount": 2,
        "author": null, "creatorTool": "Microsoft Word",
        "createdAt": null, "modifiedAt": null
      },
      "pages": [
        { "page_idx": 0, "width": 1190, "height": 1684 },
        { "page_idx": 1, "width": 1190, "height": 1684 }
      ]
    }
    """;

    /// <summary>映射后的内部适配 IR（docId 由调用方传入；页面 1190×1684 为真实尺寸，bbox 为 0~1 归一化值）。</summary>
    public const string Valid = """
    {
      "schemaVersion": "2.0",
      "docId": "doc-a",
      "meta": {
        "fileName": "标书A.pdf",
        "pageCount": 2,
        "author": null,
        "creatorTool": "Microsoft Word",
        "createdAt": null,
        "modifiedAt": null
      },
      "pages": [
        { "pageIdx": 0, "width": 1190, "height": 1684 },
        { "pageIdx": 1, "width": 1190, "height": 1684 }
      ],
      "outline": [
        { "title": "第三章 技术方案", "level": 1, "blockId": "b0001", "children": [] }
      ],
      "blocks": [
        {
          "blockId": "b0001", "pageIdx": 0, "bbox": [0.0672, 0.0594, 0.9244, 0.095],
          "type": "title", "text": "第三章 技术方案", "textLevel": 1,
          "source": "native", "confidence": 1.0
        },
        {
          "blockId": "b0002", "pageIdx": 1, "bbox": [0.0672, 0.1188, 0.9244, 0.2969],
          "type": "table", "text": "报价表", "textLevel": 0,
          "source": "native", "confidence": 1.0,
          "pageBBoxes": [
            { "pageIdx": 1, "bbox": [0.0672, 0.1188, 0.9244, 0.2969] },
            { "pageIdx": 2, "bbox": [0.05, 0.1, 0.95, 0.3] }
          ],
          "mergedFrom": ["b0002-p2"],
          "table": { "html": "<table><tr><td>总价</td></tr></table>", "imgPath": "images/t1.jpg" }
        },
        {
          "blockId": "b0003", "pageIdx": 1, "bbox": [0.0672, 0.3563, 0.9244, 0.4157],
          "type": "para", "text": "盖章扫描文字", "textLevel": 0,
          "source": "ocr", "confidence": 0.3
        }
      ]
    }
    """;

    public const string ValidContentMd = "# 第三章 技术方案\n\n本方案……\n";
}
