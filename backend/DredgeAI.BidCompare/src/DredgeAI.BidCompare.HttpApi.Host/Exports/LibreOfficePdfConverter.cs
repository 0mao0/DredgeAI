using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.Exports;

/// <summary>LibreOffice headless 转换：soffice --headless --convert-to pdf --outdir {tmp} report.docx。</summary>
public class LibreOfficePdfConverter : IPdfConverter, ITransientDependency
{
    private readonly LibreOfficeOptions _options;

    public LibreOfficePdfConverter(IOptions<LibreOfficeOptions> options)
    {
        _options = options.Value;
    }

    public async Task<byte[]> ConvertToPdfAsync(byte[] docxContent, CancellationToken cancellationToken = default)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "bidcompare-export", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var docxPath = Path.Combine(workDir, "report.docx");
            await File.WriteAllBytesAsync(docxPath, docxContent, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = _options.SofficePath,
                Arguments = $"--headless --convert-to pdf --outdir \"{workDir}\" \"{docxPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo)
                ?? throw new BusinessException(BidCompareErrorCodes.ExportFailed).WithData("reason", "无法启动 soffice");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(System.TimeSpan.FromSeconds(_options.TimeoutSeconds));
            await process.WaitForExitAsync(timeoutCts.Token);

            var pdfPath = Path.Combine(workDir, "report.pdf");
            if (process.ExitCode != 0 || !File.Exists(pdfPath))
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new BusinessException(BidCompareErrorCodes.ExportFailed)
                    .WithData("exitCode", process.ExitCode)
                    .WithData("stderr", stderr);
            }

            return await File.ReadAllBytesAsync(pdfPath, cancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, recursive: true);
            }
            catch (IOException)
            {
                // 临时目录清理失败不影响导出结果
            }
        }
    }
}
