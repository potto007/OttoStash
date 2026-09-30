namespace OttoStash.APIs.MUC.MUCSrc.Data;

public interface IPackage {
    ZPackage WriteToPackage();

#if DEBUG
        void PrintDebug();
#endif
}