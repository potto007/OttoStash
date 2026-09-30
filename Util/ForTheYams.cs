using YamlDotNet.Serialization;

namespace OttoStash.Util;

public static class YamlUtils
{
    internal static void ReadYaml(string yamlInput)
    {
        IDeserializer? deserializer = new DeserializerBuilder().Build();
        ContainerRules.Rules = deserializer.Deserialize<Dictionary<string, object>>(yamlInput);
        Functions.LogIfBuildDebug($"Container rules:\n{yamlInput}");
    }

    internal static void ParseGroups()
    {
        // First use creates the group table.
        ContainerRules.Groups ??= new Dictionary<string?, HashSet<string?>>();

        // Nothing to parse until the rules file has been read.
        if (ContainerRules.Rules == null)
        {
            Functions.LogError("The container rules are not loaded.");
            return;
        }

        if (ContainerRules.Rules.TryGetValue("groups", out object groupData))
        {
            // Safely cast to the expected Dictionary type
            if (groupData is Dictionary<object, object> groupDict)
            {
                foreach (KeyValuePair<object, object> group in groupDict)
                {
                    string? groupName = group.Key?.ToString();
                    if (groupName == null) continue; // Skip if the key can't be converted to string

                    // Safely cast to the expected List type
                    if (group.Value is List<object> prefabs)
                    {
                        HashSet<string?> prefabNames = [];

                        foreach (object prefab in prefabs)
                        {
                            string? prefabName = prefab?.ToString();
                            if (prefabName != null)
                            {
                                prefabNames.Add(prefabName);
                            }
                        }

                        ContainerRules.Groups[groupName] = prefabNames;
                    }
                }
            }
            else
            {
                Functions.LogError("groupData is not of type Dictionary<object, object>.");
            }
        }
        else
        {
            Functions.LogError("The container rules have no groups entry.");
        }
    }

    public static void WriteYaml(string filePath)
    {
        ISerializer? serializer = new SerializerBuilder().Build();
        using StreamWriter? output = new StreamWriter(filePath);
        serializer.Serialize(output, ContainerRules.Rules);

        // Serialize the data again to YAML format
        string serializedData = serializer.Serialize(ContainerRules.Rules);

        // Append the serialized YAML data to the file
        File.AppendAllText(filePath, serializedData);
    }
}