using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public interface IParticleEffectsManager
{
    AdvancedParticleProperties? Get(string code, string domain);
}

public class ParticleEffectsManager : IParticleEffectsManager
{
    private readonly Dictionary<string, AdvancedParticleProperties> mParticleProperties = new();

    public ParticleEffectsManager(ICoreAPI api)
    {
        List<IAsset> assets = api.Assets.GetManyInCategory("config", "particle-effects.json");

        foreach (IAsset asset in assets)
        {
            string domain = asset.Location.Domain;
            byte[] data = asset.Data;
            string json = System.Text.Encoding.UTF8.GetString(data);
            JObject token = JObject.Parse(json);
            foreach ((string code, JToken? effect) in token)
            {
                JsonObject effectJson = new(effect);
                mParticleProperties.Add($"{domain}:{code}", effectJson.AsObject<AdvancedParticleProperties>());
            }
        }
    }

    public AdvancedParticleProperties? Get(string code, string domain)
    {
        string key = $"{domain}:{code}";
        if (!mParticleProperties.ContainsKey(key)) return null;
        return mParticleProperties[key];
    }
}
