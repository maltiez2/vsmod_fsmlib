// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "All the disposable classes here can be disposed multiple times and there no way to know if it was already disposed when disposing", Scope = "member", Target = "~M:MaltiezFSM.Framework.FiniteStateMachine.Dispose")]
[assembly: SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "Same", Scope = "member", Target = "~M:MaltiezFSM.Framework.InputManager.Dispose")]
