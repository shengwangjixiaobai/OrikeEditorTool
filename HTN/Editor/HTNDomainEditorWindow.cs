using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace Orike.HTN.Editor
{
    /// <summary>
    /// HTN 定义域配置窗口（Unity UI Toolkit，菜单 Orike/HTN Domain）。
    ///
    /// 布局：
    ///   顶部工具栏（定义域资产 / 新建 / 世界状态属性 / 保存）
    ///   左侧结构树（根任务 → 实现方法 → 子任务；未挂接任务）
    ///   右侧属性面板（选中任务 / 方法的详细编辑）
    ///   底部规划测试面板（临时世界状态 → 规划 → 计划 + 分解日志）
    /// </summary>
    public class HTNDomainEditorWindow : EditorWindow, IHTNDomainTreeController
    {
        private const string WindowMenuPath =
            "Orike/HTN Domain";

        /// <summary>窗口样式表：分隔条光标等 UI 样式。</summary>
        private const string UssPath =
            "Assets/Script/OrikeScript/HTN/Editor/HTNEditor.uss";

        /// <summary>分隔条鼠标热区宽度（像素）；分隔条本身透明，不画线。</summary>
        private const float SplitterHitThickness = 10f;

        // 面板尺寸约束
        private const float TreeMinWidth = 200f;

        private const float TreeMaxWidth = 720f;

        private const float InspectorMinWidth = 260f;

        private const float PlanTestMinHeight = 140f;

        private const float PlanTestMaxHeight = 640f;

        private const float DefaultTreeWidth = 420f;

        private const float DefaultPlanTestHeight = 300f;


        private ObjectField _domainField;

        private HTNDomainTree _tree;

        private HTNInspectorPanel _inspector;

        private HTNPlanTestPanel _planTestPanel;

        private VisualElement _content;

        private VisualElement _treePane;

        /// <summary>未加载定义域时显示在结构树位置的提示。</summary>
        private Label _treeHint;

        // 可拖拽的面板尺寸（跨 Play Mode 域重载持久化）
        [SerializeField]
        private float _treeWidth = DefaultTreeWidth;

        [SerializeField]
        private float _planTestHeight = DefaultPlanTestHeight;

        /// <summary>是否正在拖拽分隔条。</summary>
        private bool _isResizing;

        /// <summary>按下分隔条时的鼠标坐标（按拖拽方向取 x 或 y）。</summary>
        private float _resizeStartPosition;

        /// <summary>按下分隔条时被调整面板的尺寸。</summary>
        private float _resizeStartSize;

        // 会话状态（跨 Play Mode 域重载持久化）
        [SerializeField]
        private HTNDomain _domainAsset;

        /// <summary>延迟自动保存计时器（秒）。</summary>
        private float _autoSaveTimer;

        private const float AutoSaveInterval = 2f;

        private double _lastEditorTime;


        public HTNDomain Domain => _domainAsset;


        // =========================================================
        // 打开
        // =========================================================

        [MenuItem(WindowMenuPath)]
        public static void Open()
        {
            HTNDomainEditorWindow window =
                GetWindow<HTNDomainEditorWindow>();

            window.titleContent =
                new GUIContent("HTN Domain");

            window.minSize =
                new Vector2(
                    1100,
                    620);
        }


        // =========================================================
        // 生命周期
        // =========================================================

        private void CreateGUI()
        {
            ApplyStyleSheet();

            BuildToolbar();

            BuildContent();

            RestoreState();

            EditorApplication.update += OnEditorUpdate;

            _lastEditorTime =
                EditorApplication.timeSinceStartup;
        }


        /// <summary>
        /// 加载 HTNEditor.uss（分隔条光标等样式）。
        /// </summary>
        private void ApplyStyleSheet()
        {
            StyleSheet styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    UssPath);

            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(
                    styleSheet);
            }
        }


        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }


        private void BuildToolbar()
        {
            VisualElement toolbar =
                new VisualElement();

            toolbar.style.height = 34;

            toolbar.style.flexShrink = 0f;

            toolbar.style.flexDirection =
                FlexDirection.Row;

            toolbar.style.alignItems =
                Align.Center;

            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;

            toolbar.style.backgroundColor =
                new Color(0.1f, 0.1f, 0.1f);

            toolbar.style.borderBottomWidth = 1;

            toolbar.style.borderBottomColor =
                new Color(0.05f, 0.05f, 0.05f);

            rootVisualElement.Add(toolbar);


            Label label =
                new Label("定义域");

            label.style.width = 48;

            toolbar.Add(label);

            _domainField =
                new ObjectField
                {
                    objectType = typeof(HTNDomain),
                    allowSceneObjects = false,
                };

            _domainField.style.width = 260;

            _domainField.RegisterValueChangedCallback(evt =>
            {
                _domainAsset =
                    evt.newValue as HTNDomain;

                LoadDomain();
            });

            toolbar.Add(_domainField);


            toolbar.Add(
                BuildToolbarButton(
                    "新建",
                    60,
                    OnClickNewDomain));

            toolbar.Add(
                BuildToolbarButton(
                    "世界状态属性…",
                    110,
                    OnClickWorldState));

            toolbar.Add(
                BuildToolbarButton(
                    "添加复合任务",
                    96,
                    () => RequestAddCompoundTask()));

            toolbar.Add(
                BuildToolbarButton(
                    "添加基元任务",
                    96,
                    () => RequestAddPrimitiveTask()));


            VisualElement spacer =
                new VisualElement();

            spacer.style.flexGrow = 1f;

            toolbar.Add(spacer);

            toolbar.Add(
                BuildToolbarButton(
                    "保存",
                    60,
                    OnClickSave));
        }


        private Button BuildToolbarButton(
            string text,
            float width,
            System.Action onClick)
        {
            Button button =
                new Button(onClick)
                {
                    text = text,
                };

            button.style.width = width;

            button.style.height = 24;

            button.style.marginLeft = 4;

            button.style.marginRight = 4;

            return button;
        }


        private void BuildContent()
        {
            _content =
                new VisualElement();

            _content.style.flexGrow = 1f;

            _content.style.flexDirection =
                FlexDirection.Row;

            rootVisualElement.Add(_content);


            // 左上：结构树（宽度可拖拽；行数超出高度时由 TreeView 滚动）
            _treePane =
                new VisualElement();

            _treePane.style.width = _treeWidth;

            _treePane.style.flexShrink = 0f;

            _treePane.style.overflow =
                Overflow.Hidden;

            _content.Add(_treePane);

            // 未加载定义域时的占位提示
            _treeHint =
                new Label(
                    "未加载定义域\n\n" +
                    "点工具栏「新建」，或先用菜单\n" +
                    "Orike/HTN/生成树神示例域 生成示例，\n" +
                    "再拖入左上角「定义域」字段\n\n" +
                    "空白处右键：添加任务 / 世界状态属性\n" +
                    "左键按住拖拽行：调整子任务与方法");

            _treeHint.style.whiteSpace =
                WhiteSpace.Normal;

            _treeHint.style.color =
                new Color(0.55f, 0.55f, 0.55f);

            _treeHint.style.marginTop = 24;

            _treeHint.style.marginLeft = 12;

            _treeHint.style.display =
                DisplayStyle.None;

            _treePane.Add(_treeHint);

            _tree =
                new HTNDomainTree(this);

            _treePane.Add(_tree);


            // 左右分隔条：拖拽调整结构树 / 属性面板的宽度分配
            _content.Add(
                CreateSplitter(true));


            // 右上：属性面板（占满剩余宽度；内容超高时由内置滚动条滚动）
            _inspector =
                new HTNInspectorPanel(this);

            _content.Add(_inspector);


            // 上下分隔条：拖拽调整内容区 / 规划测试面板的高度分配
            rootVisualElement.Add(
                CreateSplitter(false));


            // 底部规划测试（高度可拖拽）
            _planTestPanel =
                new HTNPlanTestPanel(this);

            _planTestPanel.style.height = _planTestHeight;

            rootVisualElement.Add(_planTestPanel);

            rootVisualElement.style.flexDirection =
                FlexDirection.Column;
        }


        // =========================================================
        // 分隔条：拖拽调整面板尺寸
        // =========================================================

        /// <summary>
        /// 创建分隔条。
        ///
        /// 分隔条本身不画任何东西（背景透明），面板之间的细线仍然由
        /// 属性面板的 1px 左边框 / 规划测试面板的 1px 上边框提供，
        /// 外观与加分隔条之前完全一致；这里只提供一块 10px 宽的鼠标热区，
        /// 让光标能变成双向箭头并可以拖拽。
        /// </summary>
        /// <param name="leftRight">
        /// true：结构树 / 属性面板之间的竖条，左右拖拽；
        /// false：内容区 / 规划测试面板之间的横条，上下拖拽。
        /// </param>
        private VisualElement CreateSplitter(bool leftRight)
        {
            VisualElement splitter =
                new VisualElement();

            splitter.style.flexShrink = 0f;

            splitter.style.backgroundColor =
                new Color(0f, 0f, 0f, 0f);

            if (leftRight)
            {
                splitter.style.width = SplitterHitThickness;

                // 水平双向箭头光标（HTNEditor.uss）
                splitter.AddToClassList(
                    "htn-splitter--left-right");
            }
            else
            {
                splitter.style.height = SplitterHitThickness;

                // 垂直双向箭头光标（HTNEditor.uss）
                splitter.AddToClassList(
                    "htn-splitter--top-bottom");
            }

            splitter.RegisterCallback<PointerDownEvent>(
                evt => OnSplitterPointerDown(evt, leftRight));

            splitter.RegisterCallback<PointerMoveEvent>(
                evt => OnSplitterPointerMove(evt, leftRight));

            splitter.RegisterCallback<PointerUpEvent>(
                OnSplitterPointerUp);

            return splitter;
        }


        private void OnSplitterPointerDown(
            PointerDownEvent evt,
            bool leftRight)
        {
            if (evt.button != 0)
            {
                return;
            }

            _isResizing = true;

            _resizeStartPosition =
                leftRight
                    ? evt.position.x
                    : evt.position.y;

            _resizeStartSize =
                leftRight
                    ? _treePane.resolvedStyle.width
                    : _planTestPanel.resolvedStyle.height;

            if (evt.currentTarget is VisualElement splitter)
            {
                splitter.CapturePointer(
                    evt.pointerId);
            }

            evt.StopPropagation();
        }


        private void OnSplitterPointerMove(
            PointerMoveEvent evt,
            bool leftRight)
        {
            if (!_isResizing)
            {
                return;
            }

            if (!(evt.currentTarget is VisualElement splitter) ||
                !splitter.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            if (leftRight)
            {
                // 分隔条右移 → 结构树变宽，属性面板相应变窄
                float delta =
                    evt.position.x - _resizeStartPosition;

                // 右侧至少保留 InspectorMinWidth
                float maxWidth =
                    Mathf.Min(
                        TreeMaxWidth,
                        _content.resolvedStyle.width -
                        InspectorMinWidth -
                        SplitterHitThickness);

                _treeWidth =
                    Mathf.Clamp(
                        _resizeStartSize + delta,
                        TreeMinWidth,
                        Mathf.Max(TreeMinWidth, maxWidth));

                _treePane.style.width = _treeWidth;
            }
            else
            {
                // 分隔条下移 → 规划测试面板变矮
                float delta =
                    evt.position.y - _resizeStartPosition;

                _planTestHeight =
                    Mathf.Clamp(
                        _resizeStartSize - delta,
                        PlanTestMinHeight,
                        PlanTestMaxHeight);

                _planTestPanel.style.height = _planTestHeight;
            }

            evt.StopPropagation();
        }


        private void OnSplitterPointerUp(
            PointerUpEvent evt)
        {
            if (!_isResizing)
            {
                return;
            }

            _isResizing = false;

            if (evt.currentTarget is VisualElement splitter)
            {
                splitter.ReleasePointer(
                    evt.pointerId);
            }

            evt.StopPropagation();
        }


        private void RestoreState()
        {
            if (_domainAsset != null)
            {
                _domainField.SetValueWithoutNotify(
                    _domainAsset);

                LoadDomain();
            }
        }


        // =========================================================
        // 加载 / 保存
        // =========================================================

        private void LoadDomain()
        {
            if (_domainAsset != null)
            {
                _domainAsset.RemoveNullReferences();
            }

            _treeHint.style.display =
                _domainAsset != null
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;

            _tree.RebuildTree();

            _inspector.Show(null);

            _planTestPanel.RebuildStateFields();
        }


        private void OnClickNewDomain()
        {
            HTNDomain domain =
                HTNDomainAssetOps.CreateDomainAsset();

            if (domain != null)
            {
                _domainField.value = domain;
            }
        }


        private void OnClickWorldState()
        {
            if (Domain == null)
            {
                ShowNoDomainWarning();

                return;
            }

            HTNWorldStateWindow.Open(
                Domain,
                () =>
                {
                    _planTestPanel.RebuildStateFields();

                    _inspector.Refresh();
                });
        }


        private void OnClickSave()
        {
            if (Domain == null)
            {
                ShowNoDomainWarning();

                return;
            }

            EditorUtility.SetDirty(Domain);

            foreach (HTNTask task in Domain.Tasks)
            {
                if (task != null)
                {
                    EditorUtility.SetDirty(task);
                }
            }

            AssetDatabase.SaveAssets();

            ShowNotification(
                new GUIContent("HTN 定义域已保存"));
        }


        private void ShowNoDomainWarning()
        {
            ShowNotification(
                new GUIContent("请先新建或指定一个 HTN 定义域"));
        }


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

            // 延迟自动保存：停止编辑 2 秒后统一写盘
            if (Domain != null)
            {
                _autoSaveTimer += deltaTime;

                if (_autoSaveTimer >= AutoSaveInterval)
                {
                    _autoSaveTimer = 0f;

                    AssetDatabase.SaveAssetIfDirty(Domain);
                }
            }
        }


        // =========================================================
        // 修改标记（供子面板调用）
        // =========================================================

        /// <summary>数据内容修改：只标脏 + 刷新 Inspector 显示。</summary>
        public void MarkDirty()
        {
            if (Domain == null)
            {
                return;
            }

            EditorUtility.SetDirty(Domain);

            _autoSaveTimer = 0f;
        }


        /// <summary>
        /// 结构修改（任务 / 方法 / 子任务增删、重命名）：
        /// 标脏 + 重建树（保持选中）+ 刷新 Inspector。
        /// </summary>
        public void MarkStructureChanged()
        {
            if (Domain == null)
            {
                return;
            }

            EditorUtility.SetDirty(Domain);

            _autoSaveTimer = 0f;

            _tree.RebuildTree();

            _inspector.Refresh();
        }


        // =========================================================
        // IHTNDomainTreeController
        // =========================================================

        public void OnTreeSelectionChanged(object item)
        {
            _inspector.Show(item);
        }


        public void RequestAddCompoundTask()
        {
            if (Domain == null)
            {
                ShowNoDomainWarning();

                return;
            }

            HTNCompoundTask task =
                HTNDomainAssetOps.CreateCompoundTask(Domain);

            if (task != null)
            {
                MarkStructureChanged();

                _tree.SelectTask(task);

                _inspector.Show(task);
            }
        }


        public void RequestAddPrimitiveTask()
        {
            if (Domain == null)
            {
                ShowNoDomainWarning();

                return;
            }

            HTNPrimitiveTask task =
                HTNDomainAssetOps.CreatePrimitiveTask(Domain);

            if (task != null)
            {
                MarkStructureChanged();

                _tree.SelectTask(task);

                _inspector.Show(task);
            }
        }


        public void RequestDeleteTask(HTNTask task)
        {
            if (Domain == null ||
                task == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "删除任务",
                    $"确定删除任务 “{task.name}”？\n所有方法中对它的引用也会一并移除。",
                    "删除",
                    "取消"))
            {
                return;
            }

            HTNDomainAssetOps.DeleteTask(
                Domain,
                task);

            _inspector.Show(null);

            MarkStructureChanged();
        }


        public void RequestSetRoot(HTNTask task)
        {
            if (Domain == null)
            {
                return;
            }

            HTNDomainAssetOps.SetRootTask(
                Domain,
                task);

            MarkStructureChanged();
        }


        public void RequestRenameTask(HTNTask task)
        {
            if (task == null)
            {
                return;
            }

            string newName =
                EditorInputDialog.Show(
                    "重命名任务",
                    "新任务名",
                    task.name);

            if (!string.IsNullOrEmpty(newName))
            {
                HTNDomainAssetOps.RenameTask(
                    task,
                    newName);

                MarkStructureChanged();
            }
        }


        public void RequestAddSubTask(HTNMethodRef methodRef)
        {
            if (methodRef?.Method == null)
            {
                return;
            }

            // 弹出域内任务选择
            GenericMenu menu =
                new GenericMenu();

            foreach (HTNTask task in Domain.Tasks)
            {
                if (task == null)
                {
                    continue;
                }

                HTNTask captured = task;

                menu.AddItem(
                    new GUIContent(
                        $"{(task.IsCompound ? "复合/" : "基元/")}{task.name}"),
                    false,
                    () =>
                    {
                        Undo.RecordObject(
                            methodRef.Owner,
                            "Add HTN SubTask");

                        methodRef.Method.SubTasks.Add(captured);

                        MarkStructureChanged();
                    });
            }

            menu.AddSeparator("");

            menu.AddItem(
                new GUIContent("＋ 新建基元任务"),
                false,
                () => CreateSubTask(
                    methodRef.Owner,
                    methodRef.Method,
                    true));

            menu.AddItem(
                new GUIContent("＋ 新建复合任务"),
                false,
                () => CreateSubTask(
                    methodRef.Owner,
                    methodRef.Method,
                    false));

            menu.ShowAsContext();
        }


        public void RequestDeleteMethod(HTNMethodRef methodRef)
        {
            DeleteMethod(methodRef);
        }


        public void RequestMoveMethod(
            HTNMethodRef methodRef,
            int delta)
        {
            HTNCompoundTask owner = methodRef?.Owner;

            if (owner == null ||
                methodRef.Method == null)
            {
                return;
            }

            int target =
                methodRef.Index + delta;

            if (target < 0 ||
                target >= owner.Methods.Count)
            {
                return;
            }

            Undo.RecordObject(owner, "Reorder HTN Methods");

            owner.Methods.RemoveAt(methodRef.Index);

            owner.Methods.Insert(
                target,
                methodRef.Method);

            MarkStructureChanged();
        }


        public void RequestOpenWorldStateWindow()
        {
            OnClickWorldState();
        }


        public void RequestTreeDrop(object draggedItem, object targetItem)
        {
            if (Domain == null ||
                draggedItem == null ||
                targetItem == null)
            {
                return;
            }


            // ---------------------------------------------------------
            // 方法 → 方法：调整实现方法优先级（同一复合任务内）
            // ---------------------------------------------------------
            if (draggedItem is HTNMethodRef sourceMethod)
            {
                if (targetItem is HTNMethodRef targetMethod &&
                    targetMethod.Owner != null &&
                    ReferenceEquals(targetMethod.Owner, sourceMethod.Owner) &&
                    targetMethod.Index != sourceMethod.Index &&
                    sourceMethod.Index >= 0 &&
                    sourceMethod.Index < sourceMethod.Owner.Methods.Count &&
                    targetMethod.Index >= 0 &&
                    targetMethod.Index < sourceMethod.Owner.Methods.Count)
                {
                    Undo.RecordObject(
                        sourceMethod.Owner,
                        "Reorder HTN Methods");

                    HTNMethod moved =
                        sourceMethod.Owner.Methods[sourceMethod.Index];

                    sourceMethod.Owner.Methods.RemoveAt(
                        sourceMethod.Index);

                    // 先移除再插入：目标在源之后时索引左移一位
                    int insertAt =
                        targetMethod.Index > sourceMethod.Index
                            ? targetMethod.Index - 1
                            : targetMethod.Index;

                    sourceMethod.Owner.Methods.Insert(
                        insertAt,
                        moved);

                    MarkStructureChanged();
                }

                return;
            }


            // ---------------------------------------------------------
            // 任务来源：子任务出现行（带位置）或顶层任务行
            // ---------------------------------------------------------
            HTNTask task;
            HTNCompoundTask fromOwner;
            HTNMethod fromMethod;
            int fromIndex;

            if (draggedItem is HTNTaskRow sourceRow)
            {
                task = sourceRow.Task;
                fromOwner = sourceRow.ParentOwner;
                fromMethod = sourceRow.ParentMethod;
                fromIndex = sourceRow.SubIndex;
            }
            else if (draggedItem is HTNTask topLevelTask)
            {
                task = topLevelTask;
                fromOwner = null;
                fromMethod = null;
                fromIndex = -1;
            }
            else
            {
                return;
            }

            if (task == null)
            {
                return;
            }


            // ---------------------------------------------------------
            // 投放目标解析：
            //   方法行           → 追加为该方法子任务（末尾）
            //   复合任务行        → 放进它的方法0末尾（无方法则自动新建）
            //   基元任务出现行    → 插入到目标位置
    //     （同列表内：向上拖 = 目标之前，向下拖 = 目标之后）
            //   顶层基元任务行    → 无效，忽略
            // ---------------------------------------------------------
            HTNCompoundTask toOwner;
            HTNMethod toMethod;
            int insertIndex;

            if (targetItem is HTNMethodRef dropOnMethod)
            {
                toOwner =
                    dropOnMethod.Owner;

                toMethod =
                    dropOnMethod.Method;

                if (toMethod == null)
                {
                    toMethod =
                        EnsureFirstMethod(toOwner);
                }

                insertIndex =
                    toMethod != null
                        ? toMethod.SubTasks.Count
                        : 0;
            }
            else if (targetItem is HTNTaskRow dropOnRow)
            {
                if (dropOnRow.Task is HTNCompoundTask targetCompound)
                {
                    // 拖到复合任务上：放进它的方法0 末尾
                    toOwner =
                        targetCompound;

                    toMethod =
                        EnsureFirstMethod(targetCompound);

                    insertIndex =
                        toMethod != null
                            ? toMethod.SubTasks.Count
                            : 0;
                }
                else if (dropOnRow.ParentMethod != null)
                {
                    // 基元任务出现行：插入到目标位置
                    toOwner =
                        dropOnRow.ParentOwner;

                    toMethod =
                        dropOnRow.ParentMethod;

                    insertIndex =
                        dropOnRow.SubIndex;
                }
                else
                {
                    return;
                }
            }
            else if (targetItem is HTNTask topLevelTarget &&
                     topLevelTarget is HTNCompoundTask topCompound)
            {
                // 拖到顶层复合任务行上：放进它的方法0 末尾
                toOwner =
                    topCompound;

                toMethod =
                    EnsureFirstMethod(topCompound);

                insertIndex =
                    toMethod != null
                        ? toMethod.SubTasks.Count
                        : 0;
            }
            else
            {
                return;
            }

            if (toOwner == null ||
                toMethod == null)
            {
                return;
            }

            bool sameList =
                fromMethod != null &&
                ReferenceEquals(fromMethod, toMethod);

            if (sameList &&
                fromIndex == insertIndex)
            {
                return;
            }

            if (fromMethod != null)
            {
                if (fromIndex < 0 ||
                    fromIndex >= fromMethod.SubTasks.Count ||
                    !ReferenceEquals(fromMethod.SubTasks[fromIndex], task))
                {
                    // 来源位置已经变化，放弃移动避免错删
                    return;
                }

                Undo.RecordObject(
                    fromOwner,
                    "Move HTN SubTask");

                fromMethod.SubTasks.RemoveAt(fromIndex);
            }

            if (!sameList)
            {
                Undo.RecordObject(
                    toOwner,
                    "Add HTN SubTask");
            }

            // 同列表重排：先移除再按目标原始下标插入——
            // 向上拖落在目标之前、向下拖落在目标之后，两个方向都正确；
            // “追加到末尾”的目标下标可能超过移除后的长度，收敛一下
            insertIndex =
                Mathf.Clamp(
                    insertIndex,
                    0,
                    toMethod.SubTasks.Count);

            toMethod.SubTasks.Insert(
                insertIndex,
                task);

            MarkStructureChanged();
        }


        /// <summary>
        /// 取复合任务的第一个实现方法（方法0）；没有方法时自动新建。
        /// </summary>
        private static HTNMethod EnsureFirstMethod(HTNCompoundTask owner)
        {
            if (owner == null)
            {
                return null;
            }

            if (owner.Methods.Count == 0)
            {
                Undo.RecordObject(
                    owner,
                    "Add HTN Method");

                owner.Methods.Add(new HTNMethod());

                EditorUtility.SetDirty(owner);
            }

            return owner.Methods[0];
        }


        // =========================================================
        // 方法 / 子任务操作（供 Inspector 调用）
        // =========================================================

        public void AddMethod(HTNCompoundTask owner)
        {
            if (owner == null)
            {
                return;
            }

            Undo.RecordObject(owner, "Add HTN Method");

            owner.Methods.Add(new HTNMethod());

            MarkStructureChanged();
        }


        public void DeleteMethod(HTNMethodRef methodRef)
        {
            HTNCompoundTask owner = methodRef?.Owner;

            if (owner == null ||
                methodRef.Method == null ||
                methodRef.Index < 0 ||
                methodRef.Index >= owner.Methods.Count)
            {
                return;
            }

            Undo.RecordObject(owner, "Delete HTN Method");

            owner.Methods.RemoveAt(methodRef.Index);

            MarkStructureChanged();
        }


        public void CreateSubTask(
            HTNCompoundTask owner,
            HTNMethod method,
            bool primitive)
        {
            if (owner == null ||
                method == null ||
                Domain == null)
            {
                return;
            }

            HTNTask task =
                primitive
                    ? HTNDomainAssetOps.CreatePrimitiveTask(Domain)
                    : (HTNTask)HTNDomainAssetOps.CreateCompoundTask(Domain);

            if (task == null)
            {
                return;
            }

            Undo.RecordObject(owner, "Add HTN SubTask");

            method.SubTasks.Add(task);

            MarkStructureChanged();

            _tree.SelectTask(task);

            _inspector.Show(task);
        }
    }


    /// <summary>
    /// 简单的字符串输入对话框（EditorWindow 版）。
    /// </summary>
    internal static class EditorInputDialog
    {
        /// <summary>
        /// 弹出输入框；返回 null 表示取消。
        /// </summary>
        public static string Show(
            string title,
            string label,
            string defaultValue)
        {
            string result =
                EditorInputDialogWindow.ShowAndGetResult(
                    title,
                    label,
                    defaultValue);

            return result;
        }


        private class EditorInputDialogWindow : EditorWindow
        {
            private string _value;

            private string _label;

            private bool _closed;

            private bool _cancelled;


            public static string ShowAndGetResult(
                string title,
                string label,
                string defaultValue)
            {
                EditorInputDialogWindow dialog =
                    CreateInstance<EditorInputDialogWindow>();

                dialog.titleContent =
                    new GUIContent(title);

                dialog._label = label;

                dialog._value = defaultValue;

                dialog.minSize =
                    new Vector2(420, 80);

                dialog.maxSize =
                    new Vector2(420, 80);

                dialog.ShowModal();

                return dialog._cancelled
                    ? null
                    : dialog._value;
            }


            private void OnGUI()
            {
                if (_closed)
                {
                    return;
                }

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(
                    _label,
                    GUILayout.Width(80));

                _value =
                    EditorGUILayout.TextField(_value);

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(
                        "取消",
                        GUILayout.Width(64)))
                {
                    _cancelled = true;

                    _closed = true;

                    Close();
                }

                if (GUILayout.Button(
                        "确定",
                        GUILayout.Width(64)) ||
                    (Event.current != null &&
                     Event.current.keyCode == KeyCode.Return))
                {
                    _closed = true;

                    Close();
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
