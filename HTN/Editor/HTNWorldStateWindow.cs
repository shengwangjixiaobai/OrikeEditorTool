using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orike.HTN.Editor
{
    /// <summary>
    /// 世界状态属性编辑窗口：维护定义域的属性列表（名字 / 默认值 / 备注）。
    /// 支持增删改与上下移动，实时通知主窗口刷新。
    /// </summary>
    public class HTNWorldStateWindow : EditorWindow
    {
        private HTNDomain _domain;

        private Action _onChanged;

        private VisualElement _listRoot;


        /// <summary>
        /// 打开（或聚焦到已打开的）世界状态属性窗口。
        /// </summary>
        /// <param name="domain">要编辑的定义域。</param>
        /// <param name="onChanged">属性列表变化后的回调。</param>
        public static void Open(
            HTNDomain domain,
            Action onChanged)
        {
            HTNWorldStateWindow window =
                GetWindow<HTNWorldStateWindow>();

            window.titleContent =
                new GUIContent(
                    $"世界状态属性 ｜ {domain.name}");

            window.minSize =
                new Vector2(
                    560,
                    320);

            window._domain = domain;

            window._onChanged = onChanged;

            window.RebuildList();
        }


        private void OnEnable()
        {
            if (_listRoot != null &&
                _domain != null)
            {
                RebuildList();
            }
        }


        private void CreateGUI()
        {
            VisualElement root =
                new VisualElement();

            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            rootVisualElement.Add(root);


            VisualElement header =
                new VisualElement();

            header.style.flexDirection =
                FlexDirection.Row;

            header.style.alignItems =
                Align.Center;

            header.style.marginBottom = 6;

            Label titleLabel =
                new Label("世界状态属性（条件 / 效果通过名字引用）");

            titleLabel.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            titleLabel.style.flexGrow = 1f;

            header.Add(titleLabel);

            Button addButton =
                new Button(AddProperty)
                {
                    text = "＋ 属性",
                };

            addButton.style.width = 80;

            header.Add(addButton);

            root.Add(header);


            // 列头
            VisualElement columnHeader =
                new VisualElement();

            columnHeader.style.flexDirection =
                FlexDirection.Row;

            Label nameLabel =
                new Label("属性名");

            nameLabel.style.width = 200;

            nameLabel.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            columnHeader.Add(nameLabel);

            Label defaultLabel =
                new Label("默认值");

            defaultLabel.style.width = 80;

            defaultLabel.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            columnHeader.Add(defaultLabel);

            Label commentLabel =
                new Label("备注（值域说明，如 0=近 1=中 2=远）");

            commentLabel.style.flexGrow = 1f;

            commentLabel.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            columnHeader.Add(commentLabel);

            Label spacer =
                new Label("操作");

            spacer.style.width = 128;

            spacer.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            columnHeader.Add(spacer);

            root.Add(columnHeader);

            _listRoot =
                new VisualElement();

            _listRoot.style.flexGrow = 1f;

            root.Add(_listRoot);
        }


        private void RebuildList()
        {
            if (_listRoot == null ||
                _domain == null)
            {
                return;
            }

            _listRoot.Clear();

            for (int i = 0; i < _domain.WorldStateProperties.Count; i++)
            {
                int capturedIndex = i;

                HTNWorldStateProperty property =
                    _domain.WorldStateProperties[i];

                if (property == null)
                {
                    continue;
                }

                VisualElement row =
                    new VisualElement();

                row.style.flexDirection =
                    FlexDirection.Row;

                row.style.alignItems =
                    Align.Center;

                row.style.marginBottom = 3;


                TextField nameField =
                    new TextField
                    {
                        value = property.Name,
                        isDelayed = true,
                    };

                nameField.style.width = 200;

                nameField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_domain, "Edit HTN World State");

                    property.Name = evt.newValue;

                    NotifyChanged();
                });

                row.Add(nameField);

                IntegerField defaultField =
                    new IntegerField
                    {
                        value = property.DefaultValue,
                    };

                defaultField.style.width = 80;

                defaultField.style.marginRight = 4;

                defaultField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_domain, "Edit HTN World State");

                    property.DefaultValue = evt.newValue;

                    NotifyChanged();
                });

                row.Add(defaultField);

                TextField commentField =
                    new TextField
                    {
                        value = property.Comment ?? "",
                        isDelayed = true,
                    };

                commentField.style.flexGrow = 1f;

                commentField.style.marginRight = 4;

                commentField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_domain, "Edit HTN World State");

                    property.Comment = evt.newValue;

                    NotifyChanged();
                });

                row.Add(commentField);


                // 上移 / 下移 / 删除
                row.Add(BuildMiniButton("↑", () => MoveProperty(capturedIndex, -1)));

                row.Add(BuildMiniButton("↓", () => MoveProperty(capturedIndex, 1)));

                Button deleteButton =
                    new Button(
                        () => DeleteProperty(capturedIndex))
                    {
                        text = "×",
                    };

                deleteButton.style.width = 22;

                deleteButton.style.marginLeft = 2;

                deleteButton.tooltip =
                    "删除属性（条件 / 效果中对它的引用会失效并触发运行时警告）";

                row.Add(deleteButton);

                _listRoot.Add(row);
            }

            if (_domain.WorldStateProperties.Count == 0)
            {
                Label empty =
                    new Label("还没有属性。点右上 “＋ 属性” 添加，例如 WsCanSeeEnemy。");

                empty.style.color =
                    new Color(0.6f, 0.6f, 0.6f);

                empty.style.marginTop = 12;

                _listRoot.Add(empty);
            }
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


        private void AddProperty()
        {
            if (_domain == null)
            {
                return;
            }

            Undo.RecordObject(_domain, "Add HTN World State");

            string baseName =
                "WsProperty";

            int index = 1;

            while (_domain.WorldStateProperties.Exists(
                       p => p != null && p.Name == $"{baseName}_{index}"))
            {
                index++;
            }

            _domain.WorldStateProperties.Add(
                new HTNWorldStateProperty
                {
                    Name = $"{baseName}_{index}",
                });

            EditorUtility.SetDirty(_domain);

            RebuildList();

            NotifyChanged();
        }


        private void DeleteProperty(int index)
        {
            if (_domain == null ||
                index < 0 ||
                index >= _domain.WorldStateProperties.Count)
            {
                return;
            }

            Undo.RecordObject(_domain, "Delete HTN World State");

            _domain.WorldStateProperties.RemoveAt(index);

            EditorUtility.SetDirty(_domain);

            RebuildList();

            NotifyChanged();
        }


        private void MoveProperty(
            int index,
            int delta)
        {
            if (_domain == null)
            {
                return;
            }

            int target = index + delta;

            if (index < 0 ||
                target < 0 ||
                index >= _domain.WorldStateProperties.Count ||
                target >= _domain.WorldStateProperties.Count)
            {
                return;
            }

            Undo.RecordObject(_domain, "Reorder HTN World State");

            HTNWorldStateProperty temp =
                _domain.WorldStateProperties[target];

            _domain.WorldStateProperties[target] =
                _domain.WorldStateProperties[index];

            _domain.WorldStateProperties[index] =
                temp;

            EditorUtility.SetDirty(_domain);

            RebuildList();

            NotifyChanged();
        }


        private void NotifyChanged()
        {
            EditorUtility.SetDirty(_domain);

            _onChanged?.Invoke();
        }
    }
}
