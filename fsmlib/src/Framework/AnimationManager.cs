using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib.API
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationRequest
    {
        public AnimationPlayerAction Action { get; set; }
        public long EntityId { get; set; }
        public CategoryIdentifier Category { get; set; }
        public AnimationIdentifier AnimationId { get; set; }
        public short Duration { get; set; }
        public ProgressModifierType Modifier { get; set; }
        public short? StartFrame { get; set; }
        public short? FinishFrame { get; set; }
    }

    public interface IAnimationManager : IDisposable
    {
        bool Register(AnimationIdentifier id, JsonObject definition);
        bool Register(AnimationIdentifier id, AnimationMetaData metaData);
        bool Register(AnimationIdentifier id, string playerAnimationCode);
        long Run(params AnimationRequest[] requests);
        void Stop(long runId);
    }




    public enum AnimationPlayerAction : byte
    {
        EaseIn,
        EaseOut,
        Start,
        Stop,
        Rewind,
        Clear,
        Set
    }

    public enum ProgressModifierType : byte
    {
        Linear,
        Quadratic,
        Cubic,
        Sqrt,
        Sin,
        SinQuadratic
    }

    public enum BlendingType
    {
        Add,
        Subtract,
        Average,
    }

    internal struct AnimationPlayerIdentifier
    {
        public long EntityId { get; set; }
        public CategoryIdentifier Category { get; set; }

        public AnimationPlayerIdentifier(AnimationRequest request)
        {
            this.EntityId = request.EntityId;
            this.Category = request.Category;
        }
        public static implicit operator AnimationPlayerIdentifier(AnimationRequest request) => new AnimationPlayerIdentifier(request);
    }

    public struct AnimationRunMetadata
    {
        public AnimationPlayerAction Action { get; set; }
        public short Duration { get; set; }
        public short? StartFrame { get; set; }
        public short? FinishFrame { get; set; }

        public AnimationRunMetadata(AnimationRequest request)
        {
            this.Action = request.Action;
            this.Duration = request.Duration;
            this.StartFrame = request.StartFrame;
            this.FinishFrame = request.FinishFrame;
        }
        public static implicit operator AnimationRunMetadata(AnimationRequest request) => new AnimationRunMetadata(request);
    }

    public struct AnimationIdentifier
    {
        public uint Hash { get; private set; }

        public AnimationIdentifier(string name) => new AnimationIdentifier() { Hash = Utils.ToCrc32(name) };
        public AnimationIdentifier(uint hash) => new AnimationIdentifier() { Hash = hash };

        public static implicit operator AnimationIdentifier(AnimationRequest request) => request.AnimationId;
    }

    public struct CategoryIdentifier
    {
        public uint Hash { get; private set; }
        public BlendingType Blending { get; private set; }

        public CategoryIdentifier((string name, BlendingType blending) parameters) => new CategoryIdentifier() { Blending = parameters.blending, Hash = Utils.ToCrc32(parameters.name) };
        public CategoryIdentifier((uint hash, BlendingType blending) parameters) => new CategoryIdentifier() { Blending = parameters.blending, Hash = parameters.hash };
        
        public static implicit operator CategoryIdentifier(AnimationRequest request) => request.Category;
    }

    public struct ComposeRequest
    {
        public long EntityId { get; set; }
    }

    public interface IAnimationResult : ICloneable
    {
        IAnimationResult Add(IAnimationResult value);
        IAnimationResult Subtract(IAnimationResult value);
        IAnimationResult Average(IAnimationResult value, float weight);
    }

    public interface IAnimation<out TAnimationResult> : IDisposable
        where TAnimationResult : IAnimationResult
    {
        public TAnimationResult Calculate(float progress);
    }

    public interface IAnimator<TAnimationResult> : IDisposable
        where TAnimationResult : IAnimationResult
    {
        public void Init(ICoreAPI api);
        public void Run(AnimationRunMetadata parameters, IAnimation<TAnimationResult> animation);
        public TAnimationResult Calculate(int timeElapsed_ms);
    }

    public interface IAnimationComposer<TAnimationResult> : IDisposable
        where TAnimationResult : IAnimationResult
    {
        void SetAnimatorType<TAnimator>()
            where TAnimator : IAnimator<TAnimationResult>;
        bool Register(AnimationIdentifier id, IAnimation<TAnimationResult> animation);
        void Run(AnimationRequest request);
        TAnimationResult Compose(ComposeRequest request);
    }

    public interface IAnimationSynchronizer : IDisposable
    {
        public delegate void AnimationRequestHandler(AnimationRequest request);
        void Init(ICoreAPI api, AnimationRequestHandler handler, string channelName);
        void Sync(AnimationRequest request);
    }

    static public class ProgressModifiers
    {
        public delegate float ProgressModifier(float progress);

        private readonly static Dictionary<ProgressModifierType, ProgressModifier> Modifiers = new()
        {
            { ProgressModifierType.Linear,       (float progress) => GameMath.Clamp(progress, 0, 1) },
            { ProgressModifierType.Quadratic,    (float progress) => GameMath.Clamp(progress * progress, 0, 1) },
            { ProgressModifierType.Cubic,        (float progress) => GameMath.Clamp(progress * progress * progress, 0, 1) },
            { ProgressModifierType.Sqrt,         (float progress) => GameMath.Sqrt(GameMath.Clamp(progress, 0, 1)) },
            { ProgressModifierType.Sin,          (float progress) => GameMath.Sin(GameMath.Clamp(progress, 0, 1) * 2 / GameMath.PI) },
            { ProgressModifierType.SinQuadratic, (float progress) => GameMath.Sin(GameMath.Clamp(progress * progress, 0, 1) * 2 / GameMath.PI) }
        };

        public static ProgressModifier Get(ProgressModifierType id) => Modifiers[id];
        public static bool TryAdd(ProgressModifierType id, ProgressModifier modifier) => Modifiers.TryAdd(id, modifier);
    }

    static public class Utils
    {
        public static uint ToCrc32(string value) => GameMath.Crc32(value.ToLowerInvariant()) & int.MaxValue;
    }
}