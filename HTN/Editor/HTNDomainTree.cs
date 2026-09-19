using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orike.HTN.Editor
{
    /// <summary>
    /// 方法与其所属复合任务的引用（树的选中项之一，编辑用）。
    /// </summary>
    public class HTNMethodRef
    {
        /// <summary>方法所属的复合任务（Undo 记录对象）。</summary>
        public HTNCompoundTask Owner;

        /// <summary>方法本体。</summary>
        public HTNMethod Method;

        /// <summary>方法在 Owner.Methods 中的索引（优先级顺序）。</summary>
        public int Index;
    }


    /// <summary>
    /// 子任务出现位置（树上的一次子任务引用）。
    /// 同一个任务可能被多个方法引用（包括递归），
    /// 拖拽 / 定位插入需要知道它出现在哪个方法、第几个。
    /// </summary>
    public class HTNTaskRow
    {
        /// <summary>引用的任务本体。</summary>
        public HTNTask Task;

        /// <summary>所在方法所属的复合任务（顶层任务行此值为 null）。</summary>
        public HTNCompoundTask ParentOwner;

        /// <summary>所在方法（顶层任务行此值为 null）。</summary>
        public HTNMethod ParentMethod;

        /// <summary>在 ParentMethod.SubTasks 中的下标。</summary>
        public int SubIndex;

        /// <summary>是否为递归引用叶子（不再展开）。</summary>
        public bool IsRecursiveReference;
    }


    /// <summary>
    /// 树编辑器的控制器接口：由 HTNDomainEditorWindow 实现，
    /// 树的所有修改动作都转发给它（窗口统一负责 Undo / 刷新 / 自动保存）。
    /// </summary>
    public interface IHTNDomainTreeController
    {
        HTNDomain Domain { get; }

        void OnTreeSelectionChanged(object item);

        void RequestAddCompoundTask();

        void RequestAddPrimitiveTask();

        void RequestDeleteTask(HTNTask task);

        void RequestSetRoot(HTNTask task);

        void RequestRenameTask(HTNTask task);

        void RequestAddSubTask(HTNMethodRef methodRef);

        void RequestDeleteMethod(HTNMethodRef methodRef);

        void RequestMoveMethod(HTNMethodRef methodRef, int delta);

        void RequestOpenWorldStateWindow();

        /// <summary>
        /// 拖拽投放：draggedItem 为 HTNTask / HTNTaskRow / HTNMethodRef，
        /// targetItem 为 HTNMethodRef（追加 / 移动到该方法末尾）或
        /// HTNTaskRow（插入到该子任务位置）。
        /// </summary>
        void RequestTreeDrop(object draggedItem, object targetItem);
    }


    /// <summary>
    /// 域结构树（UI Toolkit TreeView）：
    ///
    ///   ★ 根任务
    ///   └ 方法0：发现敌人 ｜ WsCanSeeEnemy == 1
    ///     └〔基元〕NavigateToEnemy
    ///     └〔复合〕AttackEnemy
    ///       └ ↻ AttackEnemy（递归引用显示为叶子）
    ///   未挂接任务
    ///   └〔复合〕XXX（尚未被任何方法引用）
    ///
    /// 交互：
    ///   - 左键点击行 → 右侧 Inspector 显示对应任务 / 方法；
    ///   - 右键行 / 空白 → 上下文菜单（设根 / 重命名 / 删除 / 调优先级 / 加子任务）；
    ///   - 左键按住拖拽：
    ///       任务 → 方法行     ：把任务添加 / 移动为该方法子任务（追加末尾）
    ///       任务 → 子任务行   ：插入到该子任务之前（同列表内即重排）
    ///       方法 → 方法行     ：调整实现方法优先级顺序
    /// </summary>
    public class HTNDomainTree : TreeView
    {
        private readonly IHTNDomainTreeController _controller;

        /// <summary>树项 id → 数据对象（HTNTask / HTNTaskRow / HTNMethodRef）。</summary>
        private readonly List<object> _items =
            new List<object>();

        /// <summary>从根任务可达的所有任务（用于划分“未挂接任务”区）。</summary>
        private readonly HashSet<HTNTask> _coveredTasks =
            new HashSet<HTNTask>();

        /// <summary>
        /// 折叠状态（按任务 / 方法实例记录，重建树后恢复），
        /// 避免每次结构变化后整棵树全部展开。
        /// </summary>
        private readonly HashSet<object> _collapsedKeys =
            new HashSet<object>();


        public HTNDomainTree(IHTNDomainTreeController controller)
        {
            _controller = controller;

            makeItem =
                MakeRow;

            bindItem =
                BindRow;

            // 展开状态由 RebuildTree 手工维护（记录 / 恢复），
            // 不用 autoExpand 整体展开
            autoExpand = false;

            selectionType = SelectionType.Single;

            style.flexGrow = 1f;

            selectionChanged += OnSelectionChanged;

            // 右键菜单：PointerDown 记录命中的行，PointerUp（右键抬起）时
            // 用 GenericMenu.ShowAsContext 弹出（不依赖 Manipulator，
            // Unity 6 已移除 AddManipulator）

            // 右键按下时先清空上下文目标（冒泡阶段行回调会重新设置）
            RegisterCallback<PointerDownEvent>(
                OnTreePointerDown,
                TrickleDown.TrickleDown);

            // 左键拖拽：按下记录候选行，移动超阈值后进入拖拽
            RegisterCallback<PointerDownEvent>(OnRowPointerDown);
            RegisterCallback<PointerMoveEvent>(OnTreePointerMove);
            RegisterCallback<PointerUpEvent>(OnTreePointerUp);
            RegisterCallback<PointerCancelEvent>(OnTreePointerCancel);
        }


        // =========================================================
        // 数据构建
        // =========================================================

        /// <summary>
        /// 重建整棵树（域切换 / 结构变化后调用）。
        /// 重建前记录当前折叠状态，重建后逐行恢复，
        /// 保证拖拽 / 编辑不会把整棵树重新展开。
        /// </summary>
        public void RebuildTree()
        {
            // 先用旧的 _items / 展开状态记录折叠键
            CaptureCollapsedKeys();

            List<TreeViewItemData<int>> roots =
                BuildItems();

            SetRootItems(roots);

            Rebuild();

            RestoreExpandedStates();
        }


        /// <summary>
        /// 记录当前所有折叠的行（键为任务 / 方法实例）。
        /// </summary>
        private void CaptureCollapsedKeys()
        {
            _collapsedKeys.Clear();

            for (int i = 0; i < _items.Count; i++)
            {
                // 没有子行的行谈不上折叠，跳过，
                // 避免空方法 / 基元任务被误记为折叠
                if (!GetChildrenIdsForIndex(i).Any())
                {
                    continue;
                }

                if (!IsExpanded(i))
                {
                    object key =
                        ExpansionKeyOf(_items[i]);

                    if (key != null)
                    {
                        _collapsedKeys.Add(key);
                    }
                }
            }
        }


        /// <summary>
        /// 展开状态的稳定键：任务行按任务实例（同一任务的所有出现共享状态），
        /// 方法行按方法实例（方法重排后状态不变）。
        /// </summary>
        private static object ExpansionKeyOf(object item)
        {
            return item switch
            {
                HTNTaskRow row => row.Task,
                HTNTask task => task,
                HTNMethodRef methodRef => (object)methodRef.Method ?? methodRef.Owner,
                _ => null,
            };
        }


        /// <summary>
        /// 重建后按折叠键恢复每行的展开状态（未记录的默认展开）。
        /// </summary>
        private void RestoreExpandedStates()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                object key =
                    ExpansionKeyOf(_items[i]);

                bool shouldBeExpanded =
                    key == null ||
                    !_collapsedKeys.Contains(key);

                if (shouldBeExpanded)
                {
                    if (!IsExpanded(i))
                    {
                        ExpandItem(i, true, false);
                    }
                }
                else
                {
                    if (IsExpanded(i))
                    {
                        CollapseItem(i, false, false);
                    }
                }
            }
        }


        /// <summary>
        /// 选中并滚动到某个任务的第一个出现位置。
        /// </summary>
        public void SelectTask(HTNTask task)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                object item = _items[i];

                bool match =
                    item == task ||
                    (item is HTNTaskRow row && row.Task == task);

                if (match)
                {
                    SetSelectionByIdWithoutNotify(
                        new[] { i });

                    ScrollToItemById(i);

                    return;
                }
            }
        }


        private List<TreeViewItemData<int>> BuildItems()
        {
            _items.Clear();
            _coveredTasks.Clear();

            List<TreeViewItemData<int>> roots =
                new List<TreeViewItemData<int>>();

            HTNDomain domain = _controller.Domain;

            if (domain == null)
            {
                return roots;
            }

            // 根任务分支
            if (domain.RootTask != null)
            {
                roots.Add(
                    BuildTaskItem(
                        domain.RootTask,
                        new HashSet<HTNTask>()));
            }

            // 未挂接任务：未被根任务层级覆盖的域内任务
            foreach (HTNTask task in domain.Tasks)
            {
                if (task != null &&
                    !_coveredTasks.Contains(task))
                {
                    roots.Add(
                        BuildTaskItem(
                            task,
                            new HashSet<HTNTask>()));
                }
            }

            return roots;
        }


        /// <summary>
        /// 构建一个任务节点（根任务 / 未挂接任务，可展开）。
        /// </summary>
        private TreeViewItemData<int> BuildTaskItem(
            HTNTask task,
            HashSet<HTNTask> path)
        {
            int id = _items.Count;

            _items.Add(task);

            _coveredTasks.Add(task);

            List<TreeViewItemData<int>> children =
                new List<TreeViewItemData<int>>();

            if (task is HTNCompoundTask compound)
            {
                HashSet<HTNTask> childPath =
                    new HashSet<HTNTask>(path);

                childPath.Add(task);

                for (int i = 0; i < compound.Methods.Count; i++)
                {
                    children.Add(
                        BuildMethodItem(
                            compound,
                            i,
                            childPath));
                }
            }

            return new TreeViewItemData<int>(
                id,
                id,
                children);
        }


        /// <summary>
        /// 构建一个方法节点。
        /// </summary>
        private TreeViewItemData<int> BuildMethodItem(
            HTNCompoundTask owner,
            int methodIndex,
            HashSet<HTNTask> path)
        {
            HTNMethod method = owner.Methods[methodIndex];

            int id = _items.Count;

            _items.Add(new HTNMethodRef
            {
                Owner = owner,
                Method = method,
                Index = methodIndex,
            });

            List<TreeViewItemData<int>> children =
                new List<TreeViewItemData<int>>();

            if (method != null)
            {
                for (int i = 0; i < method.SubTasks.Count; i++)
                {
                    HTNTask subTask = method.SubTasks[i];

                    if (subTask == null)
                    {
                        continue;
                    }

                    if (path.Contains(subTask))
                    {
                        // 递归 / 祖先链上的引用：叶子展示，不再展开
                        children.Add(
                            BuildRecursiveLeafItem(
                                subTask,
                                owner,
                                method,
                                i));
                    }
                    else
                    {
                        children.Add(
                            BuildSubTaskItem(
                                subTask,
                                owner,
                                method,
                                i,
                                path));
                    }
                }
            }

            return new TreeViewItemData<int>(
                id,
                id,
                children);
        }


        /// <summary>
        /// 构建一个子任务出现节点（携带位置信息，可继续展开）。
        /// </summary>
        private TreeViewItemData<int> BuildSubTaskItem(
            HTNTask task,
            HTNCompoundTask owner,
            HTNMethod method,
            int subIndex,
            HashSet<HTNTask> path)
        {
            int id = _items.Count;

            _items.Add(new HTNTaskRow
            {
                Task = task,
                ParentOwner = owner,
                ParentMethod = method,
                SubIndex = subIndex,
            });

            _coveredTasks.Add(task);

            List<TreeViewItemData<int>> children =
                new List<TreeViewItemData<int>>();

            if (task is HTNCompoundTask compound)
            {
                HashSet<HTNTask> childPath =
                    new HashSet<HTNTask>(path);

                childPath.Add(task);

                for (int i = 0; i < compound.Methods.Count; i++)
                {
                    children.Add(
                        BuildMethodItem(
                            compound,
                            i,
                            childPath));
                }
            }

            return new TreeViewItemData<int>(
                id,
                id,
                children);
        }


        private TreeViewItemData<int> BuildRecursiveLeafItem(
            HTNTask task,
            HTNCompoundTask owner,
            HTNMethod method,
            int subIndex)
        {
            int id = _items.Count;

            _items.Add(new HTNTaskRow
            {
                Task = task,
                ParentOwner = owner,
                ParentMethod = method,
                SubIndex = subIndex,
                IsRecursiveReference = true,
            });

            _coveredTasks.Add(task);

            // children 传 null：递归引用显示为不可展开的叶子
            return new TreeViewItemData<int>(
                id,
                id,
                null);
        }


        // =========================================================
        // 行 visuals
        // =========================================================

        private VisualElement MakeRow()
        {
            VisualElement row =
                new VisualElement();

            row.style.flexDirection =
                FlexDirection.Row;

            row.style.alignItems =
                Align.Center;

            row.style.paddingLeft = 2;
            row.style.paddingRight = 4;

            Label badge =
                new Label();

            badge.style.width = 42;
            badge.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            badge.style.fontSize = 10;

            row.Add(badge);

            Label name =
                new Label();

            name.style.flexGrow = 1f;
            name.style.overflow = Overflow.Hidden;
            name.style.textOverflow = TextOverflow.Ellipsis;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;

            row.Add(name);

            row.userData = null;

            return row;
        }


        private void BindRow(
            VisualElement row,
            int index)
        {
            // GetItemDataForIndex<T> 直接返回数据载荷（这里是 _items 的下标）
            int dataIndex =
                GetItemDataForIndex<int>(index);

            object data =
                dataIndex >= 0 && dataIndex < _items.Count
                    ? _items[dataIndex]
                    : null;

            row.userData = data;

            // 行会被回收复用，先清掉拖拽高亮
            row.style.backgroundColor =
                StyleKeyword.Null;

            Label badge =
                row.ElementAt(0) as Label;

            Label name =
                row.ElementAt(1) as Label;

            if (badge == null ||
                name == null)
            {
                return;
            }


            switch (data)
            {
                case HTNCompoundTask compoundTask:
                {
                    badge.text =
                        "复合";
                    badge.style.color =
                        new Color(0.55f, 0.75f, 1f);

                    name.text =
                        (compoundTask == _controller.Domain?.RootTask
                            ? "★ "
                            : "") +
                        compoundTask.name;

                    name.style.color =
                        new Color(0.92f, 0.92f, 0.92f);

                    name.style.unityFontStyleAndWeight =
                        FontStyle.Normal;

                    break;
                }

                case HTNPrimitiveTask primitiveTask:
                {
                    badge.text =
                        "基元";
                    badge.style.color =
                        new Color(0.45f, 0.85f, 0.65f);

                    name.text =
                        primitiveTask.name;

                    name.style.color =
                        new Color(0.92f, 0.92f, 0.92f);

                    name.style.unityFontStyleAndWeight =
                        FontStyle.Normal;

                    break;
                }

                case HTNTaskRow taskRow:
                {
                    HTNTask task = taskRow.Task;

                    if (task == null)
                    {
                        badge.text = "";
                        name.text = "";

                        break;
                    }

                    badge.text =
                        task.IsCompound ? "复合" : "基元";
                    badge.style.color =
                        task.IsCompound
                            ? new Color(0.55f, 0.75f, 1f)
                            : new Color(0.45f, 0.85f, 0.65f);

                    name.text =
                        (taskRow.IsRecursiveReference
                            ? "↻ "
                            : "") +
                        task.name;

                    name.style.color =
                        taskRow.IsRecursiveReference
                            ? new Color(0.6f, 0.6f, 0.6f)
                            : new Color(0.92f, 0.92f, 0.92f);

                    name.style.unityFontStyleAndWeight =
                        FontStyle.Normal;

                    break;
                }

                case HTNMethodRef methodRef:
                {
                    HTNMethod method = methodRef.Method;

                    badge.text =
                        "方法";
                    badge.style.color =
                        new Color(1f, 0.8f, 0.45f);

                    string conditions =
                        method != null
                            ? method.DescribeConditions()
                            : "";

                    name.text =
                        $"方法{methodRef.Index}" +
                        (method != null &&
                         !string.IsNullOrEmpty(method.MethodName)
                            ? $"：{method.MethodName}"
                            : "") +
                        $" ｜ {conditions}";

                    name.style.color =
                        new Color(0.75f, 0.75f, 0.75f);

                    name.style.unityFontStyleAndWeight =
                        FontStyle.Normal;

                    break;
                }

                default:
                {
                    badge.text = "";
                    name.text = "";

                    break;
                }
            }
        }


        // =========================================================
        // 选中 / 右键菜单
        // =========================================================

        private void OnSelectionChanged(
            IEnumerable<object> selectedItems)
        {
            foreach (object payload in selectedItems)
            {
                // 载荷可能是数据本身（int），也可能被包装成 TreeViewItemData
                int id = -1;

                if (payload is int direct)
                {
                    id = direct;
                }
                else if (payload is TreeViewItemData<int> wrapped)
                {
                    id = wrapped.data;
                }

                if (id >= 0 &&
                    id < _items.Count)
                {
                    object item = _items[id];

                    // 子任务出现行按其任务本体展示
                    if (item is HTNTaskRow taskRow)
                    {
                        item =
                            taskRow.Task;
                    }

                    _controller.OnTreeSelectionChanged(item);

                    return;
                }
            }
        }


        /// <summary>当前右键命中的行数据（null 表示空白处）。</summary>
        private object _contextTarget;


        private void OnTreePointerDown(PointerDownEvent evt)
        {
            // 捕获阶段先清空，行回调（冒泡阶段随后执行）再覆盖
            if (evt.button == 1)
            {
                _contextTarget = null;
            }
        }


        private void OnRowPointerDown(PointerDownEvent evt)
        {
            // 回调注册在树上，evt.currentTarget 是树本身；
            // 从实际命中元素向上回溯找到带行数据的行
            if (evt.button == 1)
            {
                _contextTarget =
                    FindRowData(evt.target as VisualElement);
            }
            else if (evt.button == 0)
            {
                object source =
                    FindRowData(evt.target as VisualElement);

                _press = source != null
                    ? new PressState
                    {
                        Source = source,
                        StartPosition = evt.position,
                        PointerId = evt.pointerId,
                    }
                    : default;
            }
        }


        /// <summary>
        /// 从命中元素沿可视树向上找第一个带有效行数据的元素。
        /// </summary>
        private static object FindRowData(VisualElement element)
        {
            while (element != null)
            {
                if (element.userData is HTNTask ||
                    element.userData is HTNTaskRow ||
                    element.userData is HTNMethodRef)
                {
                    return element.userData;
                }

                element =
                    element.parent;
            }

            return null;
        }


        /// <summary>
        /// 弹出右键菜单（右键抬起时调用，
        /// 按 PointerDown 记录的命中行决定内容）。
        /// </summary>
        private void ShowContextMenu()
        {
            GenericMenu menu =
                new GenericMenu();

            if (_contextTarget is HTNTask task)
            {
                BuildTaskMenu(menu, task);
            }
            else if (_contextTarget is HTNTaskRow taskRow &&
                     taskRow.Task != null)
            {
                BuildTaskMenu(menu, taskRow.Task);
            }
            else if (_contextTarget is HTNMethodRef methodRef)
            {
                BuildMethodMenu(menu, methodRef);
            }
            else
            {
                BuildBlankMenu(menu);
            }

            menu.ShowAsContext();
        }


        private void BuildTaskMenu(
            GenericMenu menu,
            HTNTask task)
        {
            if (task is HTNCompoundTask)
            {
                menu.AddItem(
                    new GUIContent("设为根任务"),
                    false,
                    () => _controller.RequestSetRoot(task));
            }

            menu.AddItem(
                new GUIContent("重命名…"),
                false,
                () => _controller.RequestRenameTask(task));

            menu.AddSeparator("");

            menu.AddItem(
                new GUIContent("删除任务"),
                false,
                () => _controller.RequestDeleteTask(task));
        }


        private void BuildMethodMenu(
            GenericMenu menu,
            HTNMethodRef methodRef)
        {
            menu.AddItem(
                new GUIContent("添加子任务…"),
                false,
                () => _controller.RequestAddSubTask(methodRef));

            menu.AddItem(
                new GUIContent("上移方法"),
                false,
                () => _controller.RequestMoveMethod(methodRef, -1));

            menu.AddItem(
                new GUIContent("下移方法"),
                false,
                () => _controller.RequestMoveMethod(methodRef, 1));

            menu.AddSeparator("");

            menu.AddItem(
                new GUIContent("删除方法"),
                false,
                () => _controller.RequestDeleteMethod(methodRef));
        }


        private void BuildBlankMenu(GenericMenu menu)
        {
            menu.AddItem(
                new GUIContent("添加复合任务"),
                false,
                () => _controller.RequestAddCompoundTask());

            menu.AddItem(
                new GUIContent("添加基元任务"),
                false,
                () => _controller.RequestAddPrimitiveTask());

            menu.AddSeparator("");

            menu.AddItem(
                new GUIContent("世界状态属性…"),
                false,
                () => _controller.RequestOpenWorldStateWindow());
        }


        // =========================================================
        // 左键拖拽
        // =========================================================

        /// <summary>左键按下的候选拖拽信息。</summary>
        private struct PressState
        {
            public object Source;

            /// <summary>按下位置（面板坐标，Unity 6 指针事件为 Vector3）。</summary>
            public Vector3 StartPosition;

            public int PointerId;
        }

        private PressState _press;

        private bool _isDragging;

        private Label _dragGhost;

        private VisualElement _dragOverRow;

        /// <summary>触发拖拽的位移阈值（像素）。</summary>
        private const float DragThreshold = 6f;


        private void OnTreePointerMove(PointerMoveEvent evt)
        {
            if (_press.Source == null ||
                _isDragging)
            {
                return;
            }

            // 左键已松开（错过 Up 事件）→ 放弃候选
            if ((evt.pressedButtons & 1) == 0)
            {
                _press = default;

                return;
            }

            if ((evt.position - _press.StartPosition).sqrMagnitude <
                DragThreshold * DragThreshold)
            {
                return;
            }

            StartDrag(evt);
        }


        private void OnTreePointerUp(PointerUpEvent evt)
        {
            if (_isDragging)
            {
                CompleteDrag(evt.position);

                CleanupDrag();

                _press = default;

                return;
            }

            // 右键抬起：按按下时记录的命中行弹出菜单
            if (evt.button == 1)
            {
                ShowContextMenu();
            }

            _press = default;
        }


        private void OnTreePointerCancel(PointerCancelEvent evt)
        {
            if (_isDragging)
            {
                CleanupDrag();
            }

            _press = default;
        }


        private void StartDrag(PointerMoveEvent evt)
        {
            if (_press.Source == null ||
                panel == null)
            {
                return;
            }

            _isDragging = true;

            // 拖拽期间的 move / up 注册到面板根，指针移出树也能收到；
            // TrickleDown 保证先于其它元素处理
            panel.visualTree.RegisterCallback<PointerMoveEvent>(
                OnRootPointerMove,
                TrickleDown.TrickleDown);

            panel.visualTree.RegisterCallback<PointerUpEvent>(
                OnRootPointerUp,
                TrickleDown.TrickleDown);

            panel.visualTree.RegisterCallback<PointerCancelEvent>(
                OnRootPointerCancel,
                TrickleDown.TrickleDown);


            _dragGhost =
                new Label(DragSourceLabel(_press.Source));

            _dragGhost.pickingMode =
                PickingMode.Ignore;

            _dragGhost.style.position =
                Position.Absolute;

            _dragGhost.style.paddingTop = 3;
            _dragGhost.style.paddingBottom = 3;
            _dragGhost.style.paddingLeft = 8;
            _dragGhost.style.paddingRight = 8;

            _dragGhost.style.backgroundColor =
                new Color(0.2f, 0.32f, 0.5f, 0.92f);

            _dragGhost.style.color =
                Color.white;

            _dragGhost.style.fontSize = 11;

            _dragGhost.style.borderTopLeftRadius = 4;
            _dragGhost.style.borderTopRightRadius = 4;
            _dragGhost.style.borderBottomLeftRadius = 4;
            _dragGhost.style.borderBottomRightRadius = 4;

            panel.visualTree.Add(_dragGhost);

            MoveGhost(evt.position);
        }


        private static string DragSourceLabel(object source)
        {
            return source switch
            {
                HTNTaskRow row => row.Task != null ? row.Task.name : "",
                HTNTask task => task.name,
                HTNMethodRef methodRef =>
                    $"方法{methodRef.Index}" +
                    (methodRef.Method != null &&
                     !string.IsNullOrEmpty(methodRef.Method.MethodName)
                        ? $"：{methodRef.Method.MethodName}"
                        : ""),
                _ => "",
            };
        }


        private void OnRootPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging)
            {
                return;
            }

            if ((evt.pressedButtons & 1) == 0)
            {
                // 在树外松开了左键：取消拖拽
                CleanupDrag();
                _press = default;

                return;
            }

            MoveGhost(evt.position);

            UpdateDropHighlight(
                ResolveDropTarget(evt.position));
        }


        private void OnRootPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging)
            {
                return;
            }

            object target =
                ResolveDropTarget(evt.position);

            if (target != null &&
                _press.Source != null)
            {
                _controller.RequestTreeDrop(
                    _press.Source,
                    target);
            }

            CleanupDrag();

            _press = default;
        }


        private void OnRootPointerCancel(PointerCancelEvent evt)
        {
            if (_isDragging)
            {
                CleanupDrag();
            }

            _press = default;
        }


        private void MoveGhost(Vector3 panelPosition)
        {
            if (_dragGhost == null)
            {
                return;
            }

            _dragGhost.style.left =
                panelPosition.x + 10;

            _dragGhost.style.top =
                panelPosition.y + 10;
        }


        /// <summary>
        /// 从面板坐标向下拾取，向上回溯找到带有效行数据的元素。
        /// </summary>
        private object ResolveDropTarget(Vector3 panelPosition)
        {
            if (panel == null)
            {
                return null;
            }

            VisualElement picked =
                panel.Pick(panelPosition);

            while (picked != null)
            {
                if (picked.userData is HTNTask ||
                    picked.userData is HTNTaskRow ||
                    picked.userData is HTNMethodRef)
                {
                    return picked.userData;
                }

                picked =
                    picked.parent;
            }

            return null;
        }


        private void UpdateDropHighlight(object target)
        {
            VisualElement targetRow = null;

            // 遍历可见行，找 userData 与投放目标匹配的行元素
            if (target != null)
            {
                foreach (VisualElement child in this.Children())
                {
                    targetRow =
                        FindRowByData(child, target);

                    if (targetRow != null)
                    {
                        break;
                    }
                }
            }

            if (_dragOverRow == targetRow)
            {
                return;
            }

            if (_dragOverRow != null)
            {
                _dragOverRow.style.backgroundColor =
                    StyleKeyword.Null;
            }

            _dragOverRow = targetRow;

            if (_dragOverRow != null)
            {
                _dragOverRow.style.backgroundColor =
                    new Color(0.25f, 0.42f, 0.62f, 0.4f);
            }
        }


        private static VisualElement FindRowByData(
            VisualElement element,
            object data)
        {
            if (element.userData == data)
            {
                return element;
            }

            foreach (VisualElement child in element.Children())
            {
                VisualElement found =
                    FindRowByData(child, data);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }


        private void CompleteDrag(Vector3 panelPosition)
        {
            object target =
                ResolveDropTarget(panelPosition);

            if (target == null ||
                _press.Source == null)
            {
                return;
            }

            // 拖回自己：忽略
            if (ReferenceEquals(target, _press.Source))
            {
                return;
            }

            _controller.RequestTreeDrop(
                _press.Source,
                target);
        }


        private void CleanupDrag()
        {
            if (panel != null)
            {
                panel.visualTree.UnregisterCallback<PointerMoveEvent>(
                    OnRootPointerMove);

                panel.visualTree.UnregisterCallback<PointerUpEvent>(
                    OnRootPointerUp);

                panel.visualTree.UnregisterCallback<PointerCancelEvent>(
                    OnRootPointerCancel);
            }

            if (_dragGhost != null)
            {
                _dragGhost.RemoveFromHierarchy();

                _dragGhost = null;
            }

            if (_dragOverRow != null)
            {
                _dragOverRow.style.backgroundColor =
                    StyleKeyword.Null;

                _dragOverRow = null;
            }

            _isDragging = false;
        }
    }
}
