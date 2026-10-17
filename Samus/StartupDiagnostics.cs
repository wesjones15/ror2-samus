using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;

namespace Samus
{
    // RoR2 DLC4 catches Steam startup exceptions and replaces them with a generic
    // dialog. Observe the existing checks and catch handler without changing
    // results, suppressing errors, retrying Steam or bypassing validation.
    internal static class StartupDiagnostics
    {
        static readonly List<ILHook> hooks = new List<ILHook>();

        internal static void Install()
        {
            try
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static;
                var type = typeof(SteamworksClientManager);
                var ctor = type.GetConstructor(flags, null, Type.EmptyTypes, null);
                if (ctor == null) throw new MissingMethodException(type.FullName, ".ctor()");
                var loader = type.GetNestedTypes(flags).SelectMany(t => t.GetMethods(flags))
                    .Single(m => m.Name.StartsWith("<Init>b__", StringComparison.Ordinal) &&
                        m.ReturnType == typeof(bool) && m.GetParameters().Length == 0);
                hooks.Add(new ILHook(ctor, TraceChecks));
                hooks.Add(new ILHook(loader, TraceCaughtException));
                Plugin.Log.LogInfo("Startup trace installed; all Steam validation results remain unchanged.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Startup trace unavailable: " + e);
            }
        }

        static void TraceChecks(ILContext il)
        {
            foreach (var call in il.Body.Instructions.ToArray())
            {
                if ((call.OpCode != OpCodes.Call && call.OpCode != OpCodes.Callvirt) ||
                    !(call.Operand is MethodReference method) ||
                    method.ReturnType.MetadataType != MetadataType.Boolean) continue;
                string name = method.DeclaringType.FullName + "." + method.Name;
                var cursor = new ILCursor(il);
                cursor.Goto(call, MoveType.After);
                cursor.EmitDelegate<Func<bool, bool>>(value =>
                {
                    Plugin.Log.LogInfo("Steam startup check " + name + " => " + value);
                    return value;
                });
            }
        }

        static void TraceCaughtException(ILContext il)
        {
            int count = 0;
            foreach (var handler in il.Body.ExceptionHandlers)
            {
                if (handler.HandlerType != ExceptionHandlerType.Catch) continue;
                var cursor = new ILCursor(il);
                cursor.Goto(handler.HandlerStart, MoveType.Before);
                // The handler's original pop must still receive the exception.
                // Move the handler boundary to include our observation.
                cursor.Emit(OpCodes.Dup);
                handler.HandlerStart = cursor.Prev;
                cursor.EmitDelegate<Action<object>>(error =>
                    Plugin.Log.LogError("Steam startup caught exception: " + error));
                count++;
            }
            if (count == 0) throw new InvalidOperationException("Steam loader has no catch handler to trace.");
        }
    }
}
