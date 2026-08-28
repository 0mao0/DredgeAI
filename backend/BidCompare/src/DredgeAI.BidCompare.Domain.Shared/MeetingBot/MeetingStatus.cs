namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>AI 晨会状态机：录入 → 晨会稿 → 点名 → 进行中 → 完成。</summary>
public enum MeetingStatus : byte
{
    /// <summary>会前录入完成，草稿状态。</summary>
    Draft = 0,

    /// <summary>晨会稿已生成/确认。</summary>
    Prepared = 1,

    /// <summary>拍照点名中。</summary>
    Rollcall = 2,

    /// <summary>会议进行中（语音问答/录音）。</summary>
    Ongoing = 3,

    /// <summary>已完成，报告可查。</summary>
    Completed = 4
}

/// <summary>点名状态。</summary>
public enum AttendanceStatus : byte
{
    Present = 0,
    Absent = 1,
    Late = 2,
    Unrecognized = 3
}

/// <summary>问答意图分类。</summary>
public enum QaIntentType : byte
{
    Knowledge = 0,
    Chitchat = 1,
    Meeting = 2
}

/// <summary>工人人脸状态。</summary>
public enum FaceStatus : byte
{
    Pending = 0,
    Enrolled = 1
}
