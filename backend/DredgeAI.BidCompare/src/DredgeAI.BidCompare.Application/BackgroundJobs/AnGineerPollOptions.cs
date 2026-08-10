using System;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class AnGineerPollOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
}
