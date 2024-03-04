using MaltiezFSM.API;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using VSImGui;
using ImGuiNET;
using System.Linq;
using System.Diagnostics;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IOperation;
using Vintagestory.Client;
using Vintagestory.Server;

namespace MaltiezFSM.Framework;

internal sealed class ImGuiDebugWindow : IDisposable
{
    public static ImGuiDebugWindow? Instance { get; private set; }
    public static void Init(ICoreClientAPI api) => Instance ??= new(api);
    public static event Action? DrawWindows;
    public static void DisposeInstance() => Instance?.Dispose();

    private ICoreClientAPI mApi;

    private ImGuiDebugWindow(ICoreClientAPI api)
    {
        mApi = api;
#if DEBUG        
        //api.ModLoader.GetModSystem<ImGuiModSystem>().Draw += Draw;
        InputManagerDebugWindow.Init();
        OperationsDebugWindow.Init();
        SystemsDebugWindow.Init();
    }

    public static void DrawHint(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.BeginItemTooltip())
        {
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();

            ImGui.EndTooltip();
        }
    }

    private void Draw()
    {
        ImGui.Begin("FSM lib - debug window");

        if (ImGui.CollapsingHeader("Sound effects##FSMlib"))
        {
            mApi.ModLoader.GetModSystem<FiniteStateMachineSystem>().SoundEffects?.Draw($"SoundEffectsFSMlib");
        }
        if (ImGui.CollapsingHeader("Particle effects##FSMlib"))
        {
            mApi.ModLoader.GetModSystem<FiniteStateMachineSystem>().ParticleEffects?.Draw($"ParticleEffectsFSMlib");
        }
        if (ImGui.CollapsingHeader("Input manager##FSMlib"))
        {
            InputManagerDebugWindow.Draw();
        }
        if (ImGui.CollapsingHeader("Operations##FSMlib"))
        {
            OperationsDebugWindow.Draw();
        }
        if (ImGui.CollapsingHeader("Systems##FSMlib"))
        {
            SystemsDebugWindow.Draw();
        }

        DrawWindows?.Invoke();

        ImGui.End();
#endif
    }

    public void Dispose()
    {
        InputManagerDebugWindow.Clear();
        OperationsDebugWindow.Clear();
    }
}

internal struct InputData
{
    public SlotData Slot { get; set; }
    public IPlayer Player { get; set; }
    public IInput Input { get; set; }
    public bool Handled { get; set; }
    public bool ClientSide { get; set; }
    public int Index { get; set; }
    public string TimeStamp { get; set; } = "";
    public bool FromPacket { get; set; }

    public InputData(SlotData slot, IPlayer player, IInput input, bool handled, bool clientSide, bool fromPacket)
    {
        Slot = slot;
        Player = player;
        Input = input;
        Handled = handled;
        ClientSide = clientSide;
        FromPacket = fromPacket;
    }
}

internal sealed class InputManagerDebugWindowImpl : IDisposable
{
    private readonly FixedSizedQueue<InputData> mServerInputDataQueue = new(64);
    private readonly FixedSizedQueue<InputData> mClientInputDataQueue = new(64);
    private readonly FixedSizedQueue<float> mServerPackets = new(120);
    private readonly FixedSizedQueue<float> mClientPackets = new(120);
    private int mServerCounter = 0;
    private int mClientCounter = 0;
    private bool mStopUpdates = false;
    private bool mOnlyHandled = false;
    private bool mShowPackets = false;
    private readonly Type[] mInputs = new Type[]
    {
        typeof(IInput),
        typeof(Inputs.MouseKey),
        typeof(Inputs.KeyboardKey),
        typeof(Inputs.Custom),
        typeof(Inputs.HotkeyInput),
        typeof(Inputs.OperationInput),
        typeof(Inputs.AfterSlotChanged),
        typeof(Inputs.BeforeSlotChanged),
        typeof(Inputs.StatusInput)
    };
    private int mCurrentFilter = 0;

