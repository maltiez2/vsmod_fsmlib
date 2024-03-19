using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Framework.Simplified.Systems;

public readonly struct Angle
{
    public float Radians => _value;
    public float Degrees => _value * GameMath.RAD2DEG;
    public float Minutes => _value * GameMath.RAD2DEG * 60f;
    public float Seconds => _value * GameMath.RAD2DEG * 3600f;

    public override string ToString() => $"{_value * GameMath.RAD2DEG:F2} deg";
    public override bool Equals(object? obj) => ((Angle?)obj)?._value == _value;
    public override int GetHashCode() => _value.GetHashCode();

    public static Angle Zero => new(0);

    public static Angle FromRadians(float radians) => new(radians);
    public static Angle FromDegrees(float degrees) => new(degrees * GameMath.DEG2RAD);
    public static Angle FromMinutes(float minutes) => new(minutes * GameMath.DEG2RAD / 60f);
    public static Angle FromSeconds(float seconds) => new(seconds * GameMath.DEG2RAD / 3600f);

    public static Angle operator +(Angle a, Angle b) => new(a._value + b._value);
    public static Angle operator -(Angle a, Angle b) => new(a._value - b._value);
    public static Angle operator *(Angle a, float b) => new(a._value * b);
    public static Angle operator *(float a, Angle b) => new(a * b._value);
    public static Angle operator /(Angle a, float b) => new(a._value / b);
    public static float operator /(Angle a, Angle b) => a._value / b._value;

    public static bool operator ==(Angle a, Angle b) => MathF.Abs(a._value - b._value) < Epsilon(a._value, b._value);
    public static bool operator !=(Angle a, Angle b) => MathF.Abs(a._value - b._value) >= Epsilon(a._value, b._value);
    public static bool operator <(Angle a, Angle b) => a._value < b._value && a != b;
    public static bool operator >(Angle a, Angle b) => a._value > b._value && a != b;
    public static bool operator <=(Angle a, Angle b) => a._value <= b._value;
    public static bool operator >=(Angle a, Angle b) => a._value >= b._value;

    private Angle(float radians) => _value = radians;
    private readonly float _value;

    /// <summary>
    /// For ~1% precision when dealing with seconds
    /// </summary>
    private const float _epsilonFactor = 1e-8f;
    private static float Epsilon(float a, float b) => MathF.Max(Math.Abs(a), Math.Abs(b)) * _epsilonFactor;
}

