using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace Orike.ActionGraph
{
    /// <summary>
    /// Action Graph 节点编辑器窗口（Unity UI Toolkit）。
    ///
    /// 布局：
    ///   顶部工具栏（Graph 资产 / 新建 / 添加节点 / 保存 / 预览角色）
    ///   左侧节点画布（GraphView）
    ///   右侧 Inspector（Node / Edge 编辑与过渡预览）
    /// </summary>
    public class ActionGraphWindow : EditorWindow
    {
        private const float ToolbarHeight =
            38f;

        /// <summary>
        /// 窗口样式表：分隔条光标等 UI 样式。
        /// </summary>
        private const string UssPath =
            "Assets/Script/OrikeScript/OrikeComboEditor/ActionComboEditor--NodeGraph/Editor/ActionComboEditor.uss";


        // =========================================================
        // 组件
        // =========================================================

        private ActionGraphView _graphView;

        private ActionGraphInspector _inspector;

        private ObjectField _graphField;

        private ObjectField _characterField;

        private Button _addNodeButton;

        private Button _saveButton;

        private TransitionPreviewSystem _preview;

        private object _previewKey;

        private double _lastEditorTime;

        /// <summary>
        /// 延迟自动保存计时器（秒）。
        /// 避免编辑字段时每个字符都触发 SaveAssetIfDirty。
        /// </summary>
        private float _autoSaveTimer;

        /// <summary>
        /// 用户手动停止预览时选中的连线，
        /// 用于阻止 SyncAutoPreviewFromSelection 在同一选中状态下自动重启。
        /// 选中目标变化时自动清除。
        /// </summary>
        private TransitionData _manualStopKey;

        private const float AutoSaveInterval =
            2f;


        // =========================================================
        // Inspector 可拖拽分隔条
        // =========================================================

        private const float SplitterWidth =
            4f;

        private const float InspectorMinWidth =
            220f;

        private const float InspectorMaxWidth =
            600f;

        private VisualElement _splitter;

        private bool _isResizing;

        private float _resizeStartX;

        private float _inspectorStartWidth;


        // =========================================================
        // 属性
        // =========================================================

        public ActionGraphData Graph =>
            _graphField != null
                ? _graphField.value as ActionGraphData
                : null;

        public ActionGraphView GraphView =>
            _graphView;


        // =========================================================
        // 打开
        // =========================================================

        [MenuItem("Orike/Action Graph")]
        public static void Open()
        {
            ActionGraphWindow window =
                GetWindow<ActionGraphWindow>();

            window.titleContent =
                new GUIContent(
                    "Action Graph");

            window.minSize =
                new Vector2(
                    960,
                    520);
        }


        // =========================================================
        // 生命周期
        // =========================================================

        private void CreateGUI()
        {
            ApplyStyleSheet();

            BuildToolbar();

            BuildContent();

            EditorApplication.update +=
                OnEditorUpdate;

            _lastEditorTime =
                EditorApplication.timeSinceStartup;
        }


        /// <summary>
        /// 加载 ActionComboEditor.uss（分隔条光标等样式）。
        /// </summary>
        private void ApplyStyleSheet()
        {
            StyleSheet styleSheet =
                AssetDatabase.LoadAssetAtPath<
                    StyleSheet>(
                        UssPath);

            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(
                    styleSheet);
            }
        }


        private void OnDisable()
        {
            EditorApplication.update -=
                OnEditorUpdate;

            if (_preview != null)
            {
                _preview.Dispose();

                _preview =
                    null;
            }

            _previewKey =
                null;

            if (_graphView != null)
            {
                _graphView.Dispose();
            }
        }


        // =========================================================
        // 工具栏
        // =========================================================

        private void BuildToolbar()
        {
            VisualElement toolbar =
                new VisualElement();

            toolbar.style.height =
                ToolbarHeight;

            toolbar.style.flexShrink =
                0f;

            toolbar.style.flexDirection =
                FlexDirection.Row;

            toolbar.style.alignItems =
                Align.Center;

            toolbar.style.paddingLeft =
                8;

            toolbar.style.paddingRight =
                8;

            toolbar.style.backgroundColor =
                new Color(
                    0.10f,
                    0.10f,
                    0.10f);

            toolbar.style.borderBottomWidth =
                1;

            toolbar.style.borderBottomColor =
                new Color(
                    0.05f,
                    0.05f,
                    0.05f);

            rootVisualElement.Add(
                toolbar);


            Label graphLabel =
                new Label("Graph");

            graphLabel.style.width =
                44;

            toolbar.Add(
                graphLabel);

            _graphField =
                new ObjectField();

            _graphField.objectType =
                typeof(ActionGraphData);

            _graphField.allowSceneObjects =
                false;

            _graphField.style.width =
                240;

            _graphField.style.marginRight =
                6;

            _graphField.RegisterValueChangedCallback(
                evt =>
                {
                    LoadGraph(
                        evt.newValue as ActionGraphData);
                });

            toolbar.Add(
                _graphField);


            Button newButton =
                CreateToolbarButton(
                    "新建",
                    60);

            newButton.clicked +=
                OnClickNewGraph;

            toolbar.Add(
                newButton);

            _addNodeButton =
                CreateToolbarButton(
                    "+ Action",
                    72);

            _addNodeButton.clicked +=
                OnClickAddNode;

            toolbar.Add(
                _addNodeButton);

            _saveButton =
                CreateToolbarButton(
                    "保存",
                    60);

            _saveButton.clicked +=
                OnClickSave;

            toolbar.Add(
                _saveButton);


            // 弹簧
            VisualElement spacer =
                new VisualElement();

            spacer.style.flexGrow =
                1f;

            toolbar.Add(
                spacer);


            Label characterLabel =
                new Label("预览角色");

            characterLabel.style.marginRight =
                6;

            toolbar.Add(
                characterLabel);

            _characterField =
                new ObjectField();

            _characterField.objectType =
                typeof(GameObject);

            _characterField.allowSceneObjects =
                true;

            _characterField.style.width =
                200;

            toolbar.Add(
                _characterField);
        }


        private Button CreateToolbarButton(
            string text,
            float width)
        {
            Button button =
                new Button
                {
                    text = text,
                };

            button.style.width =
                width;

            button.style.height =
                26;

            button.style.marginRight =
                4;

            return button;
        }


        // =========================================================
        // 内容区
        // =========================================================

        private void BuildContent()
        {
            VisualElement content =
                new VisualElement();

            content.style.flexGrow =
                1f;

            content.style.flexDirection =
                FlexDirection.Row;

            rootVisualElement.Add(
                content);


            _graphView =
                new ActionGraphView(
                    this);

            content.Add(
                _graphView);


            _splitter =
                CreateSplitter();

            content.Add(
                _splitter);


            _inspector =
                new ActionGraphInspector(
                    this);

            content.Add(
                _inspector);


            rootVisualElement.style.flexDirection =
                FlexDirection.Column;
        }


        // =========================================================
        // 分隔条：拖拽调整 Inspector 宽度
        // =========================================================

        private VisualElement CreateSplitter()
        {
            VisualElement splitter =
                new VisualElement();

            splitter.style.width =
                SplitterWidth;

            splitter.style.flexShrink =
                0f;

            // 水平双向箭头光标由 ActionComboEditor.uss 的
            // .timeline-splitter 规则提供
            splitter.AddToClassList(
                "timeline-splitter");

            splitter.style.backgroundColor =
                new Color(
                    0.06f,
                    0.06f,
                    0.06f);

            // 悬停时高亮，提示可拖拽
            splitter.RegisterCallback<
                MouseEnterEvent>(
                _ =>
                {
                    splitter.style.backgroundColor =
                        new Color(
                            0.25f,
                            0.45f,
                            0.75f);
                });

            splitter.RegisterCallback<
                MouseLeaveEvent>(
                _ =>
                {
                    if (!_isResizing)
                    {
                        splitter.style.backgroundColor =
                            new Color(
                                0.06f,
                                0.06f,
                                0.06f);
                    }
                });

            splitter.RegisterCallback<
                PointerDownEvent>(
                OnSplitterPointerDown);

            splitter.RegisterCallback<
                PointerMoveEvent>(
                OnSplitterPointerMove);

            splitter.RegisterCallback<
                PointerUpEvent>(
                OnSplitterPointerUp);

            return splitter;
        }


        private void OnSplitterPointerDown(
            PointerDownEvent evt)
        {
            _isResizing =
                true;

            _resizeStartX =
                evt.position.x;

            _inspectorStartWidth =
                _inspector.resolvedStyle.width;

            _splitter.CapturePointer(
                evt.pointerId);

            evt.StopPropagation();
        }


        private void OnSplitterPointerMove(
            PointerMoveEvent evt)
        {
            if (!_isResizing)
            {
                return;
            }

            // 分隔条右移 → Inspector 变窄；左移 → 变宽
            float delta =
                evt.position.x -
                _resizeStartX;

            float newWidth =
                Mathf.Clamp(
                    _inspectorStartWidth -
                    delta,
                    InspectorMinWidth,
                    InspectorMaxWidth);

            _inspector.style.width =
                newWidth;

            evt.StopPropagation();
        }


        private void OnSplitterPointerUp(
            PointerUpEvent evt)
        {
            if (!_isResizing)
            {
                return;
            }

            _isResizing =
                false;

            _splitter.ReleasePointer(
                evt.pointerId);

            // 恢复默认背景色
            _splitter.style.backgroundColor =
                new Color(
                    0.06f,
                    0.06f,
                    0.06f);

            evt.StopPropagation();
        }


        // =========================================================
        // 工具栏事件
        // =========================================================

        private void OnClickNewGraph()
        {
            ActionGraphData graph =
                ActionGraphAssetOps.CreateGraphAsset();

            if (graph != null)
            {
                _graphField.value =
                    graph;

                LoadGraph(
                    graph);
            }
        }


        private void OnClickAddNode()
        {
            if (Graph == null)
            {
                ShowNoGraphWarning();

                return;
            }

            _graphView.CreateActionAtCenter();
        }


        private void OnClickSave()
        {
            if (Graph == null)
            {
                ShowNoGraphWarning();

                return;
            }

            EditorUtility.SetDirty(
                Graph);

            AssetDatabase.SaveAssetIfDirty(
                Graph);

            AssetDatabase.SaveAssets();

            ShowNotification(
                new GUIContent(
                    "Action Graph 已保存"));
        }


        private void ShowNoGraphWarning()
        {
            ShowNotification(
                new GUIContent(
                    "请先新建或指定一个 Action Graph 资产"));
        }


        // =========================================================
        // 加载
        // =========================================================

        public void LoadGraph(
            ActionGraphData graph)
        {
            if (_graphView == null)
            {
                return;
            }

            // 切换 / 重建资产时停止旧预览，
            // 避免重建过程中选中变化残留的过渡预览
            if (_preview != null &&
                _preview.IsRunning)
            {
                StopPreview();
            }

            if (graph != null)
            {
                graph.RefreshData();
            }

            _graphView.LoadGraph(
                graph);

            _inspector?.Invalidate();
        }


        /// <summary>
        /// 使用当前 GraphField 引用重新加载（Undo / Redo 后调用）。
        /// </summary>
        public void ReloadGraph()
        {
            LoadGraph(
                Graph);
        }


        // =========================================================
        // 预览
        // =========================================================

        private void EnsurePreview()
        {
            if (_preview == null)
            {
                _preview =
                    new TransitionPreviewSystem();
            }
        }


        private GameObject GetPreviewCharacter()
        {
            return _characterField != null
                ? _characterField.value as GameObject
                : null;
        }


        public bool IsPreviewingAction(
            Action action)
        {
            return
                _preview != null &&
                _preview.IsRunning &&
                ReferenceEquals(
                    _previewKey,
                    action);
        }


        public bool IsPreviewingTransition(
            TransitionData transition)
        {
            return
                _preview != null &&
                _preview.IsRunning &&
                ReferenceEquals(
                    _previewKey,
                    transition);
        }


        public void TogglePreviewAction(
            Action action)
        {
            if (IsPreviewingAction(action))
            {
                StopPreview();

                return;
            }

            GameObject character =
                GetPreviewCharacter();

            if (character == null)
            {
                ShowNoCharacterWarning();

                return;
            }

            EnsurePreview();

            _preview.StartSingle(
                character,
                action.ActionData);

            _previewKey =
                action;
        }


        public void TogglePreviewTransition(
            TransitionData transition)
        {
            if (IsPreviewingTransition(transition))
            {
                // 记录手动停止的连线，阻止自动预览在下一帧重启
                _manualStopKey =
                    transition;

                StopPreview();

                return;
            }

            // 手动重新播放时清除停止标记
            _manualStopKey =
                null;

            StartTransitionPreview(
                transition,
                true);
        }


        /// <summary>
        /// 选中连线时自动触发过渡预览（不弹警告框）。
        /// 已经在预览同一条连线时保持现状；
        /// 未指定预览角色或任一端未绑定 ActionData 时静默跳过。
        /// </summary>
        public void PreviewTransitionFromSelection(
            TransitionData transition)
        {
            if (transition == null)
            {
                return;
            }

            if (IsPreviewingTransition(
                    transition))
            {
                return;
            }

            StartTransitionPreview(
                transition,
                false);
        }


        /// <summary>
        /// 取消选中连线时停止“自动预览”。
        /// 只停止 Transition 预览，不影响通过节点按钮启动的单动作预览。
        /// </summary>
        public void StopAutoTransitionPreview()
        {
            if (_previewKey is TransitionData)
            {
                StopPreview();
            }
        }


        /// <summary>
        /// 启动过渡预览的共用入口。
        /// </summary>
        /// <param name="showWarning">
        /// 手动点击按钮时为 true（缺角色弹框提示）；
        /// 选中连线自动触发时为 false（静默跳过）。
        /// </param>
        private void StartTransitionPreview(
            TransitionData transition,
            bool showWarning)
        {
            if (transition.From == null ||
                transition.To == null ||
                transition.From.ActionData == null ||
                transition.To.ActionData == null)
            {
                return;
            }

            GameObject character =
                GetPreviewCharacter();

            if (character == null)
            {
                if (showWarning)
                {
                    ShowNoCharacterWarning();
                }

                return;
            }

            EnsurePreview();

            _preview.StartTransition(
                character,
                transition.From.ActionData,
                transition.To.ActionData,
                transition);

            _previewKey =
                transition;
        }


        private void StopPreview()
        {
            _preview?.Stop();

            _previewKey =
                null;
        }


        private void ShowNoCharacterWarning()
        {
            EditorUtility.DisplayDialog(
                "Transition Preview",
                "请先在工具栏指定预览角色。",
                "OK");
        }


        // =========================================================
        // Editor Update
        // =========================================================

        private void OnEditorUpdate()
        {
            double currentTime =
                EditorApplication.timeSinceStartup;

            float deltaTime =
                Mathf.Clamp(
                    (float)(currentTime - _lastEditorTime),
                    0f,
                    0.1f);

            _lastEditorTime =
                currentTime;

            SyncAutoPreviewFromSelection();

            // 延迟自动保存：停止输入 2 秒后统一写盘
            if (Graph != null)
            {
                _autoSaveTimer +=
                    deltaTime;

                if (_autoSaveTimer >=
                    AutoSaveInterval)
                {
                    _autoSaveTimer =
                        0f;

                    AssetDatabase.SaveAssetIfDirty(
                        Graph);
                }
            }

            if (_preview != null &&
                _preview.IsRunning)
            {
                _preview.Tick(
                    deltaTime);

                Repaint();
            }
        }


        /// <summary>
        /// 根据画布选中状态同步“连线自动预览”：
        /// 选中 Edge -> 启动（静默）；选中节点 / 空白 -> 停止。
        /// 每帧调用，但内部有状态守卫，仅在选中目标变化时真正启停。
        /// </summary>
        private void SyncAutoPreviewFromSelection()
        {
            if (_graphView == null)
            {
                return;
            }

            // 多选时与 Inspector 一致：节点优先，不做过渡预览
            if (_graphView.GetSelectedNode() != null)
            {
                _manualStopKey =
                    null;

                StopAutoTransitionPreview();

                return;
            }

            UnityEditor.Experimental.GraphView.Edge edge =
                _graphView.GetSelectedEdge();

            TransitionData transition =
                edge != null
                    ? edge.userData as TransitionData
                    : null;

            if (transition != null)
            {
                // 用户手动停止了同一条连线的预览时不自动重启，
                // 选中目标变化后 _manualStopKey 被清除即恢复自动预览
                if (_manualStopKey == transition)
                {
                    return;
                }

                PreviewTransitionFromSelection(
                    transition);
            }
            else
            {
                // 选中目标不再是连线，清除手动停止标记
                _manualStopKey =
                    null;

                StopAutoTransitionPreview();
            }
        }
    }
}
