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
            BuildToolbar();

            BuildContent();

            EditorApplication.update +=
                OnEditorUpdate;

            _lastEditorTime =
                EditorApplication.timeSinceStartup;
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

            splitter.style.cursor =
                new StyleCursor(
                    GetHorizontalResizeCursor());

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
        // 分隔条光标纹理
        // =========================================================

        private static Texture2D _resizeCursor;

        /// <summary>
        /// 生成一个水平双向箭头光标纹理（左右拖拽）。
        /// </summary>
        private static UnityEngine.UIElements.Cursor GetHorizontalResizeCursor()
        {
            if (_resizeCursor == null)
            {
                _resizeCursor =
                    CreateHorizontalResizeTexture();
            }

            return
                new UnityEngine.UIElements.Cursor
                {
                    texture =
                        _resizeCursor,

                    hotspot =
                        new Vector2(
                            _resizeCursor.width * 0.5f,
                            _resizeCursor.height * 0.5f),
                };
        }


        private static Texture2D CreateHorizontalResizeTexture()
        {
            const int size =
                20;

            Texture2D texture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false);

            texture.name =
                "ResizeHorizontalCursor";

            Color[] pixels =
                new Color[size * size];

            for (int i = 0;
                 i < pixels.Length;
                 i++)
            {
                pixels[i] =
                    new Color(
                        0f,
                        0f,
                        0f,
                        0f);
            }

            int mid =
                size / 2;

            Color dark =
                new Color(
                    0.15f,
                    0.15f,
                    0.15f,
                    1f);

            Color white =
                Color.white;

            void Plot(
                int x,
                int y,
                Color color)
            {
                if (x >= 0 &&
                    x < size &&
                    y >= 0 &&
                    y < size)
                {
                    pixels[y * size + x] =
                        color;
                }
            }


            // 中心横条（粗黑边 + 白芯）
            for (int x = 1;
                 x < size - 1;
                 x++)
            {
                for (int t = -2;
                     t <= 2;
                     t++)
                {
                    Plot(
                        x,
                        mid + t,
                        dark);
                }

                Plot(
                    x,
                    mid,
                    white);
            }


            // 左箭头（黑边 + 白芯）
            for (int i = 0;
                 i < 4;
                 i++)
            {
                for (int j = -i;
                     j <= i;
                     j++)
                {
                    Plot(
                        3 + i,
                        mid + j,
                        dark);
                }
            }

            for (int i = 0;
                 i < 3;
                 i++)
            {
                for (int j = -i;
                     j <= i;
                     j++)
                {
                    Plot(
                        3 + i,
                        mid + j,
                        white);
                }
            }


            // 右箭头（黑边 + 白芯）
            for (int i = 0;
                 i < 4;
                 i++)
            {
                for (int j = -i;
                     j <= i;
                     j++)
                {
                    Plot(
                        size - 4 - i,
                        mid + j,
                        dark);
                }
            }

            for (int i = 0;
                 i < 3;
                 i++)
            {
                for (int j = -i;
                     j <= i;
                     j++)
                {
                    Plot(
                        size - 4 - i,
                        mid + j,
                        white);
                }
            }


            texture.SetPixels(
                pixels);

            texture.alphaIsTransparency =
                true;

            texture.Apply();

            return texture;
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

            if (_preview != null &&
                _preview.IsRunning)
            {
                _preview.Tick(
                    deltaTime);

                Repaint();
            }
        }
    }
}
