using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orike.HTN.Editor
{
    /// <summary>
    /// 规划测试面板（主窗口底部）：
    /// 用临时编辑的世界状态跑一遍规划器，
    /// 展示最终计划、MTR 与逐步分解日志，验证域的正确性。
    ///
    /// 编辑的值只存在于窗口会话中（SerializeField），
    /// 不写回定义域的默认值；感知器在运行期的真实更新不受影响。
    /// </summary>
    public class HTNPlanTestPanel : VisualElement
    {
        private readonly HTNDomainEditorWindow _window;

        /// <summary>测试用世界状态值缓存（下标对齐域属性），域切换时重建。</summary>
        private List<int> _testValues =
            new List<int>();

        private readonly List<HTNPlanLogEntry> _lastLog =
            new List<HTNPlanLogEntry>();

        private Label _resultLabel;

        private Label _planLabel;

        private VisualElement _stateRow;

        private ScrollView _logView;

        private HTNPlan _lastPlan;

        private bool _useMtr;


        public HTNPlanTestPanel(HTNDomainEditorWindow window)
        {
            _window = window;

            style.borderTopWidth = 1;

            style.borderTopColor =
                new Color(0.1f, 0.1f, 0.1f);

            style.paddingTop = 6;
            style.paddingBottom = 6;
            style.paddingLeft = 10;
            style.paddingRight = 10;

            // 高度由主窗口按分隔条拖拽结果指定
            style.flexShrink = 0f;

            BuildSelf();
        }


        private void BuildSelf()
        {
            // 头部：标题 + 世界状态 + 规划按钮
            VisualElement header =
                new VisualElement();

            header.style.flexDirection =
                FlexDirection.Row;

            header.style.alignItems =
                Align.Center;

            Label title =
                new Label("规划测试");

            title.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            title.style.width = 70;

            header.Add(title);

            _stateRow =
                new VisualElement();

            _stateRow.style.flexDirection =
                FlexDirection.Row;

            _stateRow.style.flexGrow = 1f;

            _stateRow.style.flexWrap =
                Wrap.Wrap;

            header.Add(_stateRow);

            Toggle mtrToggle =
                new Toggle("MTR约束")
                {
                    value = _useMtr,
                };

            mtrToggle.tooltip =
                "打开后模拟“当前有计划在运行”的重规划：受 MTR 优先级约束，只能得到优先级相同或更高的计划";

            mtrToggle.RegisterValueChangedCallback(evt =>
            {
                _useMtr = evt.newValue;
            });

            mtrToggle.style.marginLeft = 8;

            header.Add(mtrToggle);

            Button runButton =
                new Button(RunPlan)
                {
                    text = "▶ 规划",
                };

            runButton.style.width = 76;

            runButton.style.marginLeft = 8;

            header.Add(runButton);

            Add(header);


            // 结果摘要
            _resultLabel =
                new Label("尚未规划");

            _resultLabel.style.marginTop = 4;

            _resultLabel.style.color =
                new Color(0.6f, 0.6f, 0.6f);

            _resultLabel.style.whiteSpace =
                WhiteSpace.Normal;

            Add(_resultLabel);

            _planLabel =
                new Label("");

            _planLabel.style.marginTop = 2;

            _planLabel.style.whiteSpace =
                WhiteSpace.Normal;

            _planLabel.style.color =
                new Color(0.55f, 0.85f, 0.65f);

            Add(_planLabel);


            // 日志区
            _logView =
                new ScrollView(ScrollViewMode.Vertical);

            _logView.style.marginTop = 4;

            // 面板被拖高时日志区跟着变高
            _logView.style.flexGrow = 1f;

            _logView.style.minHeight = 60;

            _logView.style.backgroundColor =
                new Color(0.12f, 0.12f, 0.12f);

            Add(_logView);
        }


        /// <summary>
        /// 域切换 / 属性定义变化后重建状态编辑行。
        /// </summary>
        public void RebuildStateFields()
        {
            _stateRow.Clear();

            HTNDomain domain = _window.Domain;

            if (domain == null)
            {
                return;
            }

            // 状态值缓存长度对齐属性数量
            while (_testValues.Count < domain.WorldStateProperties.Count)
            {
                HTNWorldStateProperty property =
                    domain.WorldStateProperties[_testValues.Count];

                _testValues.Add(
                    property != null
                        ? property.DefaultValue
                        : 0);
            }

            if (_testValues.Count > domain.WorldStateProperties.Count)
            {
                _testValues.RemoveRange(
                    domain.WorldStateProperties.Count,
                    _testValues.Count - domain.WorldStateProperties.Count);
            }

            for (int i = 0; i < domain.WorldStateProperties.Count; i++)
            {
                int capturedIndex = i;

                HTNWorldStateProperty property =
                    domain.WorldStateProperties[i];

                if (property == null)
                {
                    continue;
                }

                IntegerField field =
                    new IntegerField(property.Name)
                    {
                        value = _testValues[i],
                    };

                field.style.width = 150;

                field.style.marginRight = 6;

                if (!string.IsNullOrEmpty(property.Comment))
                {
                    field.tooltip = property.Comment;
                }

                field.RegisterValueChangedCallback(evt =>
                {
                    _testValues[capturedIndex] =
                        evt.newValue;
                });

                _stateRow.Add(field);
            }
        }


        private void RunPlan()
        {
            HTNDomain domain = _window.Domain;

            if (domain == null)
            {
                return;
            }

            HTNWorldState state = domain.CreateWorldState();

            for (int i = 0; i < domain.WorldStateProperties.Count && i < _testValues.Count; i++)
            {
                state.Set(i, _testValues[i]);
            }

            _lastLog.Clear();

            HTNPlanner planner = new HTNPlanner();

            _lastPlan =
                _useMtr
                    ? planner.Plan(domain, state, _lastLog, _lastPlan)
                    : planner.Plan(domain, state, _lastLog);

            RefreshResult();
        }


        private void RefreshResult()
        {
            if (_lastPlan == null)
            {
                return;
            }

            if (_lastPlan.Success)
            {
                _resultLabel.text =
                    $"✓ 规划成功（{_lastPlan.Tasks.Count} 个基元任务" +
                    (_useMtr ? "，带 MTR 约束" : "") +
                    "）";

                _resultLabel.style.color =
                    new Color(0.55f, 0.85f, 0.65f);

                _planLabel.text =
                    "计划：" + _lastPlan.Describe();
            }
            else
            {
                _resultLabel.text =
                    "✗ 规划失败：" + _lastPlan.FailReason;

                _resultLabel.style.color =
                    new Color(0.9f, 0.5f, 0.45f);

                _planLabel.text = "";
            }


            // 日志
            _logView.Clear();

            foreach (HTNPlanLogEntry entry in _lastLog)
            {
                Color color;

                switch (entry.EntryKind)
                {
                    case HTNPlanLogEntry.Kind.Decompose:

                        color =
                            new Color(0.7f, 0.8f, 1f);

                        break;

                    case HTNPlanLogEntry.Kind.PrimitiveAccepted:

                        color =
                            new Color(0.55f, 0.85f, 0.65f);

                        break;

                    case HTNPlanLogEntry.Kind.CompoundFailed:
                    case HTNPlanLogEntry.Kind.PrimitiveRejected:

                        color =
                            new Color(0.9f, 0.65f, 0.45f);

                        break;

                    case HTNPlanLogEntry.Kind.Backtrack:

                        color =
                            new Color(0.9f, 0.5f, 0.45f);

                        break;

                    default:

                        color =
                            Color.white;

                        break;
                }

                Label line =
                    new Label(entry.Message);

                line.style.color = color;

                line.style.whiteSpace =
                    WhiteSpace.Normal;

                _logView.Add(line);
            }
        }
    }
}
