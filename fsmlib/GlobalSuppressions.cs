// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "All the disposable classes here can be disposed multiple times and there no way to know if it was already disposed when disposing", Scope = "member", Target = "~M:MaltiezFSM.Framework.FiniteStateMachine.Dispose")]
[assembly: SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "Same", Scope = "member", Target = "~M:MaltiezFSM.Framework.InputManager.Dispose")]
[assembly: SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "<Pending>", Scope = "member", Target = "~M:MaltiezFSM.Framework.CustomInputInvoker.Invoke(System.String,Vintagestory.API.Common.IPlayer,Vintagestory.API.Common.ItemSlot)~System.Boolean")]
[assembly: SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "<Pending>", Scope = "member", Target = "~M:MaltiezFSM.Framework.Utils.Field`2.#ctor(System.Type,System.String,`1)")]
[assembly: SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "<Pending>", Scope = "member", Target = "~M:MaltiezFSM.Systems.RequirementsApi.Requirement.Search(MaltiezFSM.Framework.SlotType,Vintagestory.API.Common.IPlayer,System.Func{Vintagestory.API.Common.ItemSlot,System.Boolean},System.Func{Vintagestory.API.Common.ItemSlot,System.Boolean})~System.Boolean")]
[assembly: SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "<Pending>", Scope = "member", Target = "~M:MaltiezFSM.Systems.AdvancedEntityProjectile.TryDamage(Vintagestory.API.Common.Entities.Entity)~System.Boolean")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>", Scope = "member", Target = "~M:MaltiezFSM.API.FactoryProduct.#ctor(System.Int32,System.String,Vintagestory.API.Datastructures.JsonObject,Vintagestory.API.Common.CollectibleObject,Vintagestory.API.Common.ICoreAPI)")]
[assembly: SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "<Pending>", Scope = "member", Target = "~M:MaltiezFSM.FiniteStateMachineSystem.TriggerAfterActiveSlotChanged(Vintagestory.Server.ServerEventManager,Vintagestory.API.Server.IServerPlayer,System.Int32,System.Int32)")]
[assembly: SuppressMessage("Major Code Smell", "S3264:Events should be invoked", Justification = "<Pending>", Scope = "member", Target = "~E:MaltiezFSM.Framework.FiniteStateMachineBehaviour`1.OnGetToolModes")]
[assembly: SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "<Pending>", Scope = "member", Target = "~M:MaltiezFSM.Framework.ActionInputInvoker.OnEntityAction(Vintagestory.API.Common.EnumEntityAction,System.Boolean,Vintagestory.API.Common.EnumHandling@)")]
[assembly: SuppressMessage("Critical Code Smell", "S2223:Non-constant static fields should not be visible", Justification = "<Pending>", Scope = "member", Target = "~F:MaltiezFSM.Framework.FiniteStateMachineBehaviour.TotalInitializingTime")]
