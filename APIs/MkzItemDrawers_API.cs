using Object = UnityEngine.Object;

namespace OttoStash.APIs;

public static class MkzItemDrawers_API
{
    private static readonly bool _isInstalled;
    private static readonly Type? _drawerType; // DrawerContainer
    private static readonly FieldInfo? _fiItem; // private ItemDrop.ItemData _item
    private static readonly FieldInfo? _fiQuantity; // private int _quantity

    public class Drawer
    {
        private readonly Component _drawer;
        private readonly ZNetView _nview;

        internal Drawer(Component drawer)
        {
            _drawer = drawer;
            _nview = drawer.GetComponent<ZNetView>();
        }

        public GameObject gameObject => _drawer.gameObject;
        public ZNetView m_nview => _nview;
        public Vector3 Position => _drawer.transform.position;

        public string Prefab
        {
            get
            {
                if (_fiItem == null) return null;
                ItemDrop.ItemData? itemData = _fiItem.GetValue(_drawer) as ItemDrop.ItemData;
                GameObject? go = itemData?.m_dropPrefab;
                return go ? go.name : null;
            }
        }

        public int? Amount
        {
            get
            {
                if (_fiQuantity == null) return null;
                return (int)_fiQuantity.GetValue(_drawer);
            }
        }

        public bool Accepts(string prefab)
        {
            string current = Prefab;
            return string.IsNullOrEmpty(current) || string.Equals(current, prefab, StringComparison.Ordinal);
        }

        public void Add(string prefab, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(prefab)) return;
            if (!_nview) return;
            _nview.ClaimOwnership();
            _nview.InvokeRPC("AddItem", prefab, amount);
        }
    }

    public static List<Drawer> AllDrawers => !_isInstalled ? [] : FindAllRuntimeDrawers();

    public static List<Drawer> AllDrawersInRange(Vector3 pos, float range) => !_isInstalled
        ? []
        : FindAllRuntimeDrawers().Where(d => Vector3.Distance(d.Position, pos) <= range).ToList();

    static MkzItemDrawers_API()
    {
        _drawerType = Type.GetType("DrawerContainer, itemdrawers");

        if (_drawerType == null)
        {
            _isInstalled = false;
            return;
        }

        _fiItem = _drawerType.GetField("_item", BindingFlags.Instance | BindingFlags.NonPublic);
        _fiQuantity = _drawerType.GetField("_quantity", BindingFlags.Instance | BindingFlags.NonPublic);

        _isInstalled = true;
    }

    private static List<Drawer> FindAllRuntimeDrawers()
    {
        List<Drawer> list = [];

        Object[]? components = Object.FindObjectsByType(_drawerType, FindObjectsSortMode.None);
        foreach (Object c in components)
        {
            if (c is not Component comp) continue;
            ZNetView? znv = comp.GetComponent<ZNetView>();
            if (!znv || !znv.IsValid()) continue;

            list.Add(new Drawer(comp));
        }

        return list;
    }
}