    public void Draw()
    {
#if DEBUG   
        ImGui.BeginChild("InputManager##FSMlib", new System.Numerics.Vector2(0, 800), false);

        try
        {
            if (ImGui.Button($"Clear##InputManager##FSMlib"))
            {
                mServerInputDataQueue.Queue.Clear();
                mClientInputDataQueue.Queue.Clear();
                mServerPackets.Queue.Clear();
                mClientPackets.Queue.Clear();
                mServerCounter = 0;
                mClientCounter = 0;
                mCurrentFilter = 0;
            }

            ImGui.Checkbox("Stop updates", ref mStopUpdates);
            ImGui.Checkbox("Show synchronized", ref mShowPackets);
            ImGui.Checkbox("Show only handled inputs", ref mOnlyHandled);
            ImGui.Combo("Filter##InputManager", ref mCurrentFilter, mInputs.Select(Utils.GetTypeName).ToArray(), mInputs.Length);

            if (mClientPackets.Count > 0) ImGui.PlotLines("Client packets per tick##FSMlib", ref mClientPackets.Queue.ToArray()[0], mClientPackets.Count, 0, $"Max inputs per second: {mClientPackets.Queue.Max()}", 0, mClientPackets.Queue.Max(), new System.Numerics.Vector2(0, 100));
            if (mServerPackets.Count > 0) ImGui.PlotLines("Server packets per tick##FSMlib", ref mServerPackets.Queue.ToArray()[0], mServerPackets.Count, 0, $"Max inputs per second: {mServerPackets.Queue.Max()}", 0, mServerPackets.Queue.Max(), new System.Numerics.Vector2(0, 100));

            ImGui.SeparatorText("Client side inputs");
            ImGui.BeginChild("InputManager - client", new System.Numerics.Vector2(0, mClientInputDataQueue.Count > 0 ? 200 : 20), true);
            foreach (InputData element in mClientInputDataQueue.Queue.Reverse().Where(ShowInput))
            {
                DrawElement(element);
            }
            ImGui.EndChild();

            ImGui.SeparatorText("Server side inputs");
            ImGui.BeginChild("InputManager - server", new System.Numerics.Vector2(0, mServerInputDataQueue.Count > 0 ? 200 : 20), true);
            foreach (InputData element in mServerInputDataQueue.Queue.Reverse().AsEnumerable().Where(ShowInput))
            {
                DrawElement(element);
            }
            ImGui.EndChild();
        }
        catch
        {
            // does not matter
        }

        ImGui.EndChild();
    }

    private bool ShowInput(InputData element)
    {
        return (element.Input.GetType().IsAssignableFrom(mInputs[mCurrentFilter]) || mCurrentFilter == 0) && (!mOnlyHandled || element.Handled) && (mShowPackets || !element.FromPacket);
    }

    private static void DrawElement(InputData element)
    {
        if (!element.Handled) ImGui.BeginDisabled();
        ImGui.Text(element.TimeStamp);
        ImGui.SameLine(100);
        ImGui.Text($"#{element.Index}");
        ImGui.SameLine(200);
        ImGui.Text($"{element.Input}");
        if (!element.Handled) ImGui.EndDisabled();
        ImGuiDebugWindow.DrawHint($"Slot: {element.Slot}\nPlayer: {element.Player.PlayerName}");
#endif
    }

    public void Enqueue(InputData element)
    {
        if (!mStopUpdates && element.ClientSide)
        {
            element.Index = mClientCounter;
            element.TimeStamp = $"{DateTime.Now:HH:mm:ss.fff}";
            mClientInputDataQueue.Enqueue(element);
            mClientCounter++;
        }
        if (!mStopUpdates && !element.ClientSide)
        {
            element.Index = mServerCounter;
            element.TimeStamp = $"{DateTime.Now:HH:mm:ss.fff}";
            mServerInputDataQueue.Enqueue(element);
            mServerCounter++;
        }
    }

