using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelSceneOwnerSelectionTests
    {
        private const string Target = "Assets/Scenes/Target.unity";
        private sealed class Owner { internal readonly string Scene; internal Owner(string scene) { Scene = scene; } }
        private static readonly Type Operations = typeof(CharacterTestLevelAcceptance).Assembly.GetType(
            "MxFramework.Game.CharacterTest.Editor.CharacterTestLevelAuthoringOperations", true);

        private static Owner Select(string path, params Owner[] owners)
        {
            var method = Operations.GetMethod("SelectSceneOwner", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(Owner));
            try { return (Owner)method.Invoke(null, new object[] { path, owners, new Func<Owner, string>(o => o.Scene) }); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }

        [Test] public void UnrelatedEarlierManagerDoesNotReplaceDocumentOwner()
        {
            var other = new Owner("Assets/Scenes/Other.unity"); var target = new Owner(Target);
            Assert.That(Select(Target, other, target), Is.SameAs(target));
        }
        [Test] public void OwnerEnumerationOrderDoesNotChangeSelection()
        {
            var other = new Owner("Assets/Scenes/Other.unity"); var target = new Owner(Target);
            Assert.That(Select(Target, target, other), Is.SameAs(target));
            Assert.That(Select(Target, other, target), Is.SameAs(target));
        }
        [Test] public void DuplicateTargetOwnersRejectInsteadOfChoosingFirst()
            => Assert.Throws<InvalidOperationException>(() => Select(Target, new Owner(Target), new Owner(Target)));
        [Test] public void MissingTargetRejectsWithRequiredScenePath()
            => Assert.That(Assert.Throws<InvalidOperationException>(() => Select(Target, new Owner("Assets/Scenes/Other.unity"))).Message, Does.Contain(Target));
        [Test] public void ScenePathRequiresExactIdentity()
            => Assert.Throws<InvalidOperationException>(() => Select(Target, new Owner(Target + ".bak"), new Owner("assets/scenes/target.unity")));
        [Test] public void EmptyScenePathCannotSelectAnUnsavedScene()
            => Assert.Throws<InvalidOperationException>(() => Select("", new Owner("")));
        [Test] public void NullOwnerIsIgnoredWhenOneTargetExists()
        {
            var target = new Owner(Target); Assert.That(Select(Target, null, target), Is.SameAs(target));
        }
        [Test] public void RuntimeAndEditorScopeCallTheSameProfileResolver()
        {
            var runtime = typeof(GameManager).GetMethod("InitializeRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
            var scope = Operations.GetNestedType("PreflightProfileScope", BindingFlags.NonPublic);
            var constructor = scope.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            Assert.That(Calls(runtime).Any(m => m.DeclaringType == typeof(CharacterTestSceneProfile) && m.Name == "ResolveStartupProfile"), Is.True);
            Assert.That(Calls(constructor).Any(m => m.DeclaringType == typeof(CharacterTestSceneProfile) && m.Name == "ResolveStartupProfile"), Is.True);
        }
        [Test] public void CompiledPreflightHasProfileCleanupFinally()
        {
            var preflight = Operations.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(m => m.Name == "Preflight" && m.GetParameters().Length == 2);
            var scope = Operations.GetNestedType("PreflightProfileScope", BindingFlags.NonPublic);
            var body = preflight.GetMethodBody();
            int profileLocal = body.LocalVariables.Single(local => local.LocalType == scope).LocalIndex;
            var instructions = Instructions(preflight).ToArray();
            // using invokes IDisposable.Dispose; verify the finally actually loads the profile scope,
            // rather than accepting the prepared session's independent cleanup as evidence.
            Assert.That(body.ExceptionHandlingClauses.Where(c => c.Flags == ExceptionHandlingClauseOptions.Finally).Any(clause =>
            {
                var cleanup = instructions.Where(i => i.Offset >= clause.HandlerOffset && i.Offset < clause.HandlerOffset + clause.HandlerLength).ToArray();
                return cleanup.Any(i => LoadedLocal(i.Code, i.Operand) == profileLocal)
                    && cleanup.Any(i => i.Code.OperandType == OperandType.InlineMethod
                        && preflight.Module.ResolveMethod(i.Operand).DeclaringType == typeof(IDisposable)
                        && preflight.Module.ResolveMethod(i.Operand).Name == "Dispose");
            }), Is.True);
            Assert.That(scope.GetInterfaceMap(typeof(IDisposable)).TargetMethods.Single().DeclaringType, Is.EqualTo(scope));
        }

        private static IEnumerable<MethodBase> Calls(MethodBase method)
            => Instructions(method).Where(i => i.Code.OperandType == OperandType.InlineMethod)
                .Select(i => method.Module.ResolveMethod(i.Operand));

        private static int LoadedLocal(OpCode code, int operand)
        {
            if (code == OpCodes.Ldloc_0) return 0;
            if (code == OpCodes.Ldloc_1) return 1;
            if (code == OpCodes.Ldloc_2) return 2;
            if (code == OpCodes.Ldloc_3) return 3;
            return code == OpCodes.Ldloc || code == OpCodes.Ldloc_S ? operand : -1;
        }

        private static IEnumerable<(int Offset, OpCode Code, int Operand)> Instructions(MethodBase method)
        {
            var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => unchecked((ushort)o.Value));
            var bytes = method.GetMethodBody().GetILAsByteArray();
            for (int i = 0; i < bytes.Length;)
            {
                int offset = i;
                ushort key = bytes[i++]; if (key == 0xfe) key = (ushort)(0xfe00 | bytes[i++]);
                var opcode = opcodes[key]; int size;
                switch (opcode.OperandType)
                {
                    case OperandType.InlineNone: size = 0; break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                    case OperandType.InlineVar: size = 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                    case OperandType.InlineSwitch: size = 4 + BitConverter.ToInt32(bytes, i) * 4; break;
                    default: size = 4; break;
                }
                int operand = size == 1 ? bytes[i] : size == 2 ? BitConverter.ToUInt16(bytes, i) : size == 4 ? BitConverter.ToInt32(bytes, i) : 0;
                yield return (offset, opcode, operand);
                i += size;
            }
        }
    }
}
