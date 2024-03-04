using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Framework;

public class TemplatesManager
{
    private readonly Dictionary<string, JsonObject> mTemplates = new();
    
    public TemplatesManager(ICoreAPI api)
    {
        List<IAsset> assets = api.Assets.GetManyInCategory("config", "fsmlib/templates.json");

        foreach (IAsset asset in assets)
        {
            string domain = asset.Location.Domain;
            byte[] data = asset.Data;
            string json = System.Text.Encoding.UTF8.GetString(data);
            JObject token = JObject.Parse(json);
            JsonObject assetObject = new(token);

        }
    }
}

public class Template
{
    
    public Template(JsonObject definition)
    {
        Utils.Iterate(definition["parameters"], (path, parameter) => mParameters.Add(new(path, parameter.AsString())));
        mValue = definition["value"];
    }

    private readonly JsonObject mValue;
    private readonly List<TemplateParameter> mParameters;
}

public class TemplateParameter
{
    public TemplateParameter(string path, string parameter)
    {

    }
}