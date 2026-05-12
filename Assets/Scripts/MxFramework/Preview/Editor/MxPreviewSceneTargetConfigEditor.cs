using MxFramework.Preview;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Preview.EditorMenu
{
    [CustomEditor(typeof(MxPreviewSceneTargetConfig))]
    public sealed class MxPreviewSceneTargetConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetId;
        private SerializedProperty _initialHp;
        private SerializedProperty _initialAttack;
        private SerializedProperty _initialDefense;
        private SerializedProperty _resetOnPreviewRun;
        private SerializedProperty _showOverlay;
        private SerializedProperty _createRuntimeTarget;

        private void OnEnable()
        {
            _targetId = serializedObject.FindProperty("_targetId");
            _initialHp = serializedObject.FindProperty("_initialHp");
            _initialAttack = serializedObject.FindProperty("_initialAttack");
            _initialDefense = serializedObject.FindProperty("_initialDefense");
            _resetOnPreviewRun = serializedObject.FindProperty("_resetOnPreviewRun");
            _showOverlay = serializedObject.FindProperty("_showOverlay");
            _createRuntimeTarget = serializedObject.FindProperty("_createRuntimeTarget");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Runtime Preview Target Config", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这是编辑态配置，不是运行时目标。Preview Server 启动或刷新时会按这里的配置动态生成 MxPreviewSceneTarget；场景资产中不需要保存运行时 Target 组件。",
                MessageType.Info);

            EditorGUILayout.PropertyField(_targetId, new GUIContent("Target Id", "Preview RPC 使用的目标 ID。默认 TestTarget / TestCaster。"));
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("初始数值", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_initialHp, new GUIContent("HP", "预览运行开始时的生命值。"));
            EditorGUILayout.PropertyField(_initialAttack, new GUIContent("Attack", "预览运行开始时的攻击力。"));
            EditorGUILayout.PropertyField(_initialDefense, new GUIContent("Defense", "预览运行开始时的防御力。"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("运行时生成", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_createRuntimeTarget, new GUIContent("运行时生成 Target", "关闭后该配置不会生成运行时 Target。通常保持开启。"));
            EditorGUILayout.PropertyField(_resetOnPreviewRun, new GUIContent("每次预览前重置", "连续预览时是否把属性、Buff、Modifier 恢复到初始状态。"));
            EditorGUILayout.PropertyField(_showOverlay, new GUIContent("显示运行时 Overlay", "运行后动态 Target 是否显示 legacy OnGUI overlay。后续会迁移到 UI Toolkit。"));

            if (!_createRuntimeTarget.boolValue)
                EditorGUILayout.HelpBox("该配置不会参与 Preview Server 场景模式；如果没有其他有效配置，会回退到 dummy world。", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "编辑态预览：Scene 视图会显示 Gizmo 标记。运行态预览：启动 Runtime Preview Server 后才会生成真实 Attribute / Buff / Modifier 状态。",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
