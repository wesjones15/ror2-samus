using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Inspect the shipping assembly, without running Unity, Steam or the native DLL.
// This regression guards the bootstrap boundary implicated by repeated failures.
static class Program
{
    static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types)
    {foreach(var type in types){yield return type;foreach(var nested in Types(type.NestedTypes))yield return nested;}}
    static int Main(string[] args)
    {
        try {
            using var assembly=AssemblyDefinition.ReadAssembly(args[0]);
            var module=assembly.MainModule;
            var types=Types(module.Types).ToArray();
            var methods=types.SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
            var plugin=types.Single(t=>t.FullName=="Samus.Plugin");
            var awake=plugin.Methods.Single(m=>m.Name=="Awake");
            var violations=new List<string>();
            foreach(var reference in module.GetTypeReferences())
                if(reference.FullName.Contains("Steamworks"))violations.Add("Steam bootstrap type reference: "+reference.FullName);
            var visited=new HashSet<string>();var pending=new Stack<MethodDefinition>();pending.Push(awake);
            while(pending.Count>0) {
                var method=pending.Pop();if(!visited.Add(method.FullName)||!method.HasBody)continue;
                foreach(var instruction in method.Body.Instructions) {
                    if(instruction.OpCode.Code!=Code.Call&&instruction.OpCode.Code!=Code.Callvirt&&instruction.OpCode.Code!=Code.Newobj)continue;
                    if(!(instruction.Operand is MethodReference call))continue;
                    if(call.DeclaringType.FullName=="Samus.Native"||call.DeclaringType.FullName=="Ror2Common.NativeLib")
                        violations.Add("Native bootstrap call from "+method.FullName+": "+call.FullName);
                    if(methods.TryGetValue(call.FullName,out var local))pending.Push(local);
                }
            }
            var initialize=plugin.Methods.FirstOrDefault(m=>m.Name=="InitializeAssets");
            bool onLoad=awake.Body.Instructions.Any(i=>
                i.Operand is MethodReference m&&m.DeclaringType.FullName=="RoR2.RoR2Application"&&m.Name=="add_onLoad" ||
                i.OpCode.Code==Code.Stsfld&&i.Operand is FieldReference f&&f.DeclaringType.FullName=="RoR2.RoR2Application"&&f.Name=="onLoad");
            bool deferred=awake.Body.Instructions.Any(i=>i.OpCode.Code==Code.Ldftn&&i.Operand is MethodReference m&&m.Name=="InitializeAssets");
            bool loadsNative=initialize!=null&&initialize.HasBody&&initialize.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.FullName=="Samus.Native"&&m.Name=="Load");
            if(!onLoad||!deferred||!loadsNative||((int)initialize.ImplAttributes&8)==0)violations.Add($"Missing deferred, non-inlined onLoad native initialization (onLoad={onLoad}, deferred={deferred}, loadsNative={loadsNative})");
            bool expectUnsafe=args.Skip(1).Contains("--expect-unsafe");
            if(expectUnsafe) {
                if(violations.Count==0)throw new Exception("Regression control unexpectedly passed");
                Console.WriteLine("PASS: old release rejected by startup boundary check ("+violations.Count+" violations)");return 0;
            }
            if(violations.Count>0)throw new Exception(string.Join(Environment.NewLine,violations));
            Console.WriteLine("PASS: shipping assembly has no Steam hooks/types or native calls reachable from Awake; native loading is deferred to onLoad");
            return 0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
