using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shiw.File;
using Shiw.File.Application.Contracts;
using Shiw.File.Domain;
using Shiw.File.OpsFiles;
using Shiw.File.Web;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using FileOptions = Shiw.File.Domain.Shared.FileOptions;
using BuiltInFileController = Shiw.File.FileController;

namespace DredgeAI.Controllers;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(BuiltInFileController), IncludeSelf = true)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("文件管理")]
public class FileController(IFileService fileService) : BuiltInFileController(fileService);

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(FileThumbnailWebController), IncludeSelf = true)]
[Tags("文件管理")]
public class MyFileThumbnailWebController(
    IShiwBlobContainer blobContainer,
    IFileService fileService,
    ImageManager imageManager,
    IOptions<FileOptions> options)
    : FileThumbnailWebController(blobContainer, fileService, imageManager,
        options);

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(FileDownloadWebController), IncludeSelf = true)]
[Tags("文件管理")]
public class MyFileDownloadWebController(
    IShiwBlobContainer blobContainer,
    IFileService fileService,
    ImageManager imageManager,
    IOptions<FileOptions> options)
    : FileDownloadWebController(blobContainer, fileService, imageManager,
        options);

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(OpsFileController), IncludeSelf = true)]
[Tags("文件管理")]
[Obsolete]
[RemoteService(false)]
public class MyOpsFileController(IOpsFileService opsFileService) : OpsFileController(opsFileService);