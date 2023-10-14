using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using System;

namespace MaltiezFSM.Systems
{
    internal class BasicRequirements : BaseSystem
    {
        private struct OperationRequirement
        {
            public string code { get; set; }
            public string name { get; set; }
            public int amount { get; set; }
            public int durability { get; set; }
            public int durabilityDamage { get; set; }
            public int offHand { get; set; }
            public bool consume { get; set; }

            public static implicit operator OperationRequirement((string code, string name, int amount, int durability, int durabilityDamage, int offHand, bool consume) parameters)
            {
                return new OperationRequirement() { code = parameters.code, name = parameters.name, amount = parameters.amount, durability = parameters.durability, durabilityDamage = parameters.durabilityDamage, offHand = parameters.offHand, consume = parameters.consume };
            }

            public override string ToString()
            {
                string output = Lang.Get(name);
                if (amount > 1)
                {
                    output += " (" + Lang.Get("fsmlib:requirements-amount", amount) + ")";
                }

                if (durabilityDamage > 0)
                {
                    output += " (" + Lang.Get("fsmlib:requirements-durability", durabilityDamage) + ")";
                }
                else if (durability > 0)
                {
                    output += " (" + Lang.Get("fsmlib:requirements-durability", durability) + ")";
                }

                if (offHand >= 1)
                {
                    output += " (" + Lang.Get("fsmlib:requirements-offhand") + ")";
                }

                return output;
            }
        }

        private readonly Dictionary<string, JsonObject> mRequirements = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            foreach (JsonObject requrementPack in definition["requirementSets"].AsArray())
            {
                mRequirements.Add(requrementPack["code"].AsString(), requrementPack["requirements"]);
            }
        }
        public override bool Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Verify(slot, player, parameters)) return false;

            string code = parameters["code"].AsString();
            return Check(player, mRequirements[code]);
        }
        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            string code = parameters["code"].AsString();
            string action = parameters["type"].AsString();
            switch (action)
            {
                case "check":
                    return Check(player, mRequirements[code]);
                case "take":
                    return Consume(player, mRequirements[code]);
                default:
                    mApi.Logger.Error("[FSMlib] [BasicRequirements] [Process] Type does not exists: " + action);
                    return false;
            }
        }

        private bool Check(EntityAgent byEntity, JsonObject requirements)
        {
            List<OperationRequirement> requirementsList = GetRequirements(requirements);
            bool requirementsFulfilled = true;
            foreach (OperationRequirement requirement in requirementsList)
            {
                if ((GetNextRequirement(byEntity, requirement) == null) != (requirement.offHand == 0))
                {
                    ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("fsmlib:requirements-missing", requirement.ToString()), EnumChatType.Notification);
                    requirementsFulfilled = false;
                }
            }

            return requirementsFulfilled;
        }

        private bool Consume(EntityAgent byEntity, JsonObject requirements)
        {
            List<OperationRequirement> requirementsList = GetRequirements(requirements);
            foreach (OperationRequirement requirement in requirementsList)
            {
                if ((GetNextRequirement(byEntity, requirement) == null) != (requirement.offHand == 0)) return false;
            }

            foreach (OperationRequirement requirement in requirementsList)
            {
                ItemSlot ammoSlot = GetNextRequirement(byEntity, requirement);

                if (requirement.durability != 0)
                {
                    if (ChangeDurability(ammoSlot.Itemstack, -requirement.durability))
                    {
                        ammoSlot.MarkDirty();
                    }
                }
                else if (requirement.durabilityDamage > 0)
                {
                    ammoSlot?.Itemstack.Item.DamageItem(byEntity.World, byEntity, ammoSlot, requirement.durabilityDamage);
                    ammoSlot?.MarkDirty();
                }
                else if (requirement.consume)
                {
                    ammoSlot?.TakeOut(requirement.amount);
                    ammoSlot?.MarkDirty();
                }
            }

            return true;
        }

        private ItemSlot GetNextRequirement(EntityAgent byEntity, OperationRequirement requirement)
        {
            ItemSlot slot = null;

            if (requirement.offHand >= 0)
            {
                ItemStack stack = byEntity.LeftHandItemSlot.Itemstack;

                if (stack == null) return null;
                if (!stack.Collectible.Code.Path.StartsWith(requirement.code)) return null;
                if (byEntity.RightHandItemSlot.StackSize < requirement.amount) return null;
                if (requirement.durabilityDamage > 0 && stack.Item.GetRemainingDurability(stack) < requirement.durabilityDamage) return null;
                if (requirement.durability > 0 && stack.Item.GetRemainingDurability(stack) < requirement.durability) return null;
                if (requirement.durability < 0 && stack.Item.GetRemainingDurability(stack) - stack.Item.GetMaxDurability(stack) > requirement.durability) return null;

                return byEntity.LeftHandItemSlot;
            }

            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                if (invslot.Itemstack != null && invslot.Itemstack.Collectible.Code.Path.StartsWith(requirement.code) && invslot.Itemstack.StackSize >= requirement.amount)
                {
                    if (requirement.durabilityDamage > 0 && invslot.Itemstack.Item.GetRemainingDurability(invslot.Itemstack) < requirement.durabilityDamage)
                    {
                        return true;
                    }

                    if (requirement.durability > 0 && invslot.Itemstack.Item.GetRemainingDurability(invslot.Itemstack) < requirement.durability)
                    {
                        return true;
                    }

                    if (requirement.durability < 0 && invslot.Itemstack.Item.GetRemainingDurability(invslot.Itemstack) - invslot.Itemstack.Item.GetMaxDurability(invslot.Itemstack) > requirement.durability)
                    {
                        return true;
                    }

                    slot = invslot;
                    return false;
                }

                return true;
            });

            return slot;
        }

        private List<OperationRequirement> GetRequirements(JsonObject requirements)
        {
            List<OperationRequirement> output = new List<OperationRequirement>();

            foreach (JsonObject requirement in requirements.AsArray())
            {
                string code = requirement["code"].AsString();
                string name = requirement["name"].AsString(code);
                int amount = requirement["amount"].AsInt(1);
                int durability = requirement["durability"].AsInt(0);
                int durabilityDamage = requirement["durabilityDamage"].AsInt(-1);
                int offHand = requirement["offhand"].AsInt(-1);
                bool consume = requirement["consume"].AsBool(true);

                output.Add((code, name, amount, durability, durabilityDamage, offHand, consume));
            }

            return output;
        }

        private bool ChangeDurability(ItemStack itemstack, int amount)
        {
            if (amount >= 0 && itemstack.Collectible.GetRemainingDurability(itemstack) >= itemstack.Collectible.GetMaxDurability(itemstack))
            {
                return false;
            }

            int remainingDurability = itemstack.Collectible.GetRemainingDurability(itemstack) + amount;
            remainingDurability = Math.Min(itemstack.Collectible.GetMaxDurability(itemstack), remainingDurability);

            if (remainingDurability < 0)
            {
                return false;
            }

            itemstack.Attributes.SetInt("durability", Math.Max(remainingDurability, 0));

            return true;
        }
    }
}
