using System;
using System.Reflection;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.UI.FairyGui
{
    internal static class FairyGuiTestStageReset
    {
        private const BindingFlags StaticFieldFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceMethodFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Reset()
        {
            object stage = GetStaticField(typeof(Fgui.Stage), "_inst");
            object gRoot = GetStaticField(typeof(Fgui.GRoot), "_inst");

            TryDispose(stage);
            TryDispose(gRoot);

            SetStaticField(typeof(Fgui.GRoot), "_inst", null);
            SetStaticField(typeof(Fgui.Stage), "_inst", null);

            DestroyObjectNamed("Stage");
            DestroyObjectNamed("GRoot");
            DestroyObjectNamed("Stage Camera");
        }

        private static object GetStaticField(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, StaticFieldFlags);
            return field != null ? field.GetValue(null) : null;
        }

        private static void SetStaticField(Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, StaticFieldFlags);
            field?.SetValue(null, value);
        }

        private static void TryDispose(object value)
        {
            if (value == null)
                return;

            try
            {
                MethodInfo dispose = value.GetType().GetMethod(
                    "Dispose",
                    InstanceMethodFlags,
                    null,
                    Type.EmptyTypes,
                    null);
                dispose?.Invoke(value, null);
            }
            catch (TargetInvocationException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (NullReferenceException)
            {
            }
        }

        private static void DestroyObjectNamed(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);
        }
    }
}