    private int mClientAggregator = 0;
    private int mServerAggregator = 0;
    private int mClientAggregatorCount = 0;
    private int mServerAggregatorCount = 0;
    private readonly Stopwatch mLastFlush = Stopwatch.StartNew();
    private readonly TimeSpan mAggregationPeriod = TimeSpan.FromSeconds(0.5);
    public void EnqueuePackets(int amount, bool clientSide)
    {
        if (clientSide)
        {
            mClientAggregator += amount;
            mClientAggregatorCount++;
        }
        else
        {
            mServerAggregator += amount;
            mServerAggregatorCount++;
        }

        if (mLastFlush.Elapsed > mAggregationPeriod)
        {
            mLastFlush.Restart();
            mClientPackets.Enqueue(mClientAggregatorCount == 0 ? 0 : mClientAggregator / (float)mAggregationPeriod.TotalSeconds);
            mServerPackets.Enqueue(mServerAggregatorCount == 0 ? 0 : mServerAggregator / (float)mAggregationPeriod.TotalSeconds);
            mClientAggregator = 0;
            mServerAggregator = 0;
            mClientAggregatorCount = 0;
            mServerAggregatorCount = 0;
        }
    }

    public void Dispose()
    {
        mClientInputDataQueue.Queue.Clear();
    }
}

internal static class InputManagerDebugWindow
{
    public static void Enqueue(SlotData slot, IPlayer player, IInput input, bool handled, bool clientSide, bool packet) => mInstance?.Enqueue(new(slot, player, input, handled, clientSide, packet));
    public static void EnqueuePacket(int amount, bool clientSide) => mInstance?.EnqueuePackets(amount, clientSide);
    public static void Clear() => mInstance?.Dispose();
    public static void Draw() => mInstance?.Draw();
    public static void Init() => mInstance ??= new();

    private static InputManagerDebugWindowImpl? mInstance;
}

internal struct OperationData
{
    public IOperation Operation { get; set; }
    public IPlayer Player { get; set; }
    public IInput Input { get; set; }
    public IState From { get; set; }
    public IState To { get; set; }
    public IOperation.Outcome Outcome { get; set; }
    public IOperation.Result? Result { get; set; }
    public bool ClientSide { get; set; }
    public int Index { get; set; }
    public string TimeStamp { get; set; }
}

internal sealed class OperationsDebugWindowImpl : IDisposable
{
    private readonly FixedSizedQueue<OperationData> mServerOperationDataQueue = new(64);
    private readonly FixedSizedQueue<OperationData> mClientOperationDataQueue = new(64);
    private int mServerCounter = 0;
    private int mClientCounter = 0;
    private bool mStopUpdates = false;
    private bool mOnlyHandled = false;
    private readonly Type[] mOperations = new Type[]
    {
        typeof(IOperation),
        typeof(Operations.Instant),
        typeof(Operations.Delayed)
    };
    private int mCurrentFilter = 0;
    private readonly IOperation.Outcome[] mOutcomes = new IOperation.Outcome[]
    {
        IOperation.Outcome.None,
        IOperation.Outcome.Started,
        IOperation.Outcome.Failed,
        IOperation.Outcome.Finished,
        IOperation.Outcome.StartedAndFinished
    };
    private int mOutcomeFilter = 0;

