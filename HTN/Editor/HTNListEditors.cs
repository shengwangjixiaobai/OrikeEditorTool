using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Orike.HTN.Editor
{
    /// <summary>
    /// 条件 / 效果列表的通用编辑控件构建器，
    /// 供任务与方法 Inspector 复用。
    /// 所有修改都会 Undo.RecordObject(owner, 操作名) 并触发 onChanged。
    /// </summary>
    public static class HTNListEditors
    {
        // =========================================================
        // 通用小控件
        // =========================================================

        /// <summary>区块标题行：标题 + 右侧 “＋添加” 按钮。</summary>
        public static VisualElement SectionHeader(
            string title,
            string addButtonLabel,
            Action onAdd)
        {
            VisualElement header =
                new VisualElement();

            header.style.flexDirection =
                FlexDirection.Row;

            header.style.alignItems =
                Align.Center;

            header.style.marginTop = 8;
            header.style.marginBottom = 4;

            Label label =
                new Label(title);

            label.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            label.style.flexGrow = 1f;

            header.Add(label);

            if (onAdd != null)
            {
                Button button =
                    new Button(onAdd)
                    {
                        text = addButtonLabel ?? "＋",
                    };

                button.style.width = 90;

                header.Add(button);
            }

            return header;
        }


        /// <summary>带边框的区块容器。</summary>
        public static VisualElement Box()
        {
            VisualElement box =
                new VisualElement();

            box.style.marginTop = 4;
            box.style.marginBottom = 4;
            box.style.paddingTop = 6;
            box.style.paddingBottom = 6;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 8;

            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;

            box.style.borderTopColor =
                new Color(0.2f, 0.2f, 0.2f);
            box.style.borderBottomColor =
                new Color(0.2f, 0.2f, 0.2f);
            box.style.borderLeftColor =
                new Color(0.2f, 0.2f, 0.2f);
            box.style.borderRightColor =
                new Color(0.2f, 0.2f, 0.2f);

            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;

            box.style.backgroundColor =
                new Color(0.16f, 0.16f, 0.16f);

            return box;
        }


        private static DropdownField PropertyDropdown(
            HTNDomain domain,
            string current)
        {
            List<string> choices =
                new List<string>();

            if (domain != null)
            {
                foreach (HTNWorldStateProperty property in domain.WorldStateProperties)
                {
                    if (property != null &&
                        !string.IsNullOrEmpty(property.Name))
                    {
                        choices.Add(property.Name);
                    }
                }
            }

            DropdownField dropdown =
                new DropdownField(
                    choices,
                    string.IsNullOrEmpty(current)
                        ? choices.Count > 0
                            ? choices[0]
                            : ""
                        : choices.Contains(current)
                            ? current
                            : choices.Count > 0
                                ? choices[0]
                                : "");

            dropdown.style.width = 150;

            if (choices.Count == 0)
            {
                dropdown.tooltip =
                    "定义域还没有世界状态属性，请先通过工具栏“世界状态属性”添加";
            }

            return dropdown;
        }


        private static IntegerField ValueField(int value)
        {
            IntegerField field =
                new IntegerField
                {
                    value = value,
                };

            field.style.width = 60;

            return field;
        }


        private static Button DeleteButton(Action onClick)
        {
            Button button =
                new Button(onClick)
                {
                    text = "×",
                };

            button.style.width = 22;

            return button;
        }


        // =========================================================
        // 条件列表编辑
        // =========================================================

        /// <summary>
        /// 构建条件列表编辑控件。
        /// </summary>
        /// <param name="domain">提供世界状态属性下拉选项。</param>
        /// <param name="conditions">要编辑的条件列表。</param>
        /// <param name="owner">Undo 记录对象（任务或方法所在的 ScriptableObject）。</param>
        /// <param name="onChanged">任意修改后的回调（刷新 / 自动保存）。</param>
        public static VisualElement BuildConditionsEditor(
            HTNDomain domain,
            List<HTNCondition> conditions,
            Object owner,
            Action onChanged)
        {
            VisualElement container =
                new VisualElement();

            container.Add(
                SectionHeader(
                    "条件（全部满足才可用）",
                    "＋条件",
                    () =>
                    {
                        RecordUndo(owner);

                        conditions.Add(new HTNCondition());

                        onChanged?.Invoke();
                    }));

            for (int i = 0; i < conditions.Count; i++)
            {
                int capturedIndex = i;

                HTNCondition condition = conditions[i];

                VisualElement row =
                    new VisualElement();

                row.style.flexDirection =
                    FlexDirection.Row;

                row.style.alignItems =
                    Align.Center;

                row.style.marginBottom = 2;


                DropdownField propertyDropdown =
                    PropertyDropdown(domain, condition.Property);

                propertyDropdown.RegisterValueChangedCallback(_ =>
                {
                    RecordUndo(owner);

                    condition.Property = propertyDropdown.value;

                    onChanged?.Invoke();
                });

                row.Add(propertyDropdown);


                DropdownField typeDropdown =
                    new DropdownField(
                        new List<string>
                        {
                            "==", "!=", ">", ">=", "<", "<=",
                        },
                        ConditionTypeLabel(condition.Type));

                typeDropdown.style.width = 52;

                typeDropdown.RegisterValueChangedCallback(_ =>
                {
                    RecordUndo(owner);

                    condition.Type = ParseConditionType(typeDropdown.value);

                    onChanged?.Invoke();
                });

                row.Add(typeDropdown);


                IntegerField valueField =
                    ValueField(condition.Value);

                valueField.RegisterValueChangedCallback(evt =>
                {
                    RecordUndo(owner);

                    condition.Value = evt.newValue;

                    onChanged?.Invoke();
                });

                row.Add(valueField);


                row.Add(DeleteButton(() =>
                {
                    RecordUndo(owner);

                    conditions.RemoveAt(capturedIndex);

                    onChanged?.Invoke();
                }));

                container.Add(row);
            }

            return container;
        }


        private static string ConditionTypeLabel(HTNConditionType type)
        {
            return type switch
            {
                HTNConditionType.Equal => "==",
                HTNConditionType.NotEqual => "!=",
                HTNConditionType.Greater => ">",
                HTNConditionType.GreaterOrEqual => ">=",
                HTNConditionType.Less => "<",
                HTNConditionType.LessOrEqual => "<=",
                _ => "==",
            };
        }


        private static HTNConditionType ParseConditionType(string label)
        {
            return label switch
            {
                "!=" => HTNConditionType.NotEqual,
                ">" => HTNConditionType.Greater,
                ">=" => HTNConditionType.GreaterOrEqual,
                "<" => HTNConditionType.Less,
                "<=" => HTNConditionType.LessOrEqual,
                _ => HTNConditionType.Equal,
            };
        }


        // =========================================================
        // 效果列表编辑
        // =========================================================

        /// <summary>
        /// 构建效果列表编辑控件（含“期望效果”开关）。
        /// </summary>
        public static VisualElement BuildEffectsEditor(
            HTNDomain domain,
            List<HTNEffect> effects,
            Object owner,
            Action onChanged)
        {
            VisualElement container =
                new VisualElement();

            container.Add(
                SectionHeader(
                    "效果（成功执行后改变世界状态）",
                    "＋效果",
                    () =>
                    {
                        RecordUndo(owner);

                        effects.Add(new HTNEffect());

                        onChanged?.Invoke();
                    }));

            for (int i = 0; i < effects.Count; i++)
            {
                int capturedIndex = i;

                HTNEffect effect = effects[i];

                VisualElement row =
                    new VisualElement();

                row.style.flexDirection =
                    FlexDirection.Row;

                row.style.alignItems =
                    Align.Center;

                row.style.marginBottom = 2;


                DropdownField propertyDropdown =
                    PropertyDropdown(domain, effect.Property);

                propertyDropdown.RegisterValueChangedCallback(_ =>
                {
                    RecordUndo(owner);

                    effect.Property = propertyDropdown.value;

                    onChanged?.Invoke();
                });

                row.Add(propertyDropdown);


                DropdownField opDropdown =
                    new DropdownField(
                        new List<string>
                        {
                            "=", "+=", "-=", "置true", "置false",
                        },
                        EffectOpLabel(effect.Op));

                opDropdown.style.width = 66;

                opDropdown.RegisterValueChangedCallback(_ =>
                {
                    RecordUndo(owner);

                    effect.Op = ParseEffectOp(opDropdown.value);

                    onChanged?.Invoke();
                });

                row.Add(opDropdown);


                IntegerField valueField =
                    ValueField(effect.Value);

                // 置 true / false 时数值无意义，禁用输入
                valueField.SetEnabled(
                    effect.Op != HTNEffectOp.SetTrue &&
                    effect.Op != HTNEffectOp.SetFalse);

                valueField.RegisterValueChangedCallback(evt =>
                {
                    RecordUndo(owner);

                    effect.Value = evt.newValue;

                    onChanged?.Invoke();
                });

                row.Add(valueField);


                Toggle expectedToggle =
                    new Toggle("期望")
                    {
                        value = effect.Expected,
                    };

                expectedToggle.tooltip =
                    "期望效果：只在规划 / 计划校验阶段生效，用于表达执行过程中感知器理应造成的变化";

                expectedToggle.style.marginLeft = 4;

                expectedToggle.RegisterValueChangedCallback(evt =>
                {
                    RecordUndo(owner);

                    effect.Expected = evt.newValue;

                    onChanged?.Invoke();
                });

                row.Add(expectedToggle);


                row.Add(DeleteButton(() =>
                {
                    RecordUndo(owner);

                    effects.RemoveAt(capturedIndex);

                    onChanged?.Invoke();
                }));

                container.Add(row);
            }

            return container;
        }


        private static string EffectOpLabel(HTNEffectOp op)
        {
            return op switch
            {
                HTNEffectOp.Set => "=",
                HTNEffectOp.Add => "+=",
                HTNEffectOp.Subtract => "-=",
                HTNEffectOp.SetTrue => "置true",
                HTNEffectOp.SetFalse => "置false",
                _ => "=",
            };
        }


        private static HTNEffectOp ParseEffectOp(string label)
        {
            return label switch
            {
                "+=" => HTNEffectOp.Add,
                "-=" => HTNEffectOp.Subtract,
                "置true" => HTNEffectOp.SetTrue,
                "置false" => HTNEffectOp.SetFalse,
                _ => HTNEffectOp.Set,
            };
        }


        private static void RecordUndo(Object owner)
        {
            if (owner != null)
            {
                Undo.RecordObject(
                    owner,
                    "Edit HTN Data");
            }
        }
    }
}
