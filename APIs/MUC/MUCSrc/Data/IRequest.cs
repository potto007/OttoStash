namespace AzuAutoStore.APIs.MUC.MUCSrc.Data;

public interface IRequest : IPackage {
    int RequestID { get; set; }
    Inventory SourceInventory { get; }
    Inventory TargetInventory { get; }
}