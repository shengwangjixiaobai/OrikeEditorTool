using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orike.ActionGraph
{
    /// <summary>
    /// Action Graph 的节点画布。
    ///
    /// 负责：
    ///   - 根据 ActionGraphData 构建 Node / Edge
    ///   - 拖拽连线时调用数据层 Connect（自动生成 Cancel / BeCancel）
    ///   - 删除 Edge / Node 时同步删除数据
    ///   - Undo / Redo 后整体重建
    /// </summary>
    public class ActionGraphView : GraphView
    {
        private readonly ActionGraphWindow _window;

        private ActionGraphData _graph;

        private readonly Dictionary<Action, ActionNodeView> _nodeViews =
            new Dictionary<Action, ActionNodeView>();

        /// <summary>
        /// 当前被运行时高亮的节点（对应角色正在播放的动作）。
        /// </summary>
        private ActionNodeView _activeNode;

        /// <summary>
        /// 图中的“入口”节点视图。
        /// </summary>
        private EntryNodeView _entryNode;

        /// <summary>
        /// 入口节点到入口动作的连线视图。
        /// </summary>
        private Edge _entryEdge;

        /// <summary>
        /// 正在以数据为准整体重建视图时为 true。
        /// 重建过程中的 DeleteElements 同样会触发
        /// graphViewChanged，必须跳过数据同步，
        /// 否则会把已有的 Transition 误删。
        /// </summary>
        private bool _isLoading;

        private bool _reloadScheduled;

        /// <summary>
        /// 本次连线校验中被拒绝的 Edge，待当前 graphViewChanged
        /// 返回后统一从视图移除，确保不残留连线。
        /// </summary>
        private readonly List<Edge> _rejectedEdges =
            new List<Edge>();

        /// <summary>
        /// 复制到剪贴板的 Action 序列化 JSON（用于 Ctrl+C / Ctrl+V）。
        /// 入口节点不参与复制。
        /// </summary>
        private readonly List<string> _copyBuffer =
            new List<string>();


        public ActionGraphData Graph =>
            _graph;


        private Vector2 _lastMouseWorldPosition;

        public ActionGraphView(
            ActionGraphWindow window)
        {
            _window =
                window;

            style.flexGrow =
                1f;

            style.backgroundColor =
                new Color(
                    0.13f,
                    0.13f,
                    0.13f);


            // =========================================================
            // 背景网格与操作器
            // =========================================================

            GridBackground grid =
                new GridBackground();

            Insert(
                0,
                grid);

            grid.StretchToParentSize();

            SetupZoom(
                ContentZoomer.DefaultMinScale,
                ContentZoomer.DefaultMaxScale);

            this.AddManipulator(
                new ContentDragger());

            this.AddManipulator(
                new SelectionDragger());

            this.AddManipulator(
                new SelectionDropper());

            this.AddManipulator(
                new RectangleSelector());


            // =========================================================
            // 数据变化回调
            // =========================================================

            graphViewChanged +=
                OnGraphViewChanged;

            Undo.undoRedoPerformed +=
                HandleUndoRedo;

            RegisterCallback<KeyDownEvent>(
                OnKeyDown);
        }


        /// <summary>
        /// 窗口关闭时释放回调。
        /// </summary>
        public void Dispose()
        {
            graphViewChanged -=
                OnGraphViewChanged;

            Undo.undoRedoPerformed -=
                HandleUndoRedo;

            EditorApplication.update -=
                ReloadDeferred;
        }


        // =========================================================
        // 加载 / 重建
        // =========================================================

        public void LoadGraph(
            ActionGraphData graph)
        {
            _isLoading =
                true;

            // 保存当前选中的 Action / Transition，
            // 重建后恢复选中状态，避免 Inspector 丢失选中
            Action selectedAction =
                GetSelectedNode()?.Action;

            TransitionData selectedTransition =
                GetSelectedEdge()?.userData
                    as TransitionData;

            try
            {
                _graph =
                    graph;

                DeleteElements(
                    graphElements.ToList());

                _nodeViews.Clear();

                _activeNode =
                    null;

                _entryNode =
                    null;

                _entryEdge =
                    null;

                if (graph == null)
                {
                    return;
                }

                graph.RefreshData();


                // 先建节点
                foreach (Action action in graph.Actions)
                {
                    if (action == null)
                    {
                        continue;
                    }

                    AddActionNode(
                        action);
                }


                // 再建连线
                foreach (TransitionData transition in graph.Transitions)
                {
                    if (transition == null ||
                        transition.From == null ||
                        transition.To == null)
                    {
                        continue;
                    }

                    CreateEdgeView(
                        transition);
                }

                // 入口节点（含入口连线）
                CreateEntryNode(
                    graph);

                // 恢复选中状态
                if (selectedAction != null &&
                    _nodeViews.TryGetValue(
                        selectedAction,
                        out ActionNodeView nodeToSelect))
                {
                    AddToSelection(
                        nodeToSelect);
                }
                else if (selectedTransition != null)
                {
                    foreach (Edge edge in edges.ToList())
                    {
                        if (edge.userData == selectedTransition)
                        {
                            AddToSelection(
                                edge);

                            break;
                        }
                    }
                }
            }
            finally
            {
                _isLoading =
                    false;
            }
        }


        /// <summary>
        /// Undo / Redo 后由数据整体重建视图。
        /// </summary>
        private void HandleUndoRedo()
        {
            if (_window != null)
            {
                _window.ReloadGraph();
            }
        }


        /// <summary>
        /// 请求在下一编辑帧以数据为准整体重建视图。
        /// 用于结构变更（连线 / 删线 / 删节点 / 改 Id、Cancel、BeCancel）。
        /// </summary>
        public void RequestReload()
        {
            if (_reloadScheduled)
            {
                return;
            }

            _reloadScheduled =
                true;

            EditorApplication.update +=
                ReloadDeferred;
        }


        private void ReloadDeferred()
        {
            EditorApplication.update -=
                ReloadDeferred;

            if (!_reloadScheduled)
            {
                return;
            }

            _reloadScheduled =
                false;

            if (_window != null)
            {
                _window.ReloadGraph();
            }
        }


        // =========================================================
        // 节点
        // =========================================================

        private ActionNodeView AddActionNode(
            Action action)
        {
            ActionNodeView node =
                new ActionNodeView(action, this);

            _nodeViews[action] =
                node;

            AddElement(
                node);

            return node;
        }


        /// <summary>
        /// 创建“入口”节点，并根据 graph.EntryAction 恢复入口连线。
        /// </summary>
        private void CreateEntryNode(
            ActionGraphData graph)
        {
            _entryNode =
                new EntryNodeView(
                    graph);

            Vector2 position;

            if (graph.HasEntryNodePosition)
            {
                position =
                    graph.EntryNodePosition;
            }
            else
            {
                // 首次打开：放到最左节点左侧，避免与 Action 节点重叠
                float minX =
                    float.MaxValue;

                float minY =
                    float.MaxValue;

                foreach (ActionNodeView node in _nodeViews.Values)
                {
                    Vector2 nodePos =
                        node.GetPosition().position;

                    minX =
                        Mathf.Min(
                            minX,
                            nodePos.x);

                    minY =
                        Mathf.Min(
                            minY,
                            nodePos.y);
                }

                if (minX == float.MaxValue)
                {
                    minX =
                        0f;

                    minY =
                        0f;
                }

                position =
                    new Vector2(
                        minX - 260f,
                        minY);

                graph.EntryNodePosition =
                    position;
            }

            _entryNode.SetPosition(
                new Rect(
                    position,
                    Vector2.zero));

            AddElement(
                _entryNode);

            if (graph.EntryAction == null ||
                !_nodeViews.TryGetValue(
                    graph.EntryAction,
                    out ActionNodeView target))
            {
                return;
            }

            Edge edge =
                new Edge
                {
                    userData =
                        graph.EntryAction,

                    output =
                        _entryNode.OutputPort,

                    input =
                        target.AutoInputPort,
                };

            _entryNode.OutputPort.Connect(
                edge);

            target.AutoInputPort.Connect(
                edge);

            AddElement(
                edge);

            _entryEdge =
                edge;
        }


        /// <summary>
        /// 在画布世界坐标处创建 Action 数据与节点。
        /// </summary>
        public ActionNodeView CreateActionAt(
            Vector2 worldPosition)
        {
            if (_graph == null)
            {
                return null;
            }

            Vector2 localPosition =
                contentViewContainer.WorldToLocal(
                    worldPosition);

            Action action =
                ActionGraphAssetOps.CreateAction(
                    _graph,
                    localPosition);

            return AddActionNode(
                action);
        }


        /// <summary>
        /// 在当前视口中心创建节点（工具栏按钮用）。
        /// </summary>
        public ActionNodeView CreateActionAtCenter()
        {
            Vector2 worldCenter =
                worldBound.center;

            return CreateActionAt(
                worldCenter);
        }


        public void RefreshAllNodeLabels()
        {
            foreach (ActionNodeView node in _nodeViews.Values)
            {
                node.RefreshLabels();
            }
        }


        /// <summary>
        /// 高亮当前运行时动作对应的节点。
        /// 传入 null 时清除高亮。
        /// </summary>
        public void SetActiveAction(
            Action action)
        {
            ActionNodeView newActive =
                null;

            if (action != null)
            {
                _nodeViews.TryGetValue(
                    action,
                    out newActive);
            }

            if (_activeNode == newActive)
            {
                return;
            }

            if (_activeNode != null)
            {
                _activeNode.SetActive(
                    false);
            }

            _activeNode =
                newActive;

            if (_activeNode != null)
            {
                _activeNode.SetActive(
                    true);

                // 追踪：把新建高亮的节点平移到视口中心，避免超出屏幕
                FrameNode(
                    _activeNode);
            }

            MarkDirtyRepaint();
        }


        /// <summary>
        /// 将指定节点平移到视口中心（运行时追踪高亮 / 定位入口用）。
        /// </summary>
        public void FrameNode(
            Node node)
        {
            if (node == null)
            {
                return;
            }

            Vector2 nodeCenterInView =
                contentViewContainer.ChangeCoordinatesTo(
                    this,
                    node.GetPosition().center);

            Vector2 viewportCenter =
                localBound.center;

            Vector3 position =
                contentViewContainer.transform.position;

            contentViewContainer.transform.position =
                new Vector3(
                    position.x +
                    (viewportCenter.x -
                     nodeCenterInView.x),

                    position.y +
                    (viewportCenter.y -
                     nodeCenterInView.y),

                    position.z);
        }


        /// <summary>
        /// 定位并选中入口节点（工具栏“定位入口”按钮用）。
        /// </summary>
        public void FrameEntryNode()
        {
            if (_entryNode == null)
            {
                return;
            }

            FrameNode(
                _entryNode);

            ClearSelection();

            AddToSelection(
                _entryNode);
        }


        /// <summary>
        /// 用 Sugiyama 分层布局重排所有 Action 节点与入口节点。
        /// 记录 Undo、写回各 Action.NodePosition、保存后整体缩放到合适视野。
        /// </summary>
        public void ApplyAutoLayout()
        {
            if (_graph == null ||
                _nodeViews.Count == 0)
            {
                return;
            }

            ActionGraphAutoLayout.Compute(
                _graph,
                out Dictionary<Action, Vector2> positions,
                out Vector2 entryPosition);

            if (positions.Count == 0)
            {
                return;
            }

            Undo.SetCurrentGroupName(
                "Auto Layout Action Graph");

            Undo.RecordObject(
                _graph,
                "Auto Layout");

            foreach (KeyValuePair<Action, Vector2> kv in positions)
            {
                Undo.RecordObject(
                    kv.Key,
                    "Auto Layout");

                kv.Key.NodePosition =
                    kv.Value;

                if (_nodeViews.TryGetValue(
                        kv.Key,
                        out ActionNodeView node))
                {
                    node.SetPosition(
                        new Rect(
                            kv.Value,
                            Vector2.zero));
                }

                EditorUtility.SetDirty(
                    kv.Key);
            }

            if (_entryNode != null)
            {
                _entryNode.SetPosition(
                    new Rect(
                        entryPosition,
                        Vector2.zero));
            }

            EditorUtility.SetDirty(
                _graph);

            AssetDatabase.SaveAssetIfDirty(
                _graph);

            FrameAll();

            MarkDirtyRepaint();
        }


        // =========================================================
        // 连线
        // =========================================================

        private Edge CreateEdgeView(
            TransitionData transition)
        {
            if (!_nodeViews.TryGetValue(
                    transition.From,
                    out ActionNodeView fromNode) ||
                !_nodeViews.TryGetValue(
                    transition.To,
                    out ActionNodeView toNode))
            {
                return null;
            }

            Port outputPort;

            Port inputPort;

            if (transition.Auto)
            {
                // Loop 动作的自动转移端口被隐藏，
                // 数据保留但暂不显示连线，取消 Loop 后重建恢复。
                if (transition.From == null ||
                    transition.From.Loop)
                {
                    return null;
                }

                outputPort =
                    fromNode.AutoOutputPort;

                inputPort =
                    toNode.AutoInputPort;
            }
            else
            {
                // 查找 From.BeCancels 与 To.Cancels 之间匹配的 Tag，
                // 用它定位连线两端的端口。没有匹配 Tag 时不创建连线视图。
                string tag =
                    _graph != null
                        ? _graph.FindMatchingTag(
                            transition.From,
                            transition.To)
                        : null;

                if (string.IsNullOrEmpty(tag))
                {
                    return null;
                }

                outputPort =
                    fromNode.GetOutputPort(tag);

                inputPort =
                    toNode.GetInputPort(tag);
            }

            if (outputPort == null ||
                inputPort == null)
            {
                return null;
            }

            Edge edge =
                new Edge
                {
                    userData = transition,

                    output =
                        outputPort,

                    input =
                        inputPort,
                };

            outputPort.Connect(
                edge);

            inputPort.Connect(
                edge);

            AddElement(
                edge);

            return edge;
        }


        // =========================================================
        // 端口兼容
        // =========================================================

        public override List<Port> GetCompatiblePorts(
            Port startPort,
            NodeAdapter nodeAdapter)
        {
            return ports
                .Where(
                    port =>
                        port.direction != startPort.direction &&
                        port.node != startPort.node &&
                        // Tag 端口(bool)与自动转移端口(Action)禁止混连
                        port.portType == startPort.portType)
                .ToList();
        }


        // =========================================================
        // 右键菜单
        // =========================================================

        public override void BuildContextualMenu(
            ContextualMenuPopulateEvent evt)
        {
            if (_graph == null)
            {
                return;
            }

            Vector2 menuWorldPosition =
                _lastMouseWorldPosition != Vector2.zero
                    ? _lastMouseWorldPosition
                    : worldBound.center;

            evt.menu.AppendAction(
                "Add Action Node",
                menuAction =>
                {
                    CreateActionAt(
                        menuWorldPosition);
                });

            if (selection.OfType<ActionNodeView>().Any())
            {
                evt.menu.AppendAction(
                    "Copy",
                    menuAction =>
                    {
                        CopySelection();
                    });
            }

            if (_copyBuffer.Count > 0)
            {
                evt.menu.AppendAction(
                    "Paste",
                    menuAction =>
                    {
                        PasteCopied();
                    });
            }

            if (selection.Count > 0)
            {
                evt.menu.AppendAction(
                    "Delete",
                    menuAction =>
                    {
                        DeleteSelection();
                    });
            }

            evt.menu.AppendSeparator();
        }


        // =========================================================
        // 删除键
        // =========================================================

        private void OnKeyDown(
            KeyDownEvent evt)
        {
            if (evt.target is TextField ||
                evt.target is FloatField ||
                evt.target is IntegerField ||
                evt.target is IMGUIContainer)
            {
                return;
            }

            bool command =
                evt.commandKey ||
                evt.ctrlKey;

            if (command &&
                evt.keyCode == KeyCode.C)
            {
                CopySelection();

                evt.StopPropagation();

                return;
            }

            if (command &&
                evt.keyCode == KeyCode.V)
            {
                PasteCopied();

                evt.StopPropagation();

                return;
            }

            if (evt.keyCode == KeyCode.Delete ||
                evt.keyCode == KeyCode.Backspace)
            {
                if (selection.Count > 0)
                {
                    DeleteSelection();

                    evt.StopPropagation();
                }
            }
        }


        // =========================================================
        // 复制 / 粘贴
        // =========================================================

        /// <summary>
        /// 把选中 Action 节点序列化到剪贴板（Ctrl+C）。
        /// </summary>
        private void CopySelection()
        {
            _copyBuffer.Clear();

            foreach (ActionNodeView node in
                     selection.OfType<ActionNodeView>())
            {
                if (node.Action == null)
                {
                    continue;
                }

                _copyBuffer.Add(
                    EditorJsonUtility.ToJson(
                        node.Action));
            }
        }


        /// <summary>
        /// 粘贴剪贴板中的 Action（Ctrl+V）。
        /// 依次从视口中心开始放置，并逐个右下偏移。
        /// </summary>
        private void PasteCopied()
        {
            if (_graph == null ||
                _copyBuffer.Count == 0)
            {
                return;
            }

            Vector2 anchor =
                worldBound.center;

            foreach (string json in _copyBuffer)
            {
                Vector2 localPosition =
                    contentViewContainer.WorldToLocal(
                        anchor);

                Action action =
                    ActionGraphAssetOps.PasteAction(
                        _graph,
                        json,
                        localPosition);

                ActionNodeView node =
                    AddActionNode(
                        action);

                if (node != null)
                {
                    AddToSelection(
                        node);
                }

                anchor +=
                    new Vector2(
                        40f,
                        40f);
            }
        }


        // =========================================================
        // 数据同步
        // =========================================================

        private GraphViewChange OnGraphViewChanged(
            GraphViewChange change)
        {
            // 整体重建（LoadGraph）过程中的 AddElement / DeleteElements
            // 同样会触发本回调。此时只需原样返回让 GraphView 完成视图操作，
            // 不能同步数据，否则会把现有 Transition / Action 误删。
            if (_graph == null ||
                _isLoading)
            {
                return change;
            }


            // ---------------------------------------------------------
            // 删除 Edge / Node
            // ---------------------------------------------------------
            if (change.elementsToRemove != null)
            {
                foreach (GraphElement element
                         in change.elementsToRemove.ToList())
                {
                    if (element is Edge edge)
                    {
                        // 入口连线：删除即清除入口
                        if (edge.output?.node is EntryNodeView)
                        {
                            ClearEntry();

                            continue;
                        }

                        if (edge.userData is TransitionData transition)
                        {
                            RemoveTransitionData(
                                transition);
                        }
                    }
                    else if (element is ActionNodeView node)
                    {
                        ActionGraphAssetOps.DeleteAction(
                            _graph,
                            node.Action);

                        _nodeViews.Remove(
                            node.Action);

                        RequestReload();
                    }
                }
            }


            // ---------------------------------------------------------
            // 创建 Edge（需 Tag 匹配）
            // ---------------------------------------------------------
            if (change.edgesToCreate != null)
            {
                List<Edge> accepted =
                    new List<Edge>();

                foreach (Edge edge in change.edgesToCreate)
                {
                    // 入口连线：从“入口”节点连接到某个 Action
                    if (edge.output?.node is EntryNodeView)
                    {
                        TryCreateEntryEdge(
                            edge,
                            accepted);

                        continue;
                    }

                    ActionNodeView fromNode =
                        edge.output != null
                            ? edge.output.node as ActionNodeView
                            : null;

                    ActionNodeView toNode =
                        edge.input != null
                            ? edge.input.node as ActionNodeView
                            : null;

                    if (fromNode == null ||
                        toNode == null ||
                        fromNode == toNode)
                    {
                        // 自连 / 非法连线，拒绝
                        RejectEdge(
                            edge);

                        continue;
                    }

                    Action from =
                        fromNode.Action;

                    Action to =
                        toNode.Action;

                    // 自动转移端口之间的连线走独立逻辑（不校验 Tag）
                    if (ActionNodeView.IsAutoPort(edge.output) ||
                        ActionNodeView.IsAutoPort(edge.input))
                    {
                        TryCreateAutoEdge(
                            edge,
                            from,
                            to,
                            accepted);

                        continue;
                    }

                    // 取出连线两端端口对应的 BeCancel / Cancel，
                    // 校验 Tag 是否一致。占位端口（userData 为 null）不允许连线。
                    BeCancelData fromBeCancel =
                        edge.output != null
                            ? edge.output.userData as BeCancelData
                            : null;

                    CancelData toCancel =
                        edge.input != null
                            ? edge.input.userData as CancelData
                            : null;

                    bool tagMatched =
                        fromBeCancel != null &&
                        toCancel != null &&
                        fromBeCancel.HasTag(
                            toCancel.Tag);

                    if (!tagMatched)
                    {
                        RejectEdge(
                            edge);

                        if (_window != null)
                        {
                            _window.ShowNotification(
                                new GUIContent(
                                    "连接失败：From 的 BeCancel Tag 与 To 的 Cancel Tag 不一致"));
                        }

                        continue;
                    }

                    ActionGraphAssetOps.RecordGraphStructureUndo(
                        _graph,
                        from,
                        to,
                        "Connect Action");

                    TransitionData created =
                        _graph.Connect(
                            from,
                            to);

                    if (created == null)
                    {
                        RejectEdge(
                            edge);

                        if (_window != null)
                        {
                            _window.ShowNotification(
                                new GUIContent(
                                    "连接失败：未找到匹配的 Tag"));
                        }

                        continue;
                    }

                    ActionGraphAssetOps.SetGraphAndActionsDirty(
                        _graph,
                        from,
                        to);

                    edge.userData =
                        created;

                    // Cancel / BeCancel 由用户手动管理，Connect 不再改动数据，
                    // 端口结构不变，edge 已连在正确端口上，无需重新 SyncPorts。

                    fromNode.RefreshLabels();

                    toNode.RefreshLabels();

                    accepted.Add(
                        edge);
                }

                change.edgesToCreate =
                    accepted;
            }

            return change;
        }


        /// <summary>
        /// 处理“结束自动转移”端口上的新建连线。
        /// 不需要 Tag 匹配，但要求两端都是自动端口、
        /// From 非 Loop，且每个 From 至多一条。
        /// </summary>
        private void TryCreateAutoEdge(
            Edge edge,
            Action from,
            Action to,
            List<Edge> accepted)
        {
            if (!ActionNodeView.IsAutoPort(edge.output) ||
                !ActionNodeView.IsAutoPort(edge.input) ||
                edge.output.direction != Direction.Output ||
                edge.input.direction != Direction.Input)
            {
                RejectEdge(
                    edge);

                _window?.ShowNotification(
                    new GUIContent(
                        "连接失败：自动转移端口只能连到自动转入端口"));

                return;
            }

            if (from.Loop)
            {
                RejectEdge(
                    edge);

                _window?.ShowNotification(
                    new GUIContent(
                        "连接失败：Loop 动作结束后重播自己，无需自动转移"));

                return;
            }

            if (_graph.GetAutoTransition(from) != null)
            {
                RejectEdge(
                    edge);

                _window?.ShowNotification(
                    new GUIContent(
                        "连接失败：该动作已配置结束自动转移目标"));

                return;
            }

            ActionGraphAssetOps.RecordGraphStructureUndo(
                _graph,
                from,
                to,
                "Connect Auto Transition");

            TransitionData created =
                _graph.ConnectAuto(
                    from,
                    to);

            if (created == null)
            {
                RejectEdge(
                    edge);

                return;
            }

            ActionGraphAssetOps.SetGraphAndActionsDirty(
                _graph,
                from,
                to);

            edge.userData =
                created;

            accepted.Add(
                edge);
        }


        /// <summary>
        /// 处理“入口”节点的连线创建：把目标 Action 设为入口动作。
        /// 入口唯一，已存在入口连线时拒绝新建。
        /// </summary>
        private void TryCreateEntryEdge(
            Edge edge,
            List<Edge> accepted)
        {
            ActionNodeView toNode =
                edge.input != null
                    ? edge.input.node as ActionNodeView
                    : null;

            if (toNode == null ||
                edge.input != toNode.AutoInputPort)
            {
                RejectEdge(
                    edge);

                return;
            }

            if (_graph.EntryAction != null)
            {
                RejectEdge(
                    edge);

                _window?.ShowNotification(
                    new GUIContent(
                        "入口已存在，请先删除当前入口连线"));

                return;
            }

            Undo.RecordObject(
                _graph,
                "Set Entry");

            _graph.EntryAction =
                toNode.Action;

            EditorUtility.SetDirty(
                _graph);

            AssetDatabase.SaveAssetIfDirty(
                _graph);

            edge.userData =
                toNode.Action;

            _entryEdge =
                edge;

            accepted.Add(
                edge);
        }


        /// <summary>
        /// 清除入口动作（记录 Undo 并保存）。
        /// </summary>
        private void ClearEntry()
        {
            if (_graph == null)
            {
                return;
            }

            Undo.RecordObject(
                _graph,
                "Clear Entry");

            _graph.EntryAction =
                null;

            EditorUtility.SetDirty(
                _graph);

            AssetDatabase.SaveAssetIfDirty(
                _graph);

            _entryEdge =
                null;
        }


        /// <summary>
        /// 拒绝一条连线：断开其与两端端口的引用。
        /// 拒绝是通过不把 edge 加入 edgesToCreate 返回值实现的；
        /// 这里只需清理端口连接，防止残留 state。
        /// </summary>
        private void RejectEdge(
            Edge edge)
        {
            if (edge == null)
            {
                return;
            }

            edge.output?.Disconnect(
                edge);

            edge.input?.Disconnect(
                edge);

            // 记录待清除的连线，稍后统一从视图移除，
            // 防止校验后仍有残留连线显示。
            if (!_rejectedEdges.Contains(edge))
            {
                _rejectedEdges.Add(edge);

                schedule.Execute(
                    CleanupRejectedEdges);
            }
        }


        /// <summary>
        /// 移除所有被拒绝的连线（若其仍存在于视图中）。
        /// </summary>
        private void CleanupRejectedEdges()
        {
            if (_rejectedEdges.Count == 0)
            {
                return;
            }

            foreach (Edge edge in _rejectedEdges)
            {
                if (edge != null &&
                    edge.parent != null)
                {
                    RemoveElement(
                        edge);
                }
            }

            _rejectedEdges.Clear();
        }


        private void RemoveTransitionData(
            TransitionData transition)
        {
            Action from =
                transition.From;

            Action to =
                transition.To;

            ActionGraphAssetOps.RecordGraphStructureUndo(
                _graph,
                from,
                to,
                "Delete Transition");

            _graph.Disconnect(
                transition);

            ActionGraphAssetOps.SetGraphAndActionsDirty(
                _graph,
                from,
                to);

            if (from != null &&
                _nodeViews.TryGetValue(
                    from,
                    out ActionNodeView fromNode))
            {
                fromNode.RefreshLabels();
            }

            if (to != null &&
                _nodeViews.TryGetValue(
                    to,
                    out ActionNodeView toNode))
            {
                toNode.RefreshLabels();
            }

            RequestReload();
        }


        // =========================================================
        // 选中访问（Inspector 用）
        // =========================================================

        public ActionNodeView GetSelectedNode()
        {
            return selection
                .OfType<ActionNodeView>()
                .FirstOrDefault();
        }


        public Edge GetSelectedEdge()
        {
            return selection
                .OfType<Edge>()
                .FirstOrDefault();
        }
    }
}
