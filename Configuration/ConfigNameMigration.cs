// Vendored from Ottomation_ModLib, Ottomation.Lib.Config/ConfigNameMigration.cs, by way
// of OttoAura, with one deliberate change: when a file holds a setting under both
// spellings, the old spelling wins. The library keeps the new one, because there the
// old line can only come from a downgrade. In OttoStash the new spelling was written by
// 3.2.0 and the old one by 3.2.1 through 3.5.1, so the old line is the newer value.
// The line rewrite moved into Rewrite so the tests can run it without a ConfigFile.
// Source of record: Ottomation_ModLib ADR-0007 and ADR-0008, and OttoStash ADR 0003.

using System.Text;
using BepInEx.Logging;

namespace OttoStash.Configuration;

/// <summary>
/// One time rewrite of a plugin's existing .cfg so the section headers and keys carry
/// the <see cref="ConfigName" /> spelling before anything binds against them.
/// </summary>
/// <remarks>
/// Run this after the plugin's ConfigFile exists and before the first Bind. Values,
/// comments and any line that does not parse are copied through untouched, and a file
/// that already reads correctly is not rewritten at all.
/// </remarks>
public static class ConfigNameMigration
{
    public static void Apply(ConfigFile file, ManualLogSource? logger)
    {
        try
        {
            string path = file.ConfigFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                // A first run has nothing to migrate.
                return;
            }
            string[]? lines = Rewrite(File.ReadAllLines(path), logger);
            if (lines == null)
            {
                return;
            }
            // The whole text is built first so a failure can not truncate the config.
            StringBuilder builder = new StringBuilder();
            foreach (string line in lines)
            {
                builder.Append(line).Append(Environment.NewLine);
            }
            // The backup is the only copy of the player's values once the write lands,
            // so it is written before the rename and removed only on success.
            string backup = path + ".premigration.bak";
            File.Copy(path, backup, overwrite: true);
            File.WriteAllText(path, builder.ToString());
            logger?.LogInfo("Migrated config names to PascalCase: " + path);
            try
            {
                // Rebind against the new names so the old values are picked up.
                file.Reload();
            }
            catch (Exception reloadFailure)
            {
                // The file on disk is already renamed. Leaving it there would let the next
                // Bind save an indeterminate table over it, so put the original back.
                File.Copy(backup, path, overwrite: true);
                logger?.LogWarning($"Config name migration rolled back: {reloadFailure}");
                return;
            }
            finally
            {
                TryDelete(backup, logger);
            }
        }
        catch (Exception ex)
        {
            // A failed migration must never stop the plugin from loading.
            logger?.LogWarning($"Config name migration skipped: {ex}");
        }
    }

    /// <summary>
    /// The file's lines with every section and key in the <see cref="ConfigName" />
    /// spelling, or null when the file already reads correctly.
    /// </summary>
    internal static string[]? Rewrite(string[] lines, ManualLogSource? logger)
    {
        // OttoStash 3.2.0 wrote the new spelling and 3.2.1 went back to the old one,
        // and BepInEx keeps a line it no longer binds, so a file can hold both. The old
        // line is the one the player last saw, so it wins and the other is dropped.
        HashSet<string> legacy = CollectLegacyNames(lines);
        HashSet<string> seen = new HashSet<string>();
        List<string> kept = new List<string>();
        string rawSection = string.Empty;
        string currentSection = string.Empty;
        bool changed = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string? sectionName = SectionName(lines[i]);
            if (sectionName != null)
            {
                rawSection = sectionName;
                currentSection = ConfigName.Section(sectionName);
            }
            string? duplicateKey = DuplicateKey(lines[i], rawSection, currentSection, legacy, seen);
            if (duplicateKey != null)
            {
                logger?.LogWarning("Dropping the stale duplicate " + duplicateKey);
                changed = true;
                continue;
            }
            string? rewritten = RewriteLine(lines[i]);
            kept.Add(rewritten ?? lines[i]);
            if (rewritten != null)
            {
                changed = true;
            }
        }
        return changed ? kept.ToArray() : null;
    }

    /// <summary>
    /// Returns the section name when the line is a section header, otherwise null.
    /// </summary>
    private static string? SectionName(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
        {
            return trimmed.Substring(1, trimmed.Length - 2);
        }
        return null;
    }

    /// <summary>
    /// Returns the key name of a "Section/Key" entry line, or null when the line is not one.
    /// </summary>
    private static string? KeyName(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed[0] == '#' || SectionName(line) != null)
        {
            return null;
        }
        int equals = line.IndexOf('=');
        if (equals < 0)
        {
            return null;
        }
        string key = line.Substring(0, equals).Trim();
        return (key.Length == 0) ? null : key;
    }

    /// <summary>
    /// Every "Section/Key" pair the file holds under an old spelling, of the section or
    /// of the key. A line already in the new spelling for one of these is stale.
    /// </summary>
    private static HashSet<string> CollectLegacyNames(string[] lines)
    {
        HashSet<string> names = new HashSet<string>();
        string rawSection = string.Empty;
        string section = string.Empty;
        foreach (string line in lines)
        {
            string? sectionName = SectionName(line);
            if (sectionName != null)
            {
                rawSection = sectionName;
                section = ConfigName.Section(sectionName);
                continue;
            }
            string? key = KeyName(line);
            if (key == null)
            {
                continue;
            }
            string normalized = ConfigName.Key(key);
            if (normalized.Length > 0 && (normalized != key || section != rawSection))
            {
                names.Add(section + "/" + normalized);
            }
        }
        return names;
    }

    /// <summary>
    /// Returns the "Section/Key" pair to report when this line must be dropped, or null
    /// when the line is kept. A line is dropped when it is already in the new spelling
    /// and the file also holds the setting under an old one, or when its new spelling
    /// was already seen.
    /// </summary>
    private static string? DuplicateKey(string line, string rawSection, string section, HashSet<string> legacy, HashSet<string> seen)
    {
        string? key = KeyName(line);
        if (key == null)
        {
            return null;
        }
        string normalized = ConfigName.Key(key);
        if (normalized.Length == 0)
        {
            return null;
        }
        string pair = section + "/" + normalized;
        bool newSpelling = normalized == key && section == rawSection;
        if (newSpelling && legacy.Contains(pair))
        {
            return rawSection + "/" + key;
        }
        if (!seen.Add(pair))
        {
            return rawSection + "/" + key;
        }
        return null;
    }

    /// <summary>
    /// Removes the backup once the migration has taken. A backup left behind is harmless,
    /// so a failure here is logged and nothing more.
    /// </summary>
    private static void TryDelete(string path, ManualLogSource? logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Could not remove the migration backup {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the rewritten line, or null when the line must be copied through as is.
    /// </summary>
    private static string? RewriteLine(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            // Comments, both # and ##, are regenerated by BepInEx from the descriptions.
            return null;
        }
        if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
        {
            string section = trimmed.Substring(1, trimmed.Length - 2);
            string normalized = ConfigName.Section(section);
            if (normalized.Length == 0 || normalized == section)
            {
                return null;
            }
            return Indent(line) + "[" + normalized + "]" + TrailingSpace(line);
        }
        int equals = line.IndexOf('=');
        if (equals < 0)
        {
            return null;
        }
        string left = line.Substring(0, equals);
        string key = left.Trim();
        string normalizedKey = ConfigName.Key(key);
        if (key.Length == 0 || normalizedKey.Length == 0 || normalizedKey == key)
        {
            return null;
        }
        // Only the left side moves. Everything from the '=' onward survives byte for byte.
        return Indent(left) + normalizedKey + TrailingSpace(left) + line.Substring(equals);
    }

    private static string Indent(string line)
    {
        int i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i]))
        {
            i++;
        }
        return line.Substring(0, i);
    }

    private static string TrailingSpace(string line)
    {
        int i = line.Length;
        while (i > 0 && char.IsWhiteSpace(line[i - 1]))
        {
            i--;
        }
        return line.Substring(i);
    }
}