    public void Draw()
    {
#if DEBUG   
        ImGui.BeginChild("Operations##FSMlib", new System.Numerics.Vector2(0, 800), false);
        try
        {
            if (ImGui.Button($"Clear##Operations##FSMlib"))
            {
                mServerOperationDataQueue.Queue.Clear();
                mClientOperationDataQueue.Queue.Clear();
                mServerCounter = 0;
                mClientCounter = 0;
                mCurrentFilter = 0;
                mOutcomeFilter = 0;
            }

            ImGui.Checkbox("Stop updates", ref mStopUpdates);
            ImGui.Checkbox("Hide failed", ref mOnlyHandled);
            ImGui.Combo("Operation filter##InputManager", ref mCurrentFilter, mOperations.Select(Utils.GetTypeName).ToArray(), mOperations.Length);
            ImGui.Combo("Outcome filter##InputManager", ref mOutcomeFilter, mOutcomes.Select((element) => element.ToString() ?? "").ToArray(), mOutcomes.Length);

            ImGui.SeparatorText("Client side operations");
            ImGui.BeginChild("Operations - client##FSMlib", new System.Numerics.Vector2(0, mClientOperationDataQueue.Count > 0 ? 200 : 20), true);
            try
            {
                foreach (OperationData element in mClientOperationDataQueue.Queue.Reverse().Where(ShowOperation))
                {
                    DrawElement(element);
                }
            }
            catch
            {
                // just move on
            }
            ImGui.EndChild();

            ImGui.SeparatorText("Server side operations");
            ImGui.BeginChild("Operations - server##FSMlib", new System.Numerics.Vector2(0, mServerOperationDataQueue.Count > 0 ? 200 : 20), true);
            try
            {
                foreach (OperationData element in mServerOperationDataQueue.Queue.Reverse().Where(ShowOperation))
                {
                    DrawElement(element);
                }
            }
            catch
            {
                // just move on
            }
            
            ImGui.EndChild();
        }
        catch
        {
            // just move on
        }

        ImGui.EndChild();
    }

    private bool ShowOperation(OperationData element)
    {
        return (element.Input.GetType().IsAssignableFrom(mOperations[mCurrentFilter]) || mCurrentFilter == 0) && (mOutcomeFilter == 0 || element.Outcome == mOutcomes[mOutcomeFilter]) && (!mOnlyHandled || element.Outcome != IOperation.Outcome.Failed);
    }

    private static void DrawElement(OperationData element)
    {
        if (element.Outcome == IOperation.Outcome.Failed) ImGui.BeginDisabled();
        ImGui.Text(element.TimeStamp);
        ImGui.SameLine(100);
        ImGui.Text($"#{element.Index}");
        ImGui.SameLine(150);
        ImGui.Text($"#{element.To}");
        ImGui.SameLine(300);
        ImGui.Text($"{element.Operation}");
        if (element.Outcome == IOperation.Outcome.Failed) ImGui.EndDisabled();
        ImGuiDebugWindow.DrawHint(
            $"Outcome: {element.Outcome}\n" +
            $"Timeout: {element.Result?.Timeout}\n" +
            $"From: {element.From}\n" +
            $"To: {element.To}\n" +
            $"Input: {element.Input}\n" +
            $"Player: {element.Player.PlayerName}\n");
#endif
    }

    public void Enqueue(OperationData element)
    {
        if (!mStopUpdates && element.ClientSide)
        {
            element.Index = mClientCounter;
            element.TimeStamp = $"{DateTime.Now:HH:mm:ss.fff}";
            mClientOperationDataQueue.Enqueue(element);
            mClientCounter++;
        }
        if (!mStopUpdates && !element.ClientSide)
        {
            element.Index = mServerCounter;
            element.TimeStamp = $"{DateTime.Now:HH:mm:ss.fff}";
            mServerOperationDataQueue.Enqueue(element);
            mServerCounter++;
        }
    }

    public void Dispose()
    {
        mClientOperationDataQueue.Queue.Clear();
    }
}

internal static class OperationsDebugWindow
{
    public static void Enqueue(IOperation operation, IPlayer player, IInput input, IState from, IState to, IOperation.Outcome outcome, bool clientSide, IOperation.Result? result = null)
    {
        mInstance?.Enqueue(new()
        {
            Operation = operation,
            Player = player,
            Input = input,
            From = from,
            To = to,
            Outcome = outcome,
            ClientSide = clientSide,
            Result = result
        }
        );
    }
    public static void Clear() => mInstance?.Dispose();
    public static void Draw() => mInstance?.Draw();
    public static void Init() => mInstance ??= new();

    private static OperationsDebugWindowImpl? mInstance;
}

internal struct SystemData
{
    public ISystem System { get; set; }
    public IOperation Operation { get; set; }
    public IPlayer Player { get; set; }
    public JsonObject Request { get; set; }
    public bool Result { get; set; }
    public bool ClientSide { get; set; }
    public int Index { get; set; }
    public string TimeStamp { get; set; }
}

