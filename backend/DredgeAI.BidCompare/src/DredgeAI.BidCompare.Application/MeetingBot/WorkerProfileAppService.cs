using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Storage;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>
/// 工人档案与人脸库：花名册批量导入（xlsx/zip）+ 现场补录人脸。
/// </summary>
[RemoteService(false)] // 精确路由由 HttpApi 显式 Controller 暴露（/api/meeting/workers）
public class WorkerProfileAppService : ApplicationService, IWorkerProfileAppService
{
    private readonly IRepository<WorkerProfile, Guid> _workers;
    private readonly IMeetingBotClient _bot;
    private readonly IFileStorage _fileStorage;

    public WorkerProfileAppService(
        IRepository<WorkerProfile, Guid> workers,
        IMeetingBotClient bot,
        IFileStorage fileStorage)
    {
        _workers = workers;
        _bot = bot;
        _fileStorage = fileStorage;
    }

    public async Task<List<WorkerDto>> GetListAsync()
    {
        var all = await _workers.GetListAsync();
        return all.OrderBy(w => w.Name).Select(Map).ToList();
    }

    [DisableValidation] // byte[] 无需方法级校验，避免 Stream/数组递归校验开销
    public async Task<int> ImportAsync(byte[] file, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var count = extension switch
        {
            ".xlsx" => await ImportXlsxAsync(new MemoryStream(file)),
            ".zip" => await ImportZipAsync(new MemoryStream(file)),
            _ => throw new BusinessException("MEETING_WORKER_BAD_FORMAT", "仅支持 xlsx（花名册）或 zip（照片包）")
        };
        return count;
    }

    public async Task<WorkerDto> UpdateFaceAsync(Guid workerId, byte[] image)
    {
        var worker = await _workers.GetAsync(workerId);
        await _bot.EnrollAsync(worker.Id.ToString(), worker.Name, image);

        var key = $"meeting/workers/{worker.Id}/face-{DateTime.Now:yyyyMMddHHmmss}.jpg";
        await using var stream = new MemoryStream(image);
        await _fileStorage.UploadAsync(key, stream, "image/jpeg");

        var photos = new List<string>();
        try
        {
            photos = JsonSerializer.Deserialize<List<string>>(worker.FacePhotosJson) ?? [];
        }
        catch (JsonException)
        {
            // 忽略损坏数据，重建列表
        }
        photos.Add(key);
        worker.MarkEnrolled(JsonSerializer.Serialize(photos));
        await _workers.UpdateAsync(worker);
        return Map(worker);
    }

    private async Task<int> ImportXlsxAsync(Stream file)
    {
        var imported = 0;
        using var document = SpreadsheetDocument.Open(file, false);
        var sheet = document.WorkbookPart?.Workbook?.Sheets?.GetFirstChild<Sheet>();
        if (sheet is null)
        {
            return 0;
        }
        if (sheet.Id is null || document.WorkbookPart is null)
        {
            return 0;
        }
        var worksheetPart = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id.Value);
        var rows = worksheetPart.Worksheet.Descendants<Row>().ToList();

        var existing = await _workers.GetListAsync();
        var byEmployeeNo = existing.ToDictionary(w => w.EmployeeNo, w => w);

        foreach (var row in rows.Skip(1)) // 跳过表头
        {
            var cells = row.Descendants<Cell>().ToList();
            var name = GetCellText(cells, 0, document)?.Trim() ?? "";
            var employeeNo = GetCellText(cells, 1, document)?.Trim() ?? "";
            var team = GetCellText(cells, 2, document)?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(employeeNo))
            {
                continue;
            }

            if (byEmployeeNo.TryGetValue(employeeNo, out var existingWorker))
            {
                continue; // 已存在则跳过（避免重复注册）
            }
            var worker = new WorkerProfile(GuidGenerator.Create(), name, employeeNo, team);
            await _workers.InsertAsync(worker);
            byEmployeeNo[employeeNo] = worker;
            imported++;
        }
        return imported;
    }

    private async Task<int> ImportZipAsync(Stream file)
    {
        var imported = 0;
        var existing = await _workers.GetListAsync();
        var byEmployeeNo = existing.ToDictionary(w => w.EmployeeNo, w => w);

        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || !entry.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                && !entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var baseName = Path.GetFileNameWithoutExtension(entry.Name);
            var employeeNo = baseName;
            var name = baseName;
            if (baseName.Contains('-'))
            {
                var parts = baseName.Split('-', 2);
                employeeNo = parts[0].Trim();
                name = parts[1].Trim();
            }
            if (string.IsNullOrWhiteSpace(employeeNo))
            {
                continue;
            }

            await using var photoStream = entry.Open();
            using var ms = new MemoryStream();
            await photoStream.CopyToAsync(ms);

            if (!byEmployeeNo.TryGetValue(employeeNo, out var worker))
            {
                worker = new WorkerProfile(GuidGenerator.Create(), name, employeeNo, "");
                await _workers.InsertAsync(worker);
                byEmployeeNo[employeeNo] = worker;
            }
            await _bot.EnrollAsync(worker.Id.ToString(), worker.Name, ms.ToArray());
            worker.MarkEnrolled("[]");
            await _workers.UpdateAsync(worker);
            imported++;
        }
        return imported;
    }

    private static string? GetCellText(List<Cell> cells, int index, SpreadsheetDocument document)
    {
        if (index >= cells.Count || cells[index]?.CellValue is null)
        {
            return null;
        }
        var value = cells[index].CellValue!.Text;
        if (cells[index].DataType?.Value == CellValues.SharedString)
        {
            var shared = document.WorkbookPart?.SharedStringTablePart?.SharedStringTable;
            if (shared is not null && int.TryParse(value, out var id) && id < shared.ChildElements.Count)
            {
                return shared.ChildElements[id].InnerText;
            }
        }
        return value;
    }

    private static WorkerDto Map(WorkerProfile worker) => new()
    {
        Id = worker.Id,
        Name = worker.Name,
        EmployeeNo = worker.EmployeeNo,
        Team = worker.Team,
        FaceStatus = worker.FaceStatus
    };
}
