using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace OttoStash.Favoriting;

/// The slots and item names one player has favorited, kept in a file per
/// player in the config folder. The file format is the one QuickStackStore
/// uses, and its file is read when that mod is installed, so favorites carry
/// across.
internal sealed class PlayerFavorites
{
    private const string QuickStackStoreGuid = "goldenrevolver.quick_stack_store";

    private static readonly Dictionary<long, PlayerFavorites> ByPlayer = new();
    private static readonly BinaryFormatter Formatter = new();

    private readonly string _path;
    private HashSet<Vector2i> _slots = new();
    private HashSet<string> _itemNames = new();

    private PlayerFavorites(long playerId)
    {
        string fileName = Chainloader.PluginInfos.ContainsKey(QuickStackStoreGuid)
            ? $"QuickStackStore_player_{playerId}.dat"
            : $"{ModName}_player_{playerId}.dat";
        _path = Path.Combine(Paths.ConfigPath, fileName);
        Load();
    }

    internal static PlayerFavorites For(long playerId)
    {
        if (ByPlayer.TryGetValue(playerId, out PlayerFavorites favorites))
            return favorites;

        favorites = new PlayerFavorites(playerId);
        ByPlayer[playerId] = favorites;
        return favorites;
    }

    internal void ToggleSlot(Vector2i position)
    {
        Toggle(_slots, position);
        Save();
    }

    internal void ToggleItemName(ItemDrop.ItemData.SharedData item)
    {
        Toggle(_itemNames, item.m_name);
        Save();
    }

    internal bool IsSlotFavorited(Vector2i position)
    {
        return _slots.Contains(position);
    }

    internal bool IsItemNameFavorited(ItemDrop.ItemData.SharedData item)
    {
        return _itemNames.Contains(item.m_name);
    }

    internal bool IsItemOrSlotFavorited(ItemDrop.ItemData item)
    {
        return IsItemNameFavorited(item.m_shared) || IsSlotFavorited(item.m_gridPos);
    }

    private static void Toggle<T>(HashSet<T> set, T value)
    {
        if (!set.Remove(value))
            set.Add(value);
    }

    // The file holds two serialized lists: slot coordinates as tuples, then item names.
    private void Save()
    {
        using Stream stream = File.Open(_path, FileMode.Create);
        List<Tuple<int, int>> slots = _slots.Select(slot => new Tuple<int, int>(slot.x, slot.y)).ToList();
        Formatter.Serialize(stream, slots);
        Formatter.Serialize(stream, _itemNames.ToList());
    }

    private void Load()
    {
        using Stream stream = File.Open(_path, FileMode.OpenOrCreate);
        stream.Seek(0L, SeekOrigin.Begin);

        _slots = new HashSet<Vector2i>();
        _itemNames = new HashSet<string>();

        foreach (Tuple<int, int> slot in ReadList<Tuple<int, int>>(stream))
            _slots.Add(new Vector2i(slot.Item1, slot.Item2));

        foreach (string name in ReadList<string>(stream))
            _itemNames.Add(name);
    }

    private static List<T> ReadList<T>(Stream stream)
    {
        try
        {
            return Formatter.Deserialize(stream) as List<T> ?? new List<T>();
        }
        catch (SerializationException)
        {
            return new List<T>();
        }
    }
}
