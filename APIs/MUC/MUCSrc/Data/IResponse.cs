namespace AzuAutoStore.APIs.MUC.MUCSrc.Data;

public interface IResponse : IPackage {
    int SourceID { get; set; }
    bool Success { get; set; }
    int Amount { get; set; }
}