using YamlDotNet.Serialization;

namespace OttoStash.Util;

public static class YamlUtils
{
    internal static void ReadYaml(string yamlInput)
    {
        IDeserializer? deserializer = new DeserializerBuilder().Build();
        yamlData = deserializer.Deserialize<Dictionary<string, object>>(yamlInput);
        OttoStashLogger.LogDebug($"yamlData:\n{yamlInput}");
    }

    internal static void ParseGroups()
    {
        // Initialize the groups dictionary if it's null
        groups ??= new Dictionary<string?, HashSet<string?>>();

        // Validate yamlData before trying to use it
        if (yamlData == null)
        {
            OttoStashLogger.LogError("yamlData is null.");
            return;
        }

        if (yamlData.TryGetValue("groups", out object groupData))
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

                        groups[groupName] = prefabNames;
                    }
                }
            }
            else
            {
                OttoStashLogger.LogError("groupData is not of type Dictionary<object, object>.");
            }
        }
        else
        {
            OttoStashLogger.LogError("No 'groups' key found in yamlData.");
        }
    }

    public static void WriteYaml(string filePath)
    {
        ISerializer? serializer = new SerializerBuilder().Build();
        using StreamWriter? output = new StreamWriter(filePath);
        serializer.Serialize(output, yamlData);

        // Serialize the data again to YAML format
        string serializedData = serializer.Serialize(yamlData);

        // Append the serialized YAML data to the file
        File.AppendAllText(filePath, serializedData);
    }
}