namespace DredgeAI.BidCompare;

public static class BidCompareErrorCodes
{
    public const string Namespace = "BidCompare:";

    public const string DocumentCountOutOfRange = Namespace + "DocumentCountOutOfRange";
    public const string UnsupportedFileType = Namespace + "UnsupportedFileType";
    public const string InvalidTaskState = Namespace + "InvalidTaskState";
    public const string DocumentNotFound = Namespace + "DocumentNotFound";
    public const string IrNotReady = Namespace + "IrNotReady";
    public const string IrValidationFailed = Namespace + "IrValidationFailed";
    public const string AnGineerParseFailed = Namespace + "AnGineerParseFailed";
    public const string NoTenderDocument = Namespace + "NoTenderDocument";
    public const string ClausesNotLocked = Namespace + "ClausesNotLocked";
    public const string ReportNotReady = Namespace + "ReportNotReady";
    public const string ExportJobNotFound = Namespace + "ExportJobNotFound";
    public const string ExportFailed = Namespace + "ExportFailed";
}
