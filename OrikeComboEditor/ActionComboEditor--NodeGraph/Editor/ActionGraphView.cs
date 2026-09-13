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

            try
            {
                _graph =
                    graph;

                DeleteElements(
                    graphElements.ToList());

                _nodeViews.Clear();

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

            Port outputPort =
                fromNode.GetOutputPort(tag);

            Port inputPort =
                toNode.GetInputPort(tag);

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
                        port.node != startPort.node)
                .ToList();
        }


        // =========================================================
        // 右键菜单
        // =========================================================

        public override void BuildContextualMenu(
            ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(
                evt);

            if (_graph == null)
            {
                return;
            }

            Vector2 menuWorldPosition =
                _lastMouseWorldPosition != Vector2.zero
                    ? _lastMouseWorldPosition
                    : worldBound.center;

            evt.menu.AppendAction(
                "添加 Action 节点",
                menuAction =>
                {
                    CreateActionAt(
                        menuWorldPosition);
                });

            if (selection.Count > 0)
            {
                evt.menu.AppendAction(
                    "删除选中",
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
                    if (element is Edge edge &&
                        edge.userData is TransitionData transition)
                    {
                        RemoveTransitionData(
                            transition);
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
                        !string.IsNullOrEmpty(
                            fromBeCancel.Tag) &&
                        fromBeCancel.Tag ==
                        toCancel.Tag;

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

                    // 相同 From -> To 已存在，拒绝重复
                    if (_graph.HasTransition(from, to))
                    {
                        RejectEdge(
                            edge);

                        if (_window != null)
                        {
                            _window.ShowNotification(
                                new GUIContent(
                                    "连接失败：这两个 Action 之间已存在连线"));
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
