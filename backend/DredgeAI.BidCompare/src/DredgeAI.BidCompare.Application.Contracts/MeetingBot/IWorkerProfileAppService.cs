using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace DredgeAI.BidCompare.MeetingBot;

public interface IWorkerProfileAppService : IApplicationService
{
    Task<List<WorkerDto>> GetListAsync();

    Task<int> ImportAsync(byte[] file, string fileName);

    Task<WorkerDto> UpdateFaceAsync(Guid workerId, byte[] image);
}