internal sealed class SystemsDebugWindowImpl : IDisposable
{
    private readonly FixedSizedQueue<SystemData> mServerSystemsDataQueue = new(64);
    private readonly FixedSizedQueue<SystemData> mClientSystemsDataQueue = new(64);
    private int mServerCounter = 0;
    private int mClientCounter = 0;
    private bool mStopUpdates = false;
    private bool mOnlyFailed = false;

    public void Draw()
    {
#if DEBUG   
        ImGui.BeginChild("Systems##FSMlib", new System.Numerics.Vector2(0, 800), false);
        try
        {
            if (ImGui.Button($"Clear##Systems##FSMlib"))
            {
                mServerSystemsDataQueue.Queue.Clear();
                mClientSystemsDataQueue.Queue.Clear();
                mServerCounter = 0;
                mClientCounter = 0;
            }

            ImGui.Checkbox("Stop updates", ref mStopUpdates);
            ImGui.Checkbox("Hide successful", ref mOnlyFailed);

            ImGui.SeparatorText("Client side systems");
            ImGui.BeginChild("Systems - client##FSMlib", new System.Numerics.Vector2(0, mClientSystemsDataQueue.Count > 0 ? 200 : 20), true);
            try
            {
                foreach (SystemData element in mClientSystemsDataQueue.Queue.Reverse().Where(ShowSystem))
                {
                    DrawElement(element);
                }
            }
            catch
            {
                // just move on
            }
            ImGui.EndChild();

            ImGui.SeparatorText("Server side systems");
            ImGui.BeginChild("Systems - server##FSMlib", new System.Numerics.Vector2(0, mServerSystemsDataQueue.Count > 0 ? 200 : 20), true);
            try
            {
                foreach (SystemData element in mServerSystemsDataQueue.Queue.Reverse().Where(ShowSystem))
                {
                    DrawElement(element);
                }
            }
            catch
            {
                // just move on
            }

            ImGui.EndChild();
        }
        catch
        {
            // just move on
        }

        ImGui.EndChild();
    }

    private bool ShowSystem(SystemData element)
    {
        return !mOnlyFailed || !element.Result;
    }

    private static void DrawElement(SystemData element)
    {
        if (!element.Result) ImGui.BeginDisabled();
        ImGui.Text(element.TimeStamp);
        ImGui.SameLine(100);
        ImGui.Text($"#{element.Index}");
        ImGui.SameLine(150);
        ImGui.Text($"{element.System}");
        if (!element.Result) ImGui.EndDisabled();
        ImGuiDebugWindow.DrawHint(
            $"Operation: {element.Operation}\n" +
            $"Player: {element.Player.PlayerName}\n" +
            $"Request: {element.Request}\n");
#endif
    }

    public void Enqueue(SystemData element)
    {
        if (!mStopUpdates && element.ClientSide)
        {
            element.Index = mClientCounter;
            element.TimeStamp = $"{DateTime.Now:HH:mm:ss.fff}";
            mClientSystemsDataQueue.Enqueue(element);
            mClientCounter++;
        }
        if (!mStopUpdates && !element.ClientSide)
        {
            element.Index = mServerCounter;
            element.TimeStamp = $"{DateTime.Now:HH:mm:ss.fff}";
            mServerSystemsDataQueue.Enqueue(element);
            mServerCounter++;
        }
    }

    public void Dispose()
    {
        mClientSystemsDataQueue.Queue.Clear();
    }
}

internal static class SystemsDebugWindow
{
    public static void Enqueue(ISystem system, IOperation operation, IPlayer player, JsonObject request, bool result, bool clientSide)
    {
        mInstance?.Enqueue(new()
        {
            System = system,
            Operation = operation,
            Player = player,
            Request = request,
            Result = result,
            ClientSide = clientSide
        }
        );
    }
    public static void Clear() => mInstance?.Dispose();
    public static void Draw() => mInstance?.Draw();
    public static void Init() => mInstance ??= new();

    private static SystemsDebugWindowImpl? mInstance;
}