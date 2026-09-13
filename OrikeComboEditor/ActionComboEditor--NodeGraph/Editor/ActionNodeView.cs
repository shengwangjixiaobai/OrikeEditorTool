using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orike.ActionGraph
{
    /// <summary>
    /// Graph 中的 Action 节点。
    ///
    /// 一个 Node 对应一个 Action：
    ///   - 端口按数据动态生成
    ///     InputPort  <- 每个 CancelData（主动取消方）
    ///     OutputPort <- 每个 BeCancelData（被取消方）
    ///   - 每个端口旁有可编辑的 Tag 文本框与删除按钮
    ///   - 输入/输出区底部有 "+" 按钮用于新增 Cancel / BeCancel
    ///   - 通过内嵌 IMGUI 编辑器编辑 Action 其余字段
    ///
    /// 端口方向（Edge）：
    ///   From（被取消）输出端口  ->  To（取消）输入端口
    /// </summary>
    public class ActionNodeView : Node
    {
        private const string UssClassNode =
            "action-node";


        private readonly ActionGraphView _view;

        private readonly SerializedObject _serialized;


        /// <summary>
        /// 内嵌 IMGUI 编辑器，高度按内容自适应，
        /// 避免 KeyCommand 等列表变多时内容被裁剪。
        /// </summary>
        private IMGUIContainer _editor;


        /// <summary>
        /// 输入端口：每个 CancelData 一个。
        /// </summary>
        private readonly Dictionary<CancelData, Port> _inputPorts =
            new Dictionary<CancelData, Port>();

        /// <summary>
        /// 输出端口：每个 BeCancelData 一个。
        /// </summary>
        private readonly Dictionary<BeCancelData, Port> _outputPorts =
            new Dictionary<BeCancelData, Port>();


        /// <summary>输入区底部的"+"按钮。</summary>
        private Button _addCancelButton;

        /// <summary>输出区底部的"+"按钮。</summary>
        private Button _addBeCancelButton;


        /// <summary>
        /// 节点对应的逻辑动作。
        /// </summary>
        public Action Action
        {
            get;
        }


        public ActionNodeView(
            Action action,
            ActionGraphView view)
        {
            Action =
                action;

            _view =
                view;

            AddToClassList(
                UssClassNode);

            capabilities |=
                Capabilities.Movable |
                Capabilities.Selectable |
                Capabilities.Deletable;

            title =
                GetDisplayTitle();

            style.minWidth =
                240f;


            // =========================================================
            // 内嵌编辑器（复用 Inspector 的字段绘制）
            // =========================================================

            _serialized =
                new SerializedObject(
                    action);

            _editor =
                new IMGUIContainer(
                    DrawNodeEditor);

            _editor.style.minWidth =
                220f;

            _editor.style.minHeight =
                220f;

            mainContainer.Add(
                _editor);


            RefreshExpandedState();

            // 收紧标题栏 / 端口容器与下方字段区域之间的间距
            titleContainer.style.marginBottom =
                0;

            titleContainer.style.paddingBottom =
                0;

            topContainer.style.marginTop =
                0;

            topContainer.style.marginBottom =
                0;

            topContainer.style.paddingTop =
                0;

            topContainer.style.paddingBottom =
                0;

            inputContainer.style.marginTop =
                0;

            inputContainer.style.marginBottom =
                0;

            inputContainer.style.paddingTop =
                0;

            inputContainer.style.paddingBottom =
                0;

            outputContainer.style.marginTop =
                0;

            outputContainer.style.marginBottom =
                0;

            outputContainer.style.paddingTop =
                0;

            outputContainer.style.paddingBottom =
                0;

            mainContainer.style.marginTop =
                0;

            mainContainer.style.paddingTop =
                0;

            CreateAddButtons();

            SyncPorts();

            RefreshPorts();

            _editor.MarkDirtyRepaint();

            SetPosition(
                new Rect(
                    action.NodePosition,
                    Vector2.zero));

            schedule.Execute(
                () =>
                {
                    RefreshPorts();
                    MarkDirtyRepaint();
                }).StartingIn(0);
        }


        // =========================================================
        // 内嵌编辑器
        // =========================================================

        private void DrawNodeEditor()
        {
            if (Action == null)
            {
                EditorGUILayout.HelpBox(
                    "Action 为空",
                    MessageType.Error);

                return;
            }

            ActionGraphData graph =
                _view != null
                    ? _view.Graph
                    : null;

            ActionFieldDrawer.Result result =
                ActionFieldDrawer.Draw(
                    _serialized,
                    Action,
                    graph,
                    false);

            MeasureAndResizeEditor();

            if (!result.Changed)
            {
                return;
            }

            title =
                GetDisplayTitle();

            if (result.Structural)
            {
                _view?.RequestReload();
            }
            else
            {
                _view?.RefreshAllNodeLabels();
            }
        }


        /// <summary>
        /// 依据 IMGUI 实际布局高度调整内嵌编辑器高度，
        /// 使节点随 KeyCommand 等列表的增减自适应，避免内容被裁剪。
        /// </summary>
        private void MeasureAndResizeEditor()
        {
            if (_editor == null)
            {
                return;
            }

            // 用一个零高度 sentinel 拿到当前所有内容的底部 Y，
            // 即内嵌编辑器需要的最小内容高度。
            // Layout 与 Repaint 事件都调用，保证布局缓存一致。
            Rect sentinel =
                GUILayoutUtility.GetRect(
                    GUIContent.none,
                    GUIStyle.none,
                    GUILayout.Height(0f));

            if (Event.current.type != UnityEngine.EventType.Repaint)
            {
                return;
            }

            float contentHeight =
                sentinel.y;

            // 与 minHeight 一同构成编辑器高度，并预留少量底部余量。
            float target =
                Mathf.Max(
                    220f,
                    contentHeight + 2f);

            float current =
                _editor.resolvedStyle.height;

            if (float.IsNaN(current))
            {
                current =
                    0f;
            }

            if (Mathf.Abs(current - target) > 0.5f)
            {
                _editor.style.height =
                    target;
            }
        }


        // =========================================================
        // 位置持久化
        // =========================================================

        public override void SetPosition(
            Rect newPos)
        {
            base.SetPosition(
                newPos);

            if (Action != null)
            {
                Action.NodePosition =
                    newPos.position;
            }
        }


        public void RefreshLabels()
        {
            title =
                GetDisplayTitle();
        }


        private string GetDisplayTitle()
        {
            if (Action == null ||
                string.IsNullOrEmpty(Action.Id))
            {
                return "Action";
            }

            return Action.Id;
        }


        // =========================================================
        // 按数据生成端口
        // =========================================================

        public void SyncPorts()
        {
            SyncOutputPorts();
            SyncInputPorts();

            RefreshPorts();
        }


        private void SyncOutputPorts()
        {
            List<BeCancelData> beCancels =
                Action != null &&
                Action.BeCancels != null
                    ? Action.BeCancels
                    : new List<BeCancelData>();

            beCancels.RemoveAll(
                item => item == null);


            // 删除已不在数据中的端口
            foreach (KeyValuePair<BeCancelData, Port> kv
                     in _outputPorts.ToList())
            {
                if (!beCancels.Contains(kv.Key))
                {
                    outputContainer.Remove(
                        kv.Value);

                    _outputPorts.Remove(
                        kv.Key);
                }
            }

            // 补齐缺失端口
            foreach (BeCancelData beCancel in beCancels)
            {
                if (!_outputPorts.ContainsKey(beCancel))
                {
                    Port port =
                        CreateTagPort(
                            Direction.Output,
                            beCancel.Tag,
                            beCancel,
                            newTag =>
                                OnBeCancelTagChanged(
                                    beCancel,
                                    newTag),
                            () =>
                                RemoveBeCancel(
                                    beCancel));

                    outputContainer.Add(
                        port);

                    _outputPorts[beCancel] =
                        port;
                }
            }

            // 确保"+"按钮始终在最底部
            EnsureAddButtonAtEnd(
                outputContainer,
                _addBeCancelButton);
        }


        private void SyncInputPorts()
        {
            List<CancelData> cancels =
                Action != null &&
                Action.Cancels != null
                    ? Action.Cancels
                    : new List<CancelData>();

            cancels.RemoveAll(
                item => item == null);


            foreach (KeyValuePair<CancelData, Port> kv
                     in _inputPorts.ToList())
            {
                if (!cancels.Contains(kv.Key))
                {
                    inputContainer.Remove(
                        kv.Value);

                    _inputPorts.Remove(
                        kv.Key);
                }
            }

            foreach (CancelData cancel in cancels)
            {
                if (!_inputPorts.ContainsKey(cancel))
                {
                    Port port =
                        CreateTagPort(
                            Direction.Input,
                            cancel.Tag,
                            cancel,
                            newTag =>
                                OnCancelTagChanged(
                                    cancel,
                                    newTag),
                            () =>
                                RemoveCancel(
                                    cancel));

                    inputContainer.Add(
                        port);

                    _inputPorts[cancel] =
                        port;
                }
            }

            EnsureAddButtonAtEnd(
                inputContainer,
                _addCancelButton);
        }


        /// <summary>
        /// 将"+"按钮移到容器末尾，保证它始终显示在最底部。
        /// </summary>
        private static void EnsureAddButtonAtEnd(
            VisualElement container,
            Button button)
        {
            if (container == null ||
                button == null)
            {
                return;
            }

            if (button.parent == container)
            {
                container.Remove(
                    button);
            }

            container.Add(
                button);
        }


        /// <summary>
        /// 根据 Tag 查找输出端口；优先返回对应的 BeCancel 端口。
        /// </summary>
        public Port GetOutputPort(
            string tag)
        {
            foreach (KeyValuePair<BeCancelData, Port> kv in _outputPorts)
            {
                if (kv.Key != null &&
                    kv.Key.Tag == tag)
                {
                    return kv.Value;
                }
            }

            return _outputPorts.Count > 0
                ? _outputPorts.Values.First()
                : null;
        }


        /// <summary>
        /// 根据 Tag 查找输入端口；优先返回对应的 Cancel 端口。
        /// </summary>
        public Port GetInputPort(
            string tag)
        {
            foreach (KeyValuePair<CancelData, Port> kv in _inputPorts)
            {
                if (kv.Key != null &&
                    kv.Key.Tag == tag)
                {
                    return kv.Value;
                }
            }

            return _inputPorts.Count > 0
                ? _inputPorts.Values.First()
                : null;
        }


        // =========================================================
        // "+" 按钮与增删操作
        // =========================================================

        private void CreateAddButtons()
        {
            _addCancelButton =
                CreateAddButton(
                    "+ Cancel",
                    AddCancel);

            _addBeCancelButton =
                CreateAddButton(
                    "+ BeCancel",
                    AddBeCancel);

            inputContainer.Add(
                _addCancelButton);

            outputContainer.Add(
                _addBeCancelButton);
        }


        private static Button CreateAddButton(
            string text,
            System.Action onClick)
        {
            Button button =
                new Button(
                    onClick)
                {
                    text = text,
                };

            button.style.height =
                18;

            button.style.marginTop =
                1;

            button.style.marginBottom =
                1;

            button.style.fontSize =
                10;

            return button;
        }


        private void AddCancel()
        {
            if (Action == null)
            {
                return;
            }

            Undo.RecordObject(
                Action,
                "Add Cancel");

            Action.Cancels.Add(
                new CancelData(
                    ""));

            EditorUtility.SetDirty(
                Action);

            SyncPorts();

            _view?.RefreshAllNodeLabels();
        }


        private void AddBeCancel()
        {
            if (Action == null)
            {
                return;
            }

            Undo.RecordObject(
                Action,
                "Add BeCancel");

            float duration =
                ActionDataUtility.GetDuration(
                    Action.ActionData);

            Action.BeCancels.Add(
                new BeCancelData(
                    "",
                    0f,
                    duration > 0f
                        ? duration
                        : 1f));

            EditorUtility.SetDirty(
                Action);

            SyncPorts();

            _view?.RefreshAllNodeLabels();
        }


        private void RemoveCancel(
            CancelData cancel)
        {
            if (Action == null ||
                cancel == null)
            {
                return;
            }

            ActionGraphData graph =
                _view != null
                    ? _view.Graph
                    : null;

            Undo.RecordObject(
                Action,
                "Remove Cancel");

            if (graph != null)
            {
                Undo.RecordObject(
                    graph,
                    "Remove Cancel");
            }

            Action.Cancels.Remove(
                cancel);

            // 删除 Cancel 后，可能产生无匹配 Tag 的孤立连线，一并清理。
            graph?.RemoveOrphanedTransitions(
                Action);

            EditorUtility.SetDirty(
                Action);

            if (graph != null)
            {
                EditorUtility.SetDirty(
                    graph);
            }

            SyncPorts();

            _view?.RequestReload();
        }


        private void RemoveBeCancel(
            BeCancelData beCancel)
        {
            if (Action == null ||
                beCancel == null)
            {
                return;
            }

            ActionGraphData graph =
                _view != null
                    ? _view.Graph
                    : null;

            Undo.RecordObject(
                Action,
                "Remove BeCancel");

            if (graph != null)
            {
                Undo.RecordObject(
                    graph,
                    "Remove BeCancel");
            }

            Action.BeCancels.Remove(
                beCancel);

            graph?.RemoveOrphanedTransitions(
                Action);

            EditorUtility.SetDirty(
                Action);

            if (graph != null)
            {
                EditorUtility.SetDirty(
                    graph);
            }

            SyncPorts();

            _view?.RequestReload();
        }


        private void OnCancelTagChanged(
            CancelData cancel,
            string newTag)
        {
            if (cancel == null ||
                cancel.Tag == newTag)
            {
                return;
            }

            Undo.RecordObject(
                Action,
                "Edit Cancel Tag");

            cancel.Tag =
                newTag;

            EditorUtility.SetDirty(
                Action);

            MarkDirtyRepaint();

            _view?.RefreshAllNodeLabels();
        }


        private void OnBeCancelTagChanged(
            BeCancelData beCancel,
            string newTag)
        {
            if (beCancel == null ||
                beCancel.Tag == newTag)
            {
                return;
            }

            Undo.RecordObject(
                Action,
                "Edit BeCancel Tag");

            beCancel.Tag =
                newTag;

            EditorUtility.SetDirty(
                Action);

            MarkDirtyRepaint();

            _view?.RefreshAllNodeLabels();
        }


        // =========================================================
        // 带 Tag 文本框的端口
        // =========================================================

        /// <summary>
        /// 创建一个端口，端口圆圈旁附带可编辑的 Tag 文本框和删除按钮。
        /// </summary>
        private static Port CreateTagPort(
            Direction direction,
            string tag,
            object userData,
            System.Action<string> onTagChanged,
            System.Action onRemove)
        {
            Port port =
                Port.Create<Edge>(
                    Orientation.Horizontal,
                    direction,
                    Port.Capacity.Multi,
                    typeof(bool));

            port.portName =
                string.Empty;

            port.userData =
                userData;


            // 隐藏 Port 默认的 Label，用可编辑 TextField 替代
            Label label =
                port.Q<Label>();

            if (label != null)
            {
                label.style.display =
                    DisplayStyle.None;
            }


            TextField tagField =
                new TextField
                {
                    value = tag ?? string.Empty,
                };

            tagField.style.maxWidth =
                90;

            tagField.style.minWidth =
                40;

            tagField.style.marginLeft =
                2;

            tagField.style.marginRight =
                2;

            tagField.style.fontSize =
                10;

            tagField.RegisterValueChangedCallback(
                evt =>
                {
                    onTagChanged?.Invoke(
                        evt.newValue);
                });

            // 阻止文本框上的指针事件冒泡到 Port，
            // 否则点击文本框会被当成拖拽连线起点。
            tagField.RegisterCallback<PointerDownEvent>(
                evt => evt.StopPropagation());

            tagField.RegisterCallback<MouseDownEvent>(
                evt => evt.StopPropagation());

            port.Add(
                tagField);


            if (onRemove != null)
            {
                Button removeButton =
                    new Button(
                        () => onRemove?.Invoke())
                    {
                        text = "×",
                    };

                removeButton.style.width =
                    16;

                removeButton.style.height =
                    16;

                removeButton.style.paddingLeft =
                    0;

                removeButton.style.paddingRight =
                    0;

                removeButton.style.marginLeft =
                    0;

                removeButton.style.marginRight =
                    0;

                removeButton.style.fontSize =
                    11;

                port.Add(
                    removeButton);
            }


            return port;
        }
    }


    /// <summary>
    /// KeyMap 枚举到显示符号的映射。
    /// </summary>
    public static class KeyMapGlyph
    {
        public static string GetGlyph(
            KeyMap key)
        {
            switch (key)
            {
                case KeyMap.Y:
                    return "Y";

                case KeyMap.B:
                    return "B";

                case KeyMap.A:
                    return "A";

                case KeyMap.X:
                    return "X";

                case KeyMap.Left:
                    return "←";

                case KeyMap.LeftUp:
                    return "↖";

                case KeyMap.Up:
                    return "↑";

                case KeyMap.RightUp:
                    return "↗";

                case KeyMap.Right:
                    return "→";

                case KeyMap.RightDown:
                    return "↘";

                case KeyMap.Down:
                    return "↓";

                case KeyMap.LeftDown:
                    return "↙";

                default:
                    return key.ToString();
            }
        }
    }
}
