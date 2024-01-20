using MaltiezFSM.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using VSImGui;

namespace MaltiezFSM.Systems;

public class Particles : BaseSystem
{
    private readonly Dictionary<string, ParticleEffect[]> mParticleEffects = new();

    public Particles(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        if (definition.Token is not JObject definitionObject)
        {
            LogError($"Wrong definition format.");
            return;
        }

        foreach ((string effectCode, JToken? token) in definitionObject)
        {
            if (token is not JArray && token is not JObject) continue;
            List<JsonObject> effect = ParseField(new(definitionObject), effectCode);
            mParticleEffects.Add(effectCode, effect.Select(value => new ParticleEffect(value)).ToArray());
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
                foreach (ParticleEffect particleEffect in mParticleEffects[effect])
                {
                    particleEffect.Spawn(mApi, player);
                }
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
public class ParticleEffect
{
    public string Code { get; }
    public string Domain { get; }
    public Vec3f Position { get; }
    public Vec3f Velocity { get; }

    public ParticleEffect(JsonObject definition)
    {
        string domain = definition["domain"].AsString();
        string code = definition["code"].AsString();

        JsonObject[] position = definition["position"].AsArray();
        JsonObject[] velocity = definition["velocity"].AsArray();

        Code = code;
        Domain = domain;
        Position = new Vec3f(position[0].AsFloat(), position[1].AsFloat(), position[2].AsFloat());
        Velocity = new Vec3f(velocity[0].AsFloat(), velocity[1].AsFloat(), velocity[2].AsFloat());

#if DEBUG
        DebugWindow.Float3Drag($"fsmlib", "particleEffects", $"{Code} - Position", () => Position, value => Position.Set(value.Array));
        DebugWindow.Float3Drag($"fsmlib", "particleEffects", $"{Code} - Velocity", () => Velocity, value => Velocity.Set(value.Array));
#endif
    }

    public void Spawn(ICoreAPI api, IPlayer player)
    {
#if DEBUG
        if (api.Side != EnumAppSide.Client) return;
#else
        if (api.Side != EnumAppSide.Server) return;
#endif

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

    private void SpawnParticleEffect(AdvancedParticleProperties effect, EntityAgent byEntity)
    {
        Vec3f worldPosition = Utils.FromCameraReferenceFrame(byEntity, Position);
        Vec3f worldVelocity = Utils.FromCameraReferenceFrame(byEntity, Velocity);

        effect.basePos = byEntity.SidedPos.AheadCopy(0).XYZ.Add(worldPosition.X, byEntity.LocalEyePos.Y + worldPosition.Y, worldPosition.Z);
        effect.baseVelocity = worldVelocity;

        /*Vec3f view = byEntity.SidedPos.GetViewVector().Clone().Normalize();
        Vec3f velocity = worldVelocity.Clone().Normalize();

        if (Velocity.Length() > 10)
        {
            DebugWindow.Text("fsmlib", "test", 0, $"View:\t{view}");
            DebugWindow.Text("fsmlib", "test", 1, $"Velocity:\t{velocity}");
            DebugWindow.Text("fsmlib", "test", 2, $"Diff:\t{velocity - view}");
        }*/

        byEntity.World.SpawnParticles(effect);
    }
}
