using YamlDotNet.Serialization;

namespace OttoStash.Pulling;

/// Brings an AzuCraftyBoxes setup across on first run: its settings into the
/// crafting section of the OttoStash config, and its per-container rules into
/// "pull" blocks of the OttoStash rules file. The rules file keeps its own
/// comments and layout; the carried rules are written into it as text.
internal static class PullCarryOver
{
    internal const string OldConfigName = "Azumatt.AzuCraftyBoxes.cfg";
    internal const string OldRulesName = "Azumatt.AzuCraftyBoxes.yml";

    /// Written once the rules are carried over, so they are never carried twice.
    internal const string Marker = "# OttoStash carried the pull rules over from Azumatt.AzuCraftyBoxes.yml. Delete this line to carry them over again.";

    private const string GroupsKey = "groups";
    private const string PullKey = "pull";
    private static readonly string[] ListKeys = ["exclude", "includeOverride"];

    /// The AzuCraftyBoxes setting each crafting entry takes its first value from.
    internal static void CarryOverConfig(string oldConfigPath, IEnumerable<(ConfigEntryBase Entry, string Section, string Key)> mapping)
    {
        ConfigFile old = new(oldConfigPath, false) { SaveOnConfigSet = false };
        foreach ((ConfigEntryBase entry, string section, string key) in mapping)
        {
            try
            {
                ConfigEntryBase oldEntry = BindLike(old, entry, section, key);
                entry.BoxedValue = oldEntry.BoxedValue;
            }
            catch (Exception e)
            {
                StashLog.Warning($"Could not carry '{section} / {key}' over from {OldConfigName}: {e.Message}");
            }
        }
    }

    // ConfigFile.Bind is generic, and the entry types differ.
    private static ConfigEntryBase BindLike(ConfigFile file, ConfigEntryBase entry, string section, string key)
    {
        MethodInfo bind = typeof(ConfigFile).GetMethods()
            .First(m => m.Name == nameof(ConfigFile.Bind) && m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[3].ParameterType == typeof(ConfigDescription))
            .MakeGenericMethod(entry.SettingType);
        return (ConfigEntryBase)bind.Invoke(file, [section, key, entry.DefaultValue, null]);
    }

    /// The OttoStash rules with the AzuCraftyBoxes rules merged in, or null when
    /// there is nothing to do: the marker is present already, or the old rules
    /// do not parse. A container either file names keeps its OttoStash entry and
    /// gains a pull block. A group OttoStash already defines keeps its OttoStash
    /// members.
    internal static string? Merge(string ottoYaml, string craftyYaml, List<string> notes)
    {
        if (ottoYaml.Contains(Marker))
            return null;

        Dictionary<string, Dictionary<string, List<string>>>? crafty;
        Dictionary<string, object>? otto;
        try
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            crafty = deserializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>?>(craftyYaml);
            otto = deserializer.Deserialize<Dictionary<string, object>?>(ottoYaml);
        }
        catch (Exception e)
        {
            notes.Add($"The rules could not be read, so none were carried over: {e.Message}");
            return null;
        }

        crafty ??= new Dictionary<string, Dictionary<string, List<string>>>();
        otto ??= new Dictionary<string, object>();

        List<string> lines = SplitLines(ottoYaml);
        List<string> appended = new();

        MergeGroups(crafty, otto, lines, appended, notes);

        foreach (KeyValuePair<string, Dictionary<string, List<string>>> entry in crafty)
        {
            if (entry.Key == GroupsKey || entry.Value == null)
                continue;

            List<string> pullBody = PullBody(entry.Value);
            if (pullBody.Count == 0)
                continue;

            if (!otto.ContainsKey(entry.Key))
            {
                appended.Add($"{entry.Key}:");
                appended.Add($"  {PullKey}:");
                appended.AddRange(pullBody.Select(line => "    " + line));
                continue;
            }

            if (otto[entry.Key] is Dictionary<object, object> existing && existing.ContainsKey(PullKey))
            {
                notes.Add($"'{entry.Key}' already has a pull block, so its AzuCraftyBoxes rules were left out.");
                continue;
            }

            if (!InsertUnderKey(lines, entry.Key, PullKey, pullBody))
                notes.Add($"'{entry.Key}' is not written as a plain block in the OttoStash rules, so its AzuCraftyBoxes rules were left out.");
        }

