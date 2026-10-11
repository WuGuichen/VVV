using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FIMSpace.AnimationTools.CustomModules
{

    public class ADModule_AddEventExtended : ADCustomModuleBase
    {
        public override string ModuleTitleName { get { return "Utilities/Add Event Extended"; } }
        public override bool GUIFoldable { get { return false; } }
        public override bool SupportBlending { get { return false; } }

        public string DefaultMainTitle = "Event Title";
        public List<string> TitlesToChoose = new List<string>();
        public List<string> SubTitlesToChoose = new List<string>();
        enum EParameterType { String, Float, Int, Object }

        public override void InspectorGUI_ModuleBody(float clipProgress, ADClipSettings_Main _anim_MainSet, AnimationDesignerSave s, ADClipSettings_CustomModules cModule, ADClipSettings_CustomModules.CustomModuleSet set)
        {
            EditorGUILayout.HelpBox("Module for Including Animation Event in the exported animation clip", MessageType.None);

            GUILayout.Space(6);


            EditorGUILayout.BeginHorizontal();
            var targetName = GetVariable("Event Name", set, DefaultMainTitle);
            targetName.HideFlag = false;
            targetName.DrawGUI();

            if (TitlesToChoose.Count > 0)
            {
                if (GUILayout.Button("", EditorStyles.popup, GUILayout.Width(20)))
                {
                    GenericMenu gMenu = new GenericMenu();

                    for (int i = 0; i < TitlesToChoose.Count; i++)
                    {
                        string title = TitlesToChoose[i];
                        gMenu.AddItem(new GUIContent(title), false, () => { targetName.SetValue(title); EditorUtility.SetDirty(s); });
                    }

                    gMenu.ShowAsContext();
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(" Parameter", EditorStyles.boldLabel, GUILayout.Width(70));

            var paramT = GetVariable("ParamType", set, 0);
            EParameterType param = (EParameterType)paramT.GetIntValue();
            param = (EParameterType)EditorGUILayout.EnumPopup(param, GUILayout.Width(60));
            paramT.SetValue((int)param);

            GUILayout.Space(3);

            if (param == EParameterType.String)
            {
                var stringValue = GetVariable("Param", set, "");
                stringValue.SetValue(EditorGUILayout.TextField(stringValue.GetStringValue()));

                if (SubTitlesToChoose.Count > 0)
                {
                    if (GUILayout.Button("", EditorStyles.popup, GUILayout.Width(20)))
                    {
                        GenericMenu gMenu = new GenericMenu();

                        for (int i = 0; i < SubTitlesToChoose.Count; i++)
                        {
                            string title = SubTitlesToChoose[i];
                            gMenu.AddItem(new GUIContent(title), false, () => { stringValue.SetValue(title); EditorUtility.SetDirty(s); });
                        }

                        gMenu.ShowAsContext();
                    }
                }
            }
            else if (param == EParameterType.Int)
            {
                var paramValue = GetVariable("Param", set, 0);
                paramValue.SetValue(EditorGUILayout.IntField(paramValue.GetIntValue()));
            }
            else if (param == EParameterType.Float)
            {
                var paramValue = GetVariable("Param", set, 0f);
                paramValue.SetValue(EditorGUILayout.FloatField(paramValue.GetFloatValue()));
            }
            else if (param == EParameterType.Object)
            {
                var paramValue = GetVariable("Param", set, null);
                paramValue.SetValue(EditorGUILayout.ObjectField(paramValue.GetUnityObjRef(), typeof(UnityEngine.Object), false));
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            var atProgr = GetVariable("At Animation Progress", set, 0.25f);
            atProgr.SetRangeHelperValue(0f, 1f);
            atProgr.DrawGUI();

            GUILayout.Space(4);
            EditorGUILayout.HelpBox("Current Animation Progress = " + System.Math.Round(clipProgress, 2).ToString(), MessageType.None);
        }


        public override void OnExportFinalizing(AnimationClip originalClip, AnimationClip newGeneratedClip, AnimationDesignerSave s, ADClipSettings_Main anim_MainSet, ADClipSettings_CustomModules customModules, ADClipSettings_CustomModules.CustomModuleSet set, List<AnimationEvent> addingEvents)
        {
            base.OnExportFinalizing(originalClip, newGeneratedClip, s, anim_MainSet, customModules, set, addingEvents);

            var targetName = GetVariable("Event Name", set, DefaultMainTitle).GetStringValue();
            var atProgr = GetFloatVariable("At Animation Progress");

            AnimationEvent evt = new AnimationEvent();
            evt.time = atProgr * newGeneratedClip.length /** anim_MainSet.ClipDurationMultiplier*/;
            evt.functionName = targetName;

            var paramT = GetVariable("ParamType", set, 0);
            EParameterType param = (EParameterType)paramT.GetIntValue();

            var paramValue = GetVariable("Param", set, "");

            if (param == EParameterType.String) evt.stringParameter = paramValue.GetStringValue();
            else if (param == EParameterType.Int) evt.intParameter = paramValue.GetIntValue();
            else if (param == EParameterType.Float) evt.floatParameter = paramValue.GetFloatValue();
            else if (param == EParameterType.Object) evt.objectReferenceParameter = paramValue.GetUnityObjRef();

            addingEvents.Add(evt);
        }



    }
}