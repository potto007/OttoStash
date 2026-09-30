using OttoStash.Storing;

namespace OttoStash.Tests;

public class PrefabNameTests
{
    [Theory]
    [InlineData("piece_chest_wood(Clone)", "piece_chest_wood")]
    [InlineData("piece_chest_wood (1)", "piece_chest_wood")]
    [InlineData("piece_chest_wood", "piece_chest_wood")]
    public void Strips_the_clone_suffix_and_instance_number(string sceneName, string prefab)
    {
        Assert.Equal(prefab, PrefabNames.FromSceneName(sceneName));
    }
}
