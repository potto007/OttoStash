namespace OttoStash.APIs.MUC.MUCSrc.Data;

public interface IRequest : IPackage {
    int RequestID { get; set; }
    Inventory SourceInventory { get; }
    Inventory TargetInventory { get; }
}