using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace Orike.HTN.Editor
{
    /// <summary>
    /// HTNDomainEditorWindow 的右侧属性面板：
    /// 选中树上的任务 / 方法 / 未挂接任务时编辑其属性。
    ///
    ///   - 复合任务：名称、说明、实现方法列表（名称 / 条件 / 子任务）
    ///   - 基元任务：名称、说明、操作函数、参数、条件、效果
    ///   - 方法：方法名、条件、子任务（含引用已有任务）
    ///
    /// 继承 ScrollView：方法 / 条件 / 效果配置较多时内容会超出面板高度，
    /// 由内置滚动条滚动而不是被裁掉。
    /// </summary>
    public class HTNInspectorPanel : ScrollView
    {
        private readonly HTNDomainEditorWindow _window;

        private VisualElement _content;

        private object _selected;


        public HTNInspectorPanel(HTNDomainEditorWindow window)
            : base(ScrollViewMode.Vertical)
        {
            _window = window;

            // 占满分隔条右侧的剩余宽度
            style.flexGrow = 1f;

            style.paddingTop = 8;
            style.paddingBottom = 8;
            style.paddingLeft = 10;
            style.paddingRight = 10;

            style.borderLeftWidth = 1;

            style.borderLeftColor =
                new Color(0.1f, 0.1f, 0.1f);

            _content =
                new VisualElement();

            _content.style.flexGrow = 1f;

            Add(_content);

            ShowEmpty();
        }


        /// <summary>
        /// 展示当前选中的对象（任务或方法引用）；null 时显示占位提示。
        /// </summary>
        public void Show(object selected)
        {
            _selected = selected;

            Rebuild();
        }


        /// <summary>结构变化（如方法增删）后原地重建当前面板。</summary>
        public void Refresh()
        {
            Rebuild();
        }


        private void Rebuild()
        {
            _content.Clear();

            switch (_selected)
            {
                case HTNCompoundTask compoundTask:
                    BuildCompoundEditor(compoundTask);

                    break;

                case HTNPrimitiveTask primitiveTask:
                    BuildPrimitiveEditor(primitiveTask);

                    break;

                case HTNMethodRef methodRef:
                    BuildMethodEditor(methodRef);

                    break;

                default:
                    ShowEmpty();

                    break;
            }
        }


        private void ShowEmpty()
        {
            Label label =
                new Label(
                    "在左侧选择一个任务或方法\n\n" +
                    "空白处右键可添加任务 / 打开世界状态属性\n" +
                    "复合任务行右键可设为根任务 / 重命名 / 删除\n" +
                    "方法行右键可调整优先级 / 添加子任务");

            label.style.whiteSpace =
                WhiteSpace.Normal;

            label.style.color =
                new Color(0.6f, 0.6f, 0.6f);

            _content.Add(label);
        }


        // =========================================================
        // 通用字段
        // =========================================================

        private TextField NameField(
            HTNTask task)
        {
            TextField field =
                new TextField("任务名")
                {
                    value = task.name,
                    isDelayed = true,
                };

            field.RegisterValueChangedCallback(evt =>
            {
                HTNDomainAssetOps.RenameTask(
                    task,
                    evt.newValue);

                _window.MarkStructureChanged();
            });

            return field;
        }


        private TextField DescriptionField(
            HTNTask task)
        {
            TextField field =
                new TextField("说明")
                {
                    value = task.Description ?? "",
                    isDelayed = true,
                };

            field.multiline = true;

            field.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(task, "Edit HTN Task");

                task.Description = evt.newValue;

                _window.MarkDirty();
            });

            return field;
        }


        // =========================================================
        // 复合任务
        // =========================================================

        private void BuildCompoundEditor(HTNCompoundTask task)
        {
            _content.Add(BuildTitle($"复合任务 ｜ {task.name}"));

            _content.Add(NameField(task));

            _content.Add(DescriptionField(task));

            Label hint =
                new Label(
                    "实现方法按列表顺序决定优先级：排在越前面的方法，" +
                    "条件满足时越优先被选中。");

            hint.style.whiteSpace =
                WhiteSpace.Normal;

            hint.style.color =
                new Color(0.65f, 0.65f, 0.65f);

            hint.style.marginTop = 6;

            _content.Add(hint);


            for (int i = 0; i < task.Methods.Count; i++)
            {
                int capturedIndex = i;

                HTNMethod method = task.Methods[i];

                if (method == null)
                {
                    continue;
                }

                VisualElement box =
                    HTNListEditors.Box();

                // 方法头：优先级序号 + 名称 + 删除
                VisualElement header =
                    new VisualElement();

                header.style.flexDirection =
                    FlexDirection.Row;

                header.style.alignItems =
                    Align.Center;

                Label indexLabel =
                    new Label($"方法 {i}");

                indexLabel.style.unityFontStyleAndWeight =
                    FontStyle.Bold;

                indexLabel.style.width = 56;

                header.Add(indexLabel);

                TextField nameField =
                    new TextField("名称")
                    {
                        value = method.MethodName ?? "",
                        isDelayed = true,
                    };

                nameField.style.flexGrow = 1f;

                nameField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(task, "Edit HTN Method");

                    method.MethodName = evt.newValue;

                    _window.MarkDirty();
                });

                header.Add(nameField);

                Button deleteButton =
                    new Button(
                        () => _window.DeleteMethod(
                            new HTNMethodRef
                            {
                                Owner = task,
                                Method = method,
                                Index = capturedIndex,
                            }))
                    {
                        text = "删除方法",
                    };

                deleteButton.style.marginLeft = 4;

                header.Add(deleteButton);

                box.Add(header);

                // 方法条件 / 子任务
                box.Add(
                    HTNListEditors.BuildConditionsEditor(
                        _window.Domain,
                        method.Conditions,
                        task,
                        () => _window.MarkStructureChanged()));

                box.Add(
                    BuildSubTasksEditor(
                        task,
                        method));

                _content.Add(box);
            }


            Button addMethodButton =
                new Button(() => _window.AddMethod(task))
                {
                    text = "＋ 添加实现方法",
                };

            addMethodButton.style.marginTop = 4;

            _content.Add(addMethodButton);
        }


        // =========================================================
        // 基元任务
        // =========================================================

        private void BuildPrimitiveEditor(HTNPrimitiveTask task)
        {
            _content.Add(BuildTitle($"基元任务 ｜ {task.name}"));

            _content.Add(NameField(task));

            _content.Add(DescriptionField(task));


            // 操作函数下拉（注册表里的 ID + 自定义）
            List<string> operatorIds =
                HTNOperatorRegistry.GetAllIds();

            if (!operatorIds.Contains(task.OperatorId) &&
                !string.IsNullOrEmpty(task.OperatorId))
            {
                operatorIds.Insert(0, task.OperatorId);
            }

            DropdownField operatorDropdown =
                new DropdownField(
                    "操作函数",
                    operatorIds,
                    string.IsNullOrEmpty(task.OperatorId) && operatorIds.Count > 0
                        ? operatorIds[0]
                        : task.OperatorId);

            operatorDropdown.tooltip =
                "操作函数在运行期由 HTNOperatorRegistry 解析；" +
                "项目自定义的操作函数需先 Register";

            operatorDropdown.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(task, "Edit HTN Task");

                task.OperatorId = evt.newValue;

                _window.MarkDirty();
            });

            _content.Add(operatorDropdown);


            TextField parametersField =
                new TextField("参数")
                {
                    value = task.Parameters ?? "",
                    isDelayed = true,
                };

            parametersField.multiline = true;

            parametersField.tooltip =
                "传给操作函数的参数，格式由具体操作函数约定。" +
                "内置 Mock 的格式：时长秒[|日志文本]，如 “1.5|冲向敌人”";

            parametersField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(task, "Edit HTN Task");

                task.Parameters = evt.newValue;

                _window.MarkDirty();
            });

            _content.Add(parametersField);


            _content.Add(
                HTNListEditors.BuildConditionsEditor(
                    _window.Domain,
                    task.Conditions,
                    task,
                    () => _window.MarkDirty()));

            _content.Add(
                HTNListEditors.BuildEffectsEditor(
                    _window.Domain,
                    task.Effects,
                    task,
                    () => _window.MarkDirty()));
        }


        // =========================================================
        // 方法（从树上选中）
        // =========================================================

        private void BuildMethodEditor(HTNMethodRef methodRef)
        {
            HTNMethod method = methodRef.Method;

            if (method == null ||
                methodRef.Owner == null)
            {
                ShowEmpty();

                return;
            }

            _content.Add(BuildTitle(
                $"方法 {methodRef.Index} ｜ {methodRef.Owner.name}"));

            TextField nameField =
                new TextField("方法名")
                {
                    value = method.MethodName ?? "",
                    isDelayed = true,
                };

            nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(methodRef.Owner, "Edit HTN Method");

                method.MethodName = evt.newValue;

                _window.MarkStructureChanged();
            });

            _content.Add(nameField);

            _content.Add(
                HTNListEditors.BuildConditionsEditor(
                    _window.Domain,
                    method.Conditions,
                    methodRef.Owner,
                    () => _window.MarkStructureChanged()));

            _content.Add(
                BuildSubTasksEditor(
                    methodRef.Owner,
                    method));
        }


        // =========================================================
        // 子任务编辑（新建 + 引用已有任务）
        // =========================================================

        private VisualElement BuildSubTasksEditor(
            HTNCompoundTask owner,
            HTNMethod method)
        {
            VisualElement container =
                new VisualElement();

            container.Add(
                HTNListEditors.SectionHeader(
                    "子任务（顺序执行）",
                    null,
                    null));

            for (int i = 0; i < method.SubTasks.Count; i++)
            {
                int capturedIndex = i;

                VisualElement row =
                    new VisualElement();

                row.style.flexDirection =
                    FlexDirection.Row;

                row.style.alignItems =
                    Align.Center;

                row.style.marginBottom = 2;


                Label indexLabel =
                    new Label($"{i + 1}.");

                indexLabel.style.width = 24;

                indexLabel.style.color =
                    new Color(0.6f, 0.6f, 0.6f);

                row.Add(indexLabel);


                HTNTask subTask = method.SubTasks[i];

                ObjectField taskField =
                    new ObjectField
                    {
                        objectType = typeof(HTNTask),
                        value = subTask,
                        allowSceneObjects = false,
                    };

                taskField.style.flexGrow = 1f;

                taskField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(owner, "Edit HTN SubTasks");

                    if (evt.newValue is HTNTask newTask &&
                        _window.Domain != null &&
                        !_window.Domain.Tasks.Contains(newTask))
                    {
                        // 引用了其他域的任务：拒绝
                        taskField.value = subTask;

                        return;
                    }

                    method.SubTasks[capturedIndex] =
                        evt.newValue as HTNTask;

                    _window.MarkStructureChanged();
                });

                row.Add(taskField);


                // 上移 / 下移 / 删除
                row.Add(BuildMiniButton("↑", () =>
                {
                    if (capturedIndex <= 0)
                    {
                        return;
                    }

                    Undo.RecordObject(owner, "Reorder HTN SubTasks");

                    HTNTask temp = method.SubTasks[capturedIndex - 1];

                    method.SubTasks[capturedIndex - 1] =
                        method.SubTasks[capturedIndex];

                    method.SubTasks[capturedIndex] =
                        temp;

                    _window.MarkStructureChanged();
                }));

                row.Add(BuildMiniButton("↓", () =>
                {
                    if (capturedIndex >= method.SubTasks.Count - 1)
                    {
                        return;
                    }

                    Undo.RecordObject(owner, "Reorder HTN SubTasks");

                    HTNTask temp = method.SubTasks[capturedIndex + 1];

                    method.SubTasks[capturedIndex + 1] =
                        method.SubTasks[capturedIndex];

                    method.SubTasks[capturedIndex] =
                        temp;

                    _window.MarkStructureChanged();
                }));

                row.Add(BuildMiniButton("×", () =>
                {
                    Undo.RecordObject(owner, "Remove HTN SubTask");

                    method.SubTasks.RemoveAt(capturedIndex);

                    _window.MarkStructureChanged();
                }));

                container.Add(row);
            }


            // 新建 / 引用已有任务
            VisualElement addRow =
                new VisualElement();

            addRow.style.flexDirection =
                FlexDirection.Row;

            addRow.style.marginTop = 2;

            Button newPrimitiveButton =
                new Button(
                    () => _window.CreateSubTask(
                        owner,
                        method,
                        true))
                {
                    text = "＋新建基元",
                };

            newPrimitiveButton.style.flexGrow = 1f;

            addRow.Add(newPrimitiveButton);

            Button newCompoundButton =
                new Button(
                    () => _window.CreateSubTask(
                        owner,
                        method,
                        false))
                {
                    text = "＋新建复合",
                };

            newCompoundButton.style.flexGrow = 1f;

            newCompoundButton.style.marginLeft = 4;

            addRow.Add(newCompoundButton);

            container.Add(addRow);

            return container;
        }


        private Button BuildMiniButton(
            string text,
            Action onClick)
        {
            Button button =
                new Button(onClick)
                {
                    text = text,
                };

            button.style.width = 22;
            button.style.marginLeft = 2;

            return button;
        }


        private Label BuildTitle(string title)
        {
            Label label =
                new Label(title);

            label.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            label.style.fontSize = 13;

            label.style.marginBottom = 6;

            label.style.color =
                new Color(0.9f, 0.85f, 0.6f);

            return label;
        }
    }
}