        lines.Add("");
        if (appended.Count > 0)
        {
            lines.Add("# Pull rules carried over from Azumatt.AzuCraftyBoxes.yml.");
            lines.AddRange(appended);
            lines.Add("");
        }

        lines.Add(Marker);
        lines.Add("");
        return string.Join("\n", lines);
    }

    private static void MergeGroups(Dictionary<string, Dictionary<string, List<string>>> crafty, Dictionary<string, object> otto, List<string> lines, List<string> appended, List<string> notes)
    {
        if (!crafty.TryGetValue(GroupsKey, out Dictionary<string, List<string>>? craftyGroups) || craftyGroups == null)
            return;

        Dictionary<object, object> ottoGroups = otto.TryGetValue(GroupsKey, out object groups) && groups is Dictionary<object, object> table ? table : new Dictionary<object, object>();
        List<string> newGroups = new();
        foreach (KeyValuePair<string, List<string>> group in craftyGroups)
        {
            if (ottoGroups.ContainsKey(group.Key))
            {
                notes.Add($"The group '{group.Key}' is defined in both files. The OttoStash members are kept.");
                continue;
            }

            newGroups.Add($"{Quote(group.Key)}:");
            newGroups.AddRange((group.Value ?? new List<string>()).Select(prefab => $"  - {prefab}"));
        }

        if (newGroups.Count == 0)
            return;

        if (otto.ContainsKey(GroupsKey))
        {
            if (!InsertUnderKey(lines, GroupsKey, null, newGroups))
                notes.Add("The OttoStash groups are not written as a plain block, so the AzuCraftyBoxes groups were left out.");
            return;
        }

        appended.Add($"{GroupsKey}:");
        appended.AddRange(newGroups.Select(line => "  " + line));
    }

    /// The pull block's body, unindented, from one AzuCraftyBoxes entry. Empty
    /// when the entry has no rules worth carrying.
    private static List<string> PullBody(Dictionary<string, List<string>> entry)
    {
        List<string> body = new();
        foreach (string listKey in ListKeys)
        {
            if (!entry.TryGetValue(listKey, out List<string>? items) || items == null || items.Count == 0)
                continue;
            body.Add($"{listKey}:");
            body.AddRange(items.Select(item => $"  - {Quote(item)}"));
        }

        return body;
    }

    /// Writes lines into the block of a top-level key, straight after the key
    /// line, at the indent the block already uses. With a child key the lines go
    /// under it. False when the key is not a plain "key:" line with a block under it.
    private static bool InsertUnderKey(List<string> lines, string key, string? childKey, List<string> body)
    {
        int keyLine = lines.FindIndex(line => IsTopLevelKey(line, key));
        if (keyLine < 0)
            return false;

        string indent = "  ";
        for (int i = keyLine + 1; i < lines.Count; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                continue;
            int width = lines[i].Length - trimmed.Length;
            if (width > 0)
                indent = lines[i].Substring(0, width);
            break;
        }

        List<string> insert = new();
        if (childKey != null)
        {
            insert.Add($"{indent}{childKey}:");
            insert.AddRange(body.Select(line => $"{indent}  {line}"));
        }
        else
        {
            insert.AddRange(body.Select(line => indent + line));
        }

        lines.InsertRange(keyLine + 1, insert);
        return true;
    }

    // "key:" alone or with a trailing comment. A key with an inline value is not
    // a block and is left alone.
    private static bool IsTopLevelKey(string line, string key)
    {
        if (!line.StartsWith(key + ":"))
            return false;
        string rest = line.Substring(key.Length + 1).Trim();
        return rest.Length == 0 || rest.StartsWith("#");
    }

    private static string Quote(string value)
    {
        bool plain = value.Length > 0 && value.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ' || c == '-' || c == '.') && !value.StartsWith("-") && value.Trim() == value;
        return plain ? value : "'" + value.Replace("'", "''") + "'";
    }

    private static List<string> SplitLines(string text)
    {
        List<string> lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }
}
