using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
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
    private readonly ILlmGateway _llmGateway;

    public WorkerProfileAppService(
        IRepository<WorkerProfile, Guid> workers,
        IMeetingBotClient bot,
        IFileStorage fileStorage,
        ILlmGateway llmGateway)
    {
        _workers = workers;
        _bot = bot;
        _fileStorage = fileStorage;
        _llmGateway = llmGateway;
    }

    public async Task<List<WorkerDto>> GetListAsync()
    {
        var all = await _workers.GetListAsync();
        return all.OrderBy(w => w.Name).Select(Map).ToList();
    }

    public async Task<WorkerDto> CreateAsync(WorkerCreateInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.EmployeeNo))
        {
            throw new BusinessException("MEETING_WORKER_REQUIRED", "姓名与工号/证件号不能为空");
        }
        var existing = await _workers.GetListAsync(w => w.EmployeeNo == input.EmployeeNo);
        if (existing.Count > 0)
        {
            return Map(existing[0]);
        }
        var worker = new WorkerProfile(GuidGenerator.Create(), input.Name.Trim(), input.EmployeeNo.Trim(), input.Team.Trim());
        await _workers.InsertAsync(worker);
        return Map(worker);
    }

    public async Task<IdCardRecognitionDto> RecognizeIdCardAsync(byte[] image)
    {
        const string prompt =
            "你是身份证信息识别助手。请识别图片中中华人民共和国居民身份证的正面字段，" +
            "仅返回如下 JSON（字段无法识别时置为空字符串，不要输出其他内容）：" +
            "{\"name\":\"姓名\",\"idCardNumber\":\"公民身份号码\",\"gender\":\"性别\"," +
            "\"nation\":\"民族\",\"birthDate\":\"出生日期\",\"address\":\"住址\"}";

        var base64 = Convert.ToBase64String(image);
        var raw = await _llmGateway.CompleteMultimodalAsync(
            "你是身份证信息识别助手，只输出指定 JSON。",
            prompt,
            new[] { new LlmImageInput("image/jpeg", base64) });

        var dto = ParseIdCardJson(raw);
        dto.RawText = raw;
        return dto;
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

    private static IdCardRecognitionDto ParseIdCardJson(string raw)
    {
        var dto = new IdCardRecognitionDto();
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return dto;
        }
        try
        {
            using var document = JsonDocument.Parse(raw.Substring(start, end - start + 1));
            var root = document.RootElement;
            dto.Name = ReadString(root, "name", "姓名");
            dto.IdCardNumber = ReadString(root, "idCardNumber", "公民身份号码", "id_card_number");
            dto.Gender = ReadString(root, "gender", "性别");
            dto.Nation = ReadString(root, "nation", "民族");
            dto.BirthDate = ReadString(root, "birthDate", "出生日期", "birth_date");
            dto.Address = ReadString(root, "address", "住址");
        }
        catch (JsonException)
        {
            // 非 JSON 返回时仅保留 RawText，字段留空由前端提示重试
        }
        return dto;
    }

    private static string ReadString(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString()?.Trim() ?? "";
            }
        }
        return "";
    }
}