public readonly struct DirectionOffset
{
    public readonly Angle Pitch;
    public readonly Angle Yaw;

    public DirectionOffset(Vec3d direction, Vec3d reference)
    {
        float[] from = new[] { (float)reference.X, (float)reference.Y, (float)reference.Z };
        float[] to = new[] { (float)direction.X, (float)direction.Y, (float)direction.Z };

        float yawSin = (from[2] * to[0] - from[0] * to[2]) / MathF.Sqrt((from[0] * from[0] + from[2] * from[2]) * (to[0] * to[0] + to[2] * to[2]));
        float pitchSin = (from[2] * to[1] - from[1] * to[2]) / MathF.Sqrt((from[1] * from[1] + from[2] * from[2]) * (to[1] * to[1] + to[2] * to[2]));
        Yaw = Angle.FromRadians(MathF.Asin(yawSin));
        Pitch = Angle.FromRadians(MathF.Asin(pitchSin));
    }
    public DirectionOffset(Vec3f direction, Vec3f reference)
    {
        float yawSin = (reference.Z * direction.X - reference.X * direction.Z) / MathF.Sqrt((reference.X * reference.X + reference.Z * reference.Z) * (direction.X * direction.X + direction.Z * direction.Z));
        float pitchSin = (reference.Z * direction.Y - reference.Y * direction.Z) / MathF.Sqrt((reference.Y * reference.Y + reference.Z * reference.Z) * (direction.Y * direction.Y + direction.Z * direction.Z));
        Yaw = Angle.FromRadians(MathF.Asin(yawSin));
        Pitch = Angle.FromRadians(MathF.Asin(pitchSin));
    }
    public DirectionOffset(Angle pitch, Angle yaw)
    {
        Yaw = yaw;
        Pitch = pitch;
    }

    public override readonly string ToString() => $"Pitch: {Pitch}, Yaw: {Yaw}";
    public override bool Equals(object? obj) => ((DirectionOffset?)obj)?.Pitch == Pitch && ((DirectionOffset)obj).Yaw == Yaw;
    public override int GetHashCode() => (Pitch, Yaw).GetHashCode();

    public static DirectionOffset Zero => new(Angle.Zero, Angle.Zero);

    public static DirectionOffset FromRadians(float pitch, float yaw) => new(Angle.FromRadians(pitch), Angle.FromRadians(yaw));
    public static DirectionOffset FromDegrees(float pitch, float yaw) => new(Angle.FromDegrees(pitch), Angle.FromDegrees(yaw));
    public static DirectionOffset FromMinutes(float pitch, float yaw) => new(Angle.FromMinutes(pitch), Angle.FromMinutes(yaw));
    public static DirectionOffset FromSeconds(float pitch, float yaw) => new(Angle.FromSeconds(pitch), Angle.FromSeconds(yaw));

    public static DirectionOffset operator +(DirectionOffset a, DirectionOffset b) => new(a.Pitch + b.Pitch, a.Yaw + b.Yaw);
    public static DirectionOffset operator -(DirectionOffset a, DirectionOffset b) => new(a.Pitch - b.Pitch, a.Yaw - b.Yaw);
    public static DirectionOffset operator *(DirectionOffset a, float b) => new(a.Pitch * b, a.Yaw * b);
    public static DirectionOffset operator *(float a, DirectionOffset b) => new(a * b.Pitch, a * b.Yaw);
    public static DirectionOffset operator /(DirectionOffset a, float b) => new(a.Pitch / b, a.Yaw / b);

    public static bool operator ==(DirectionOffset a, DirectionOffset b) => a.Pitch == b.Pitch && a.Yaw == b.Yaw;
    public static bool operator !=(DirectionOffset a, DirectionOffset b) => !(a == b);
    public static bool operator <(DirectionOffset a, DirectionOffset b) => a.Pitch < b.Pitch && a.Yaw < b.Yaw;
    public static bool operator >(DirectionOffset a, DirectionOffset b) => a.Pitch > b.Pitch && a.Yaw > b.Yaw;
    public static bool operator <=(DirectionOffset a, DirectionOffset b) => !(a > b);
    public static bool operator >=(DirectionOffset a, DirectionOffset b) => !(a < b);
}

public class DispersionGenerator
{
    private readonly NatFloat _generator;

    public DispersionGenerator(Angle dispersion)
    {
        _generator = new(0, dispersion.Radians, EnumDistribution.GAUSSIAN);
    }

    public DirectionOffset Generate(float multiplier) => new(Angle.FromRadians(_generator.nextFloat(multiplier)), Angle.FromRadians(_generator.nextFloat(multiplier)));
}

public sealed class Aiming : BaseSystem
{
    public Aiming(ICoreAPI api, string debugName = "", string timerAttributeName = "aimingTime") : base(api, debugName)
    {
        _timeAttributeName = timerAttributeName;
    }

    public Angle FinalDispersion { get; set; } = Angle.Zero;
    public float InitialDispersionMultiplier { get; set; } = 1.0f;
    public StatsModifier? FinalDispersionModifier { get; set; } = null;
    public StatsModifier? InitialDispersionModifier { get; set; } = null;
    public StatsModifier? AimingTimeModifier { get; set; } = null;

    public const string AimingBehaviorAttribute = "aiming-noreticle";

    public void StartAiming(IPlayer player, TimeSpan aimingTime, Action<DirectionOffset>? readyCallback = null)
    {
        SetAimingStartTime(player);
        SetAimingTime(player, aimingTime);
        if (readyCallback != null) StartCallbackTimer(player, readyCallback);
        SetDispersions(player);
        player.Entity.Attributes.SetInt(AimingBehaviorAttribute, 1);
    }
    public void StopAiming(IPlayer player)
    {
        player.Entity.Attributes.SetInt(AimingBehaviorAttribute, 0);
        StopCallbackTimer(player);
        ResetAimingStartTime(player);
    }
    public DirectionOffset GenerateOffset(IPlayer player)
    {
        float progress = GetProgress(player);
        return GenerateOffset(player, progress);
    }


