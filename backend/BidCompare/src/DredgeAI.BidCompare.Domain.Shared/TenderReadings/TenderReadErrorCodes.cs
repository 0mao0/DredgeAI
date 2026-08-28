namespace DredgeAI.BidCompare.TenderReadings;

public static class TenderReadErrorCodes
{
    public const string Namespace = "TenderRead:";

    public const string InvalidTaskState = Namespace + "InvalidTaskState";
    public const string UnsupportedFileType = Namespace + "UnsupportedFileType";
    public const string DocumentNotFound = Namespace + "DocumentNotFound";
    public const string DocumentNotParsed = Namespace + "DocumentNotParsed";
    public const string IrNotReady = Namespace + "IrNotReady";
    public const string IrValidationFailed = Namespace + "IrValidationFailed";
    public const string AnGineerParseFailed = Namespace + "AnGineerParseFailed";
    public const string BaselineNotFound = Namespace + "BaselineNotFound";
    public const string FieldNotFound = Namespace + "FieldNotFound";
}
