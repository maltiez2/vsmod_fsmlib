using MaltiezFSM.API;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable enable

namespace MaltiezFSM.Framework
{

    public sealed class DropItemsInputInvoker : IInputInvoker
    {
        private readonly ICoreClientAPI mClientApi;
        private readonly Dictionary<IItemDropped, IInputInvoker.InputCallback> mCallbacks = new();
        private readonly Dictionary<IItemDropped, CollectibleObject> mCollectibles = new();
        private bool mDispose = false;

        private readonly List<string> mHotkeys = new()
        {
            "dropitem",
            "dropitems"
        };

        public DropItemsInputInvoker(ICoreClientAPI api)
        {
            mClientApi = api;
            mClientApi.Event.KeyDown += KeyPressListener;
        }

        public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
        {
            if (input is IItemDropped slotInput)
            {
                mCallbacks.Add(slotInput, callback);
                mCollectibles.Add(slotInput, collectible);
            }
        }

        private void KeyPressListener(KeyEvent ev)
        {
            foreach (string hotkeyId in mHotkeys)
            {
                if (!mClientApi.Input.HotKeys.ContainsKey(hotkeyId))
                {
                    mClientApi.Logger.Error("[FSMlib] [ActiveSlotActiveListener] [KeyPressListener()] Hotkey '" + hotkeyId + "' not found");
                }

                if (mClientApi.Input.HotKeys.ContainsKey(hotkeyId) && CompareCombinations(ev, mClientApi.Input.HotKeys[hotkeyId].CurrentMapping))
                {
                    HotkeyPressHandler(ev);
                    break;
                }
            }
        }

        private bool CompareCombinations(KeyEvent A, KeyCombination B)
        {
            if (A.KeyCode != B.KeyCode) return false;
            if (A.ShiftPressed != B.Shift) return false;
            if (A.CtrlPressed != B.Ctrl) return false;
            if (A.AltPressed != B.Alt) return false;

            return true;
        }

        private void HotkeyPressHandler(KeyEvent ev)
        {
            bool handled = false;

            foreach ((IItemDropped input, _) in mCallbacks)
            {
                handled = HandleInput(input);
            }

            if (handled) ev.Handled = true;
        }

        private bool HandleInput(IItemDropped input)
        {
            Utils.SlotType slotType = input.SlotType();

            IEnumerable<Utils.SlotData> slots = Utils.SlotData.GetForAllSlots(slotType, mCollectibles[input], mClientApi.World.Player);

            bool handled = false;
            foreach (Utils.SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, mClientApi.World.Player, input))) // Unreadable but now warning... I guess win win?
            {
                handled = true;
            }

            return handled;
        }

        public void Dispose()
        {
            if (mDispose) return;
            mDispose = true;
            mClientApi.Event.KeyDown -= KeyPressListener;
        }
    }
}
