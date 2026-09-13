using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

namespace Orike.ActionGraph
{
    /// <summary>
    /// 右侧 Inspector 面板。
    ///
    /// 选中 ActionNode 时编辑 Action：
    ///   Id / ActionData / Priority / Loop / KeyCommand / Cancel / BeCancel
    ///
    /// 选中 Edge 时编辑 TransitionData：
    ///   From / To / FadeOut / FadeIn / StartPercent / Priority
    ///
    /// 并提供动作 / 过渡预览按钮。
    /// </summary>
    public class ActionGraphInspector : VisualElement
    {
        private const float PanelWidth =
            310f;

        private readonly ActionGraphWindow _window;

        private readonly IMGUIContainer _content;


        // Node 选中状态

        private Action _action;

        private SerializedObject _actionSerialized;


        // Edge 选中状态

        private TransitionData _transition;

        private int _transitionIndex =
            -1;

        private SerializedObject _graphSerialized;


        public ActionGraphInspector(
            ActionGraphWindow window)
        {
            _window =
                window;

            style.width =
                PanelWidth;

            style.minWidth =
                220f;

            // 不设 maxWidth，由窗口分隔条控制宽度上限

            style.flexShrink =
                0f;

            style.flexDirection =
                FlexDirection.Column;

            style.backgroundColor =
                new Color(
                    0.16f,
                    0.16f,
                    0.16f);


            Label title =
                new Label("Inspector");

            title.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            title.style.fontSize =
                13;

            title.style.paddingTop =
                8;

            title.style.paddingLeft =
                10;

            title.style.paddingBottom =
                6;

            Add(title);


            _content =
                new IMGUIContainer(
                    DrawInspector);

            _content.style.flexGrow =
                1f;

            _content.style.paddingLeft =
                8;

            _content.style.paddingRight =
                8;

            Add(_content);
        }


        /// <summary>
        /// 强制下一帧重建 SerializedObject。
        /// </summary>
        public void Invalidate()
        {
            _action =
                null;

            _actionSerialized =
                null;

            _transition =
                null;

            _transitionIndex =
                -1;

            _graphSerialized =
                null;
        }


        // =========================================================
        // 选中状态同步
        // =========================================================

        private void SyncSelection()
        {
            ActionGraphData graph =
                _window.Graph;

            ActionGraphView graphView =
                _window.GraphView;

            if (graph == null ||
                graphView == null)
            {
                ClearSelection();

                return;
            }

            ActionNodeView node =
                graphView.GetSelectedNode();

            if (node != null)
            {
                if (_action != node.Action)
                {
                    _action =
                        node.Action;

                    _actionSerialized =
                        new SerializedObject(
                            _action);
                }

                _transition =
                    null;

                return;
            }


            Edge edge =
                graphView.GetSelectedEdge();

            TransitionData transition =
                edge != null
                    ? edge.userData as TransitionData
                    : null;

            if (transition != null)
            {
                if (_transition != transition)
                {
                    _transition =
                        transition;

                    _transitionIndex =
                        graph.Transitions.IndexOf(
                            transition);

                    _graphSerialized =
                        new SerializedObject(
                            graph);
                }

                _action =
                    null;

                _actionSerialized =
                    null;

                return;
            }

            ClearSelection();
        }


        private void ClearSelection()
        {
            _action =
                null;

            _actionSerialized =
                null;

            _transition =
                null;

            _transitionIndex =
                -1;
        }


        // =========================================================
        // 绘制
        // =========================================================

        private void DrawInspector()
        {
            SyncSelection();

            if (_window.Graph == null)
            {
                EditorGUILayout.HelpBox(
                    "请先创建或打开一个 Action Graph 资产。",
                    MessageType.Info);

                return;
            }

            if (_actionSerialized != null)
            {
                DrawActionInspector();

                return;
            }

            if (_transition != null &&
                _graphSerialized != null)
            {
                DrawTransitionInspector();

                return;
            }

            EditorGUILayout.HelpBox(
                "选中一个 Action 节点编辑动作逻辑，\n或选中一条连线编辑 Transition。",
                MessageType.Info);
        }


        // =========================================================
        // Action Node Inspector
        // =========================================================

        private void DrawActionInspector()
        {
            if (_action == null)
            {
                _actionSerialized =
                    null;

                return;
            }

            EditorGUILayout.LabelField(
                "Action 节点",
                EditorStyles.boldLabel);

            ActionFieldDrawer.Result result =
                ActionFieldDrawer.Draw(
                    _actionSerialized,
                    _action,
                    _window.Graph);

            if (result.Changed)
            {
                if (result.Structural)
                {
                    _window.GraphView.RequestReload();
                }
                else
                {
                    _window.GraphView.RefreshAllNodeLabels();
                }
            }

            EditorGUILayout.Space(8);

            DrawActionPreviewButton();
        }


