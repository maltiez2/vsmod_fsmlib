using MaltiezFSM.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Systems;

public class Particles : BaseSystem
{
    private readonly Dictionary<string, ParticleEffect> mParticleEffects = new();

    public Particles(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        if (definition.Token is not JObject definitionObject)
        {
            LogError($"Wrong definition format.");
            return;
        }

        foreach ((string effectCode, JToken? token) in definitionObject)
        {
            JsonObject effect = new(token);
            mParticleEffects.Add(effectCode, new(effect));
        }
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        List<string> effects = GetEffects(parameters);

        foreach (string effect in effects)
        {
            try
            {
                mParticleEffects[effect].Spawn(mApi, player);
            }
            catch (Exception exception)
            {
                LogDebug($"Exception while spawning particle effect '{effect}':\n{exception}");
            }
        }

        return true;
    }

    private List<string> GetEffects(JsonObject parameters)
    {
        List<string> codes = new();

        if (!parameters.KeyExists("effects"))
        {
            LogError($"No 'effects' in system request");
            return codes;
        }

        if (parameters["effects"].IsArray())
        {
            foreach (JsonObject effect in parameters["effects"].AsArray())
            {
                codes.Add(effect.AsString());
            }
        }
        else
        {
            codes.Add(parameters["effects"].AsString());
        }

        return codes;
    }
}
public readonly struct ParticleEffect
{
    public string Code { get; }
    public string Domain { get; }
    public Vec3f Position { get; }
    public Vec3f Velocity { get; }

    public ParticleEffect(JsonObject definition)
    {
        string domain = definition["domain"].AsString();
        string code = definition["code"].AsString();

        JsonObject position = definition["position"];
        JsonObject velocity = definition["velocity"];

        Code = code;
        Domain = domain;
        Position = new Vec3f(position["x"].AsFloat(), position["y"].AsFloat(), position["z"].AsFloat());
        Velocity = new Vec3f(velocity["x"].AsFloat(), velocity["y"].AsFloat(), velocity["z"].AsFloat());
    }

    public readonly void Spawn(ICoreAPI api, IPlayer player)
    {
        AdvancedParticleProperties? effect = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().ParticleEffects?.Get(Code, Domain);

        if (effect != null)
        {
            SpawnParticleEffect(effect, player.Entity);
        }
        else
        {
            Logger.Warn(api, this, $"Failed to load '{Domain}:{Code}' particle effect.");
        }
    }

    private readonly void SpawnParticleEffect(AdvancedParticleProperties effect, EntityAgent byEntity)
    {
        Vec3f worldPosition = Utils.FromCameraReferenceFrame(byEntity, Position);
        Vec3f worldVelocity = Utils.FromCameraReferenceFrame(byEntity, Velocity);

        effect.basePos = byEntity.SidedPos.AheadCopy(0).XYZ.Add(worldPosition.X, byEntity.LocalEyePos.Y + worldPosition.Y, worldPosition.Z);
        effect.baseVelocity = worldVelocity;

        byEntity.World.SpawnParticles(effect);
    }
}
