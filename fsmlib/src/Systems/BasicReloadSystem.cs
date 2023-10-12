﻿using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace MaltiezFSM.Systems
{  
    internal class BasicReload : UniqueIdFactoryObject, ISystem, IAmmoSelector
    {
        public const string ammoCodeAttrName = "ammoCode";
        public const string actionAttrName = "action";
        public const string amountAttrName = "amount";
        public const string takeAction = "take";
        public const string putAction = "put";
        public const string removeAction = "remove";
        public const string offHandAttrName = "offHand";

        public string AmmoStackAttrName = "FSMlib.stack.";

        private string mCode;
        private ICoreAPI mApi;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mCode = code;
            mApi = api;
        }
        public void SetSystems(Dictionary<string, ISystem> systems)
        {
            AmmoStackAttrName += mCode;
        }
        public bool Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            bool offHand = parameters[offHandAttrName].AsBool(false);
            string action = parameters[actionAttrName].AsString();
            if (action == putAction) return ReadAmmoStackFrom(slot)?.Item != null || ReadAmmoStackFrom(slot)?.Block != null;
            int amount = 1;
            if (parameters.KeyExists(amountAttrName)) amount = parameters[amountAttrName].AsInt(1);

            string ammoCode = parameters[ammoCodeAttrName].AsString();
            ItemSlot ammoSlot = GetAmmoSlot(player, ammoCode, offHand);
            bool verified = ammoSlot != null && ammoSlot != slot && ammoSlot.Itemstack?.StackSize >= amount;
            if (!verified)
            {
                ((player as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, "[FSMlib] Cant operate, unfulfilled requirements: " + ammoCode, EnumChatType.Notification);
            }
            return verified;
        }
        public bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            bool offHand = parameters[offHandAttrName].AsBool(false);
            string action = parameters[actionAttrName].AsString();

            switch (action)
            {
                case putAction:
                    if (ReadAmmoStackFrom(slot)?.Item == null && ReadAmmoStackFrom(slot)?.Block == null) return false;
                    PutAmmoBack(slot, player, offHand);
                    return true;
                case takeAction:
                    int amount = 1;
                    if (parameters.KeyExists(amountAttrName)) amount = parameters[amountAttrName].AsInt(1);

                    string ammoCode = parameters[ammoCodeAttrName].AsString();
                    ItemSlot ammoSlot = GetAmmoSlot(player, ammoCode, offHand);
                    if (ammoSlot == null || ammoSlot == slot) return false;

                    WriteAmmoStackTo(slot, ammoSlot.TakeOut(amount));
                    ammoSlot.MarkDirty();
                    return true;
                case removeAction:
                    TakeAmmoStackFrom(slot);
                    break;
            }

            return false;
        }
        public ItemStack GetSelectedAmmo(ItemSlot slot)
        {
            return ReadAmmoStackFrom(slot);
        }
        public ItemStack TakeSelectedAmmo(ItemSlot slot, int amount = -1)
        {
            return TakeAmmoStackFrom(slot, amount);
        }

        private ItemSlot GetAmmoSlot(EntityAgent player, string ammoCode, bool offHand)
        {
            if (offHand)
            {
                ItemSlot offHandSlot = player?.LeftHandItemSlot;
                if (offHandSlot?.Itemstack?.Collectible?.Code?.Path?.StartsWith(ammoCode) == true)
                {
                    return offHandSlot;
                }

                return null;
            }
            
            ItemSlot slot = null;

            player?.WalkInventory((inventorySlot) =>
            {
                if (inventorySlot is ItemSlotCreative) return true;

                if (inventorySlot?.Itemstack?.Collectible?.Code?.Path?.StartsWith(ammoCode) == true)
                {
                    slot = inventorySlot;
                    return false;
                }

                return true;
            });

            return slot;
        }
        private void PutAmmoBack(ItemSlot slot, EntityAgent player, bool offHand)
        {
            ItemStack ammoStack = ReadAmmoStackFrom(slot);
            int? amount = ammoStack?.StackSize;

            if (offHand && amount != null)
            {
                DummySlot dummySlot = new DummySlot(ammoStack);
                if (amount == dummySlot.TryPutInto(mApi.World, player.LeftHandItemSlot, ammoStack.StackSize)) return;
                ammoStack = dummySlot.TakeOutWhole();
            }

            if (ammoStack?.Item != null || ammoStack?.Block != null) player.TryGiveItemStack(ammoStack);

            slot?.Itemstack?.Attributes?.RemoveAttribute(AmmoStackAttrName);
            slot?.MarkDirty();
        }

        private void WriteAmmoStackTo(ItemSlot slot, ItemStack ammoStack)
        {
            slot.Itemstack.Attributes.SetItemstack(AmmoStackAttrName, ammoStack);
            slot.MarkDirty();
        }
        private ItemStack ReadAmmoStackFrom(ItemSlot slot)
        {
            ItemStack stack = slot.Itemstack.Attributes.GetItemstack(AmmoStackAttrName, null);
            stack?.ResolveBlockOrItem(mApi.World);
            WriteAmmoStackTo(slot, stack);
            return stack;
        }
        private ItemStack TakeAmmoStackFrom(ItemSlot slot, int amount = -1)
        {
            ItemStack ammoStack = ReadAmmoStackFrom(slot);
            if (amount == -1 || ammoStack.StackSize == amount || ammoStack.StackSize == 0)
            {
                slot?.Itemstack?.Attributes?.RemoveAttribute(AmmoStackAttrName);
                slot?.MarkDirty();
                return ammoStack;
            }

            if (ammoStack.StackSize < amount) return null;

            ItemStack takenAmmoStack = ammoStack.Clone();
            takenAmmoStack.ResolveBlockOrItem(mApi.World);
            takenAmmoStack.StackSize = amount;
            ammoStack.StackSize -= amount;

            WriteAmmoStackTo(slot, ammoStack);
            return takenAmmoStack;
        }
    }
}
