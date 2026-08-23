using System;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.BackgroundJobs;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace DredgeAI.BidCompare.MeetingBot;

public class MeetingRecordAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly IMeetingRecordAppService _appService;
    private readonly FakeMeetingBotClient _bot;
    private readonly FakeLlmGateway _llm;
    private readonly RecordingBackgroundJobManager _jobManager;

    public MeetingRecordAppServiceTests()
    {
        _appService = GetRequiredService<IMeetingRecordAppService>();
        _bot = (FakeMeetingBotClient)GetRequiredService<IMeetingBotClient>();
        _llm = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        _jobManager = (RecordingBackgroundJobManager)GetRequiredService<IBackgroundJobManager>();
    }

    [Fact]
    public async Task Create_Should_Return_Draft_With_PreInfo()
    {
        var created = await _appService.CreateAsync(new PreInfoInput
        {
            Date = new DateTime(2026, 8, 23),
            Weather = "晴",
            Tasks = "基坑支护施工",
            RiskPoints = "临边坠落"
        });

        created.Id.ShouldNotBe(Guid.Empty);
        created.Status.ShouldBe(MeetingStatus.Draft);
        created.PreInfoJson.ShouldContain("基坑支护施工");
        created.SpeechDraft.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateSpeech_Should_Include_PreInfo_And_Knowledge_Evidence()
    {
        var meeting = await _appService.CreateAsync(new PreInfoInput
        {
            Date = new DateTime(2026, 8, 23),
            Tasks = "临边防护检查",
            RiskPoints = "高处坠落"
        });
        _llm.QueueResponse("各位工友，今天的主要任务是临边防护检查……（来自知识库）");

        var draft = await _appService.GenerateSpeechAsync(meeting.Id);

        draft.Content.ShouldContain("临边防护检查");
        draft.Status.ShouldBe("generated");

        var speech = await _appService.GetSpeechAsync(meeting.Id);
        speech.ShouldNotBeNull();
        speech!.Content.ShouldBe(draft.Content);

        // LLM user prompt 中应带前置信息与知识库证据
        _llm.Requests.ShouldContain(r => r.User.Contains("临边防护检查") && r.User.Contains("<evidence>"));
    }

    [Fact]
    public async Task GenerateSpeech_Should_Degrade_When_Retrieval_Empty()
    {
        var meeting = await _appService.CreateAsync(new PreInfoInput { Tasks = "常规作业" });
        _llm.QueueResponse("无证据生成内容");

        var draft = await _appService.GenerateSpeechAsync(meeting.Id);

        draft.Content.ShouldBe("无证据生成内容");
    }

    [Fact]
    public async Task Recognize_Should_Dedupe_And_Filter_By_Threshold()
    {
        var meeting = await _appService.CreateAsync(new PreInfoInput());
        await _appService.StartAsync(meeting.Id);
        _bot.RecognizedFaces =
        [
            new FaceMatchDto { WorkerId = Guid.NewGuid().ToString(), Name = "张三", Confidence = 0.95 },
            new FaceMatchDto { WorkerId = Guid.NewGuid().ToString(), Name = "李四", Confidence = 0.5 }, // 低于阈值
            new FaceMatchDto { WorkerId = null, Name = null, Confidence = 0.7 } // 未识别
        ];

        var first = await _appService.RecognizeAttendanceAsync(meeting.Id, new byte[] { 1 });
        first.Count(a => a.Status == AttendanceStatus.Present).ShouldBe(1);
        first.Count(a => a.Status == AttendanceStatus.Unrecognized).ShouldBe(2);

        // 第二次：张三去重，未识别新增一条
        var second = await _appService.RecognizeAttendanceAsync(meeting.Id, new byte[] { 1 });
        second.Count(a => a.Name == "张三").ShouldBe(1);
        second.Count(a => a.Status == AttendanceStatus.Unrecognized).ShouldBe(4);

        var fetched = await _appService.GetAsync(meeting.Id);
        fetched.Status.ShouldBe(MeetingStatus.Ongoing);
    }

    [Fact]
    public async Task AskQa_Should_Classify_Knowledge_And_Chitchat()
    {
        var anGineer = GetRequiredService<IAnGineerClient>();
        anGineer.ShouldBeOfType<FakeAnGineerClient>();
        var direct = await anGineer.SearchAsync("规范");
        direct.Count.ShouldBeGreaterThan(0);

        var meeting = await _appService.CreateAsync(new PreInfoInput());
        _llm.QueueResponse("按规范要求，临边作业必须系挂安全带。");
        _llm.QueueResponse("你好呀，有什么可以帮你的？");

        var knowledge = await _appService.AskQaAsync(meeting.Id, "高处作业的规范要求是什么");
        knowledge.IntentType.ShouldBe(QaIntentType.Knowledge);
        knowledge.Sources.Count.ShouldBeGreaterThan(0);

        var chitchat = await _appService.AskQaAsync(meeting.Id, "你好");
        chitchat.IntentType.ShouldBe(QaIntentType.Chitchat);
        chitchat.Sources.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetQaAudio_Should_Return_Tts_Wav_For_Answer()
    {
        var meeting = await _appService.CreateAsync(new PreInfoInput());
        _llm.QueueResponse("按规范要求，临边作业必须系挂安全带。");
        var qa = await _appService.AskQaAsync(meeting.Id, "高处作业的规范要求是什么");

        var audio = await _appService.GetQaAudioAsync(qa.Id);

        audio.Length.ShouldBeGreaterThan(0);
        audio[0].ShouldBe((byte)'R');
        audio[1].ShouldBe((byte)'I');
        _bot.TtsTexts.ShouldContain(qa.Answer);
    }

    [Fact]
    public async Task Complete_Should_Enqueue_Background_Job_And_Return_Report()
    {
        var meeting = await _appService.CreateAsync(new PreInfoInput());
        _bot.RecognizedFaces =
        [
            new FaceMatchDto { WorkerId = Guid.NewGuid().ToString(), Name = "王五", Confidence = 0.9 }
        ];
        await _appService.RecognizeAttendanceAsync(meeting.Id, new byte[] { 1 });
        _llm.QueueResponse("今天的安全交底重点是……");
        await _appService.AskQaAsync(meeting.Id, "安全交底是什么");
        _bot.TranscribeText = "会议转写内容";

        var completed = await _appService.CompleteAsync(meeting.Id);

        completed.Status.ShouldBe(MeetingStatus.Completed);
        completed.EndedAt.ShouldNotBeNull();
        _jobManager.LastEnqueued<CompleteMeetingArgs>()!.MeetingRecordId.ShouldBe(meeting.Id);

        var report = await _appService.GetReportAsync(meeting.Id);
        report.ShouldNotBeNull();
        report!.Attendance.ShouldNotBeEmpty();
        report.QaRecords.ShouldNotBeEmpty();
    }
}