    private readonly string _timeAttributeName;
    private readonly Dictionary<long, float> _aimingTimes = new();
    private readonly Dictionary<long, DispersionGenerator> _dispersions = new();
    private readonly Dictionary<long, float> _minDispersions = new();
    private readonly Dictionary<long, float> _maxDispersions = new();
    private readonly Dictionary<long, Utils.DelayedCallback> _timers = new();

    private void ResetAimingStartTime(IPlayer player)
    {
        player.Entity.Attributes.RemoveAttribute(_timeAttributeName);
    }
    private void SetAimingStartTime(IPlayer player)
    {
        long currentTime = Api.World.ElapsedMilliseconds;
        player.Entity.Attributes.SetLong(_timeAttributeName, currentTime);
    }
    private TimeSpan GetAimingDuration(IPlayer player)
    {
        long milliseconds = player.Entity.Attributes.GetLong(_timeAttributeName, Api.World.ElapsedMilliseconds);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
    private void SetAimingTime(IPlayer player, TimeSpan aimingTime)
    {
        long entityId = player.Entity.EntityId;
        if (!_aimingTimes.ContainsKey(entityId)) _aimingTimes.Add(entityId, 0);
        if (AimingTimeModifier != null)
        {
            _aimingTimes[entityId] = AimingTimeModifier.Calc(player, (float)aimingTime.TotalSeconds);
        }
        else
        {
            _aimingTimes[entityId] = (float)aimingTime.TotalSeconds;
        }
    }
    public TimeSpan GetAimingTime(IPlayer player)
    {
        long entityId = player.Entity.EntityId;
        if (_aimingTimes.ContainsKey(entityId))
        {
            return TimeSpan.FromSeconds(_aimingTimes[entityId]);
        }

        return TimeSpan.Zero;
    }
    private void StartCallbackTimer(IPlayer player, Action<DirectionOffset> readyCallback)
    {
        long entityId = player.Entity.EntityId;
        if (_timers.ContainsKey(entityId)) _timers[entityId].Dispose();
        _timers[entityId] = new(Api, GetAimingDuration(player), () => readyCallback.Invoke(GenerateOffset(player)));
    }
    private void StopCallbackTimer(IPlayer player)
    {
        long entityId = player.Entity.EntityId;
        if (_timers.ContainsKey(entityId)) _timers[entityId].Cancel();
    }
    private void SetDispersions(IPlayer player)
    {
        long entityId = player.Entity.EntityId;

        if (!_minDispersions.ContainsKey(entityId)) _minDispersions.Add(entityId, 0);
        if (FinalDispersionModifier != null)
        {
            _minDispersions[entityId] = Angle.FromMinutes(FinalDispersionModifier.Calc(player, FinalDispersion.Minutes)) / FinalDispersion;
        }
        else
        {
            _minDispersions[entityId] = 1.0f;
        }

        if (!_maxDispersions.ContainsKey(entityId)) _maxDispersions.Add(entityId, 0);
        if (InitialDispersionModifier != null)
        {
            _maxDispersions[entityId] = Angle.FromMinutes(InitialDispersionModifier.Calc(player, FinalDispersion.Minutes * InitialDispersionMultiplier)) / FinalDispersion;
        }
        else
        {
            _maxDispersions[entityId] = InitialDispersionMultiplier;
        }

        _dispersions[entityId] = new(FinalDispersion);
    }
    private float GetProgress(IPlayer player)
    {
        TimeSpan duration = GetAimingDuration(player);
        TimeSpan time = GetAimingTime(player);
        return Math.Clamp((float)(duration / time), 0f, 1f);
    }
    private DirectionOffset GenerateOffset(IPlayer player, float progress)
    {
        long entityId = player.Entity.EntityId;
        float min = _minDispersions[entityId];
        float max = _maxDispersions[entityId];
        float multiplier = (max - min) * progress + min;

        return _dispersions[entityId].Generate(multiplier);
    }
}