        private void DrawActionPreviewButton()
        {
            if (_action.ActionData == null)
            {
                EditorGUILayout.HelpBox(
                    "绑定 ActionData 后可预览动画。",
                    MessageType.None);

                return;
            }

            bool isPreviewing =
                _window.IsPreviewingAction(
                    _action);

            string buttonText =
                isPreviewing
                    ? "■ 停止动作预览"
                    : "? 预览动作动画";

            if (GUILayout.Button(
                    buttonText,
                    GUILayout.Height(26)))
            {
                _window.TogglePreviewAction(
                    _action);
            }
        }


        // =========================================================
        // Edge / Transition Inspector
        // =========================================================

        private void DrawTransitionInspector()
        {
            if (_transitionIndex < 0 ||
                _transitionIndex >=
                _window.Graph.Transitions.Count)
            {
                Invalidate();

                return;
            }

            _graphSerialized.Update();

            SerializedProperty transitionProperty =
                _graphSerialized
                    .FindProperty("Transitions")
                    .GetArrayElementAtIndex(
                        _transitionIndex);

            SerializedProperty fromProperty =
                transitionProperty.FindPropertyRelative("From");

            SerializedProperty toProperty =
                transitionProperty.FindPropertyRelative("To");

            SerializedProperty fadeOutProperty =
                transitionProperty.FindPropertyRelative("FadeOut");

            SerializedProperty fadeInProperty =
                transitionProperty.FindPropertyRelative("FadeIn");

            SerializedProperty startPercentProperty =
                transitionProperty.FindPropertyRelative("StartPercent");

            SerializedProperty priorityProperty =
                transitionProperty.FindPropertyRelative("Priority");


            Action from =
                _transition.From;

            Action to =
                _transition.To;

            string fromName =
                from != null
                    ? from.Id
                    : "<空>";

            string toName =
                to != null
                    ? to.Id
                    : "<空>";

            EditorGUILayout.LabelField(
                "Transition (Edge)",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                $"{fromName}  →  {toName}");

            EditorGUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(
                true);

            EditorGUILayout.ObjectField(
                "From（被取消）",
                from,
                typeof(Action),
                false);

            EditorGUILayout.ObjectField(
                "To（执行取消）",
                to,
                typeof(Action),
                false);

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            fadeOutProperty.floatValue =
                EditorGUILayout.Slider(
                    "FadeOut（退出秒数）",
                    fadeOutProperty.floatValue,
                    0f,
                    2f);

            fadeInProperty.floatValue =
                EditorGUILayout.Slider(
                    "FadeIn（进入秒数）",
                    fadeInProperty.floatValue,
                    0f,
                    2f);

            startPercentProperty.floatValue =
                EditorGUILayout.Slider(
                    "StartPercent（目标起点）",
                    startPercentProperty.floatValue,
                    0f,
                    1f);

            bool slidersChanged =
                EditorGUI.EndChangeCheck();

            if (slidersChanged)
            {
                // 滑杆直接写 floatValue，需手动登记 Undo
                Undo.RecordObject(
                    _window.Graph,
                    "Edit Transition");
            }

            EditorGUILayout.PropertyField(
                priorityProperty,
                new GUIContent(
                    "Priority（额外优先级）"));

            EditorGUILayout.HelpBox(
                "最终优先级 = Action.Priority + Transition.Priority",
                MessageType.None);

            if (_graphSerialized.ApplyModifiedProperties() ||
                slidersChanged)
            {
                EditorUtility.SetDirty(
                    _window.Graph);
            }

            EditorGUILayout.Space(8);

            DrawTransitionPreviewButton(
                from,
                to);
        }


        private void DrawTransitionPreviewButton(
            Action from,
            Action to)
        {
            bool canPreview =
                from != null &&
                to != null &&
                from.ActionData != null &&
                to.ActionData != null;

            if (!canPreview)
            {
                EditorGUILayout.HelpBox(
                    "两端 Action 都绑定 ActionData 后可预览过渡。",
                    MessageType.None);

                return;
            }

            bool isPreviewing =
                _window.IsPreviewingTransition(
                    _transition);

            string buttonText =
                isPreviewing
                    ? "■ 停止过渡预览"
                    : "? 预览动作过渡";

            if (GUILayout.Button(
                    buttonText,
                    GUILayout.Height(26)))
            {
                _window.TogglePreviewTransition(
                    _transition);
            }

            EditorGUILayout.HelpBox(
                "预览中可直接拖动上方滑杆，混合效果实时刷新。",
                MessageType.None);
        }
    }
}
