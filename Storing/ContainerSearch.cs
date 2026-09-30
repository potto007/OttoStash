namespace OttoStash.Storing;

/// The "ottostashsearch" console command: pings every nearby container that
/// holds an item matching the query and turns the player toward the closest.
internal static class ContainerSearch
{
    private const float SearchRadius = 50f;
    private const float LookTurnSpeed = 3.5f;

    internal const string CommandName = "ottostashsearch";

    internal static void Register()
    {
        Terminal.ConsoleCommand command = new(CommandName,
            "[prefab name/query text] - search for items in chests near the player. It uses the prefab name",
            args =>
            {
                if (args.Length <= 1 || ZNetScene.instance == null)
                    return;
                Run(args[1]);
            },
            optionsFetcher: () => ZNetScene.instance == null ? new List<string>() : ZNetScene.instance.GetPrefabNames());
    }

    private static void Run(string query)
    {
        int itemCount = 0;
        IStoreTarget? closest = null;
        float closestDistance = float.MaxValue;

        foreach (IStoreTarget target in Matching(query, ref itemCount))
        {
            ChestEffects.Ping(target.GameObject);
            float distance = Vector3.Distance(target.GameObject.transform.position, Player.m_localPlayer.transform.position);
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            closest = target;
        }

        if (closest != null)
        {
            Vector3 toward = closest.GameObject.transform.position - Player.m_localPlayer.transform.position;
            Player.m_localPlayer.SetLookDir(toward, LookTurnSpeed);
        }

        // The count is summed while the matches are enumerated, so it is read after the loop.
        Player.m_localPlayer.Message(MessageHud.MessageType.Center, closest != null
            ? $"Found {itemCount} items matching '{query}' in nearby containers."
            : $"<color=red>No items matching '{query}' found in nearby containers.</color>");
    }

    private static List<IStoreTarget> Matching(string query, ref int itemCount)
    {
        string lowered = query.ToLower();
        List<IStoreTarget> matches = new();

        foreach (Piece piece in PiecesWithin(Player.m_localPlayer.transform.position, SearchRadius))
        {
            Container? chest = piece.GetComponent<Container>();
            if (chest == null || !Holds(chest, lowered, ref itemCount))
                continue;
            matches.Add(new ChestTarget(chest));
        }

        matches.AddRange(APIs.ItemDrawers_API.AllDrawers
            .Where(drawer => string.Equals(drawer.Prefab, query, StringComparison.CurrentCultureIgnoreCase))
            .Select(drawer => new KgDrawerTarget(drawer)));
        matches.AddRange(APIs.MkzItemDrawers_API.AllDrawers
            .Where(drawer => string.Equals(drawer.Prefab, query, StringComparison.CurrentCultureIgnoreCase))
            .Select(drawer => new MkzDrawerTarget(drawer)));

        return matches;
    }

    private static bool Holds(Container chest, string loweredQuery, ref int count)
    {
        List<ItemDrop.ItemData> matching = chest.GetInventory().GetAllItems()
            .Where(item => Utils.GetPrefabName(item.m_dropPrefab).ToLower().Contains(loweredQuery) || item.m_shared.m_name.ToLower().Contains(loweredQuery))
            .ToList();

        count += matching.Sum(item => item.m_stack);
        return matching.Count > 0;
    }

    private static List<Piece> PiecesWithin(Vector3 point, float radius)
    {
        if (Piece.s_ghostLayer == 0)
            Piece.s_ghostLayer = LayerMask.NameToLayer("ghost");

        List<Piece> pieces = new();
        foreach (Piece piece in Piece.s_allPieces)
        {
            if (piece.gameObject.layer != Piece.s_ghostLayer && Vector3.Distance(point, piece.transform.position) < radius)
                pieces.Add(piece);
        }

        return pieces;
    }
}
