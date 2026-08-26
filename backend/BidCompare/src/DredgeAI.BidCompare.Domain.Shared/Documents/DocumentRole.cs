namespace DredgeAI.BidCompare.Documents;

// spec §6: 上传文档（标书/招标文件，区分 role）
public enum DocumentRole : byte
{
    Bid = 0,
    Tender = 1
}
