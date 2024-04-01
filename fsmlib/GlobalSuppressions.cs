// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "All the disposable classes here can be disposed multiple times and there no way to know if it was already disposed when disposing", Scope = "member", Target = "~M:MaltiezFSM.Framework.FiniteStateMachine.Dispose")]
[assembly: SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "Same", Scope = "member", Target = "~M:MaltiezFSM.Framework.InputManager.Dispose")]
[assembly: SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "<Pending>", Scope = "member", Target = "~M:MaltiezFSM.Framework.Utils.Field`2.#ctor(System.Type,System.String,`1)")]
[assembly: SuppressMessage("Critical Code Smell", "S2223:Non-constant static fields should not be visible", Justification = "<Pending>", Scope = "member", Target = "~F:MaltiezFSM.Framework.FiniteStateMachineBehaviour.TotalInitializingTime")]
[assembly: SuppressMessage("Major Code Smell", "S3246:Generic type parameters should be co/contravariant when possible", Justification = "<Pending>", Scope = "type", Target = "~T:MaltiezFSM.API.IFactory`1")]
