using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using ObjectField =
UnityEditor.UIElements.ObjectField;

public class InspectorView : VisualElement
{
    private readonly TimeLineController _controller;

    private Label _titleLabel;

    private VisualElement _content;

    private TextField _nameField;

    private FloatField _startTimeField;

    private FloatField _lengthField;

    private FloatField _endTimeField;

    private ObjectField _animationField;

    private ObjectField _voiceField;

    private ObjectField _effectField;


    private bool _updating;


    public InspectorView(
        TimeLineController controller)
    {
        _controller =
            controller;


        style.flexDirection =
            FlexDirection.Column;

        style.flexGrow =
            1;

        style.flexShrink =
            0;

        style.width =
            280;

        style.minWidth =
            240;

        style.backgroundColor =
            new Color(
                0.16f,
                0.16f,
                0.16f);


        CreateHeader();

        CreateContent();


        if (_controller != null)
        {
            _controller.OnSelectionChanged +=
                OnSelectionChanged;

            _controller.OnClipDataChanged +=
                OnClipDataChanged;
        }


        Refresh();
    }


    // =========================================================
    // Header
    // =========================================================

    private void CreateHeader()
    {
        VisualElement header =
            new VisualElement();


        header.style.height =
            32;

        header.style.flexShrink =
            0;

        header.style.flexDirection =
            FlexDirection.Row;

        header.style.alignItems =
            Align.Center;

        header.style.paddingLeft =
            8;

        header.style.borderBottomWidth =
            1;

        header.style.borderBottomColor =
            new Color(
                0.08f,
                0.08f,
                0.08f);


        _titleLabel =
            new Label(
                "Inspector");

        _titleLabel.style.fontSize =
            12;

        _titleLabel.style.unityFontStyleAndWeight =
            FontStyle.Bold;


        header.Add(
            _titleLabel);


        Add(
            header);
    }


    // =========================================================
    // Content
    // =========================================================

    private void CreateContent()
    {
        _content =
            new VisualElement();


        _content.style.flexGrow =
            1;

        _content.style.flexShrink =
            1;

        _content.style.paddingLeft =
            8;

        _content.style.paddingRight =
            8;

        _content.style.paddingTop =
            8;

        _content.style.paddingBottom =
            8;


        Add(
            _content);
    }


    // =========================================================
    // Refresh
    // =========================================================

    private void Refresh()
    {
        if (_content == null)
        {
            return;
        }


        _updating =
            true;


        _content.Clear();


        BaseClipData clip =
            _controller != null
                ? _controller.SelectedClip
                : null;


        if (clip == null)
        {
            Label emptyLabel =
                new Label(
                    "No Clip Selected");

            emptyLabel.style.fontSize =
                11;

            emptyLabel.style.color =
                new Color(
                    0.55f,
                    0.55f,
                    0.55f);


            _content.Add(
                emptyLabel);


            _updating =
                false;

            return;
        }


        CreateCommonFields(
            clip);


        if (clip
            is AnimationClipData
                animationClipData)
        {
            CreateAnimationFields(
                animationClipData);
        }


        if (clip
            is VoiceClipData
                voiceClipData)
        {
            CreateVoiceFields(
                voiceClipData);
        }


        if (clip
            is EffectClipData
                effectClipData)
        {
            CreateEffectFields(
                effectClipData);
        }


        // Hitbox / Behitbox 共用
        // （BehitboxClipData 继承 HitboxClipData）
        if (clip
            is HitboxClipData
                hitboxClipData)
        {
            CreateHitboxFields(
                hitboxClipData);
        }


        _updating =
            false;
    }


    // =========================================================
    // Common Fields
    // =========================================================

    private void CreateCommonFields(
        BaseClipData clip)
    {
        // =====================================================
        // Type
        // =====================================================

        Label typeLabel =
            new Label(
                GetClipTypeName(
                    clip));

        typeLabel.style.marginBottom =
            8;

        typeLabel.style.unityFontStyleAndWeight =
            FontStyle.Bold;


        _content.Add(
            typeLabel);


        // =====================================================
        // Name
        // =====================================================

        _nameField =
            new TextField(
                "Name");

        _nameField.value =
            GetClipDisplayName(
                clip);


        // -----------------------------------------------------
        // Focus Out Save
        // -----------------------------------------------------

        _nameField.RegisterCallback<
            FocusOutEvent>(
            evt =>
            {
                if (_updating)
                {
                    return;
                }

                if (_controller == null)
                {
                    return;
                }


                _controller.SetClipName(
                    clip,
                    _nameField.value);
            });


        // -----------------------------------------------------
        // Enter Save
        // -----------------------------------------------------

        _nameField.RegisterCallback<
            KeyDownEvent>(
            evt =>
            {
                if (_updating)
                {
                    return;
                }

                if (_controller == null)
                {
                    return;
                }


                if (evt.keyCode ==
                        KeyCode.Return ||
                    evt.keyCode ==
                        KeyCode.KeypadEnter)
                {
                    _controller.SetClipName(
                        clip,
                        _nameField.value);

                    _nameField.Blur();

                    evt.StopPropagation();

                    evt.PreventDefault();
                }
            });


        _content.Add(
            _nameField);


        // =====================================================
        // Start Time
        // =====================================================

        _startTimeField =
            new FloatField(
                "Start Time");

        _startTimeField.value =
            clip.StartTime;


        _startTimeField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }


                _controller.SetClipStartTime(
                    clip,
                    evt.newValue);
            });


        _content.Add(
            _startTimeField);


        // =====================================================
        // Length
        // =====================================================

        _lengthField =
            new FloatField(
                "Length");

        _lengthField.value =
            clip.Length;


        _lengthField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }


                _controller.SetClipLength(
                    clip,
                    evt.newValue);
            });


        _content.Add(
            _lengthField);


        // =====================================================
        // End Time
        // =====================================================

        _endTimeField =
            new FloatField(
                "End Time");

        _endTimeField.value =
            clip.EndTime;

        _endTimeField.isReadOnly =
            true;


        _content.Add(
            _endTimeField);
    }


    // =========================================================
    // Get Clip Display Name
    // =========================================================

    private string GetClipDisplayName(
        BaseClipData clip)
    {
        if (clip == null)
        {
            return string.Empty;
        }


        // ????????????????
        if (!string.IsNullOrEmpty(
                clip.Name))
        {
            return clip.Name;
        }


        // =====================================================
        // Animation
        // =====================================================

        if (clip
            is AnimationClipData
                animationClipData)
        {
            if (animationClipData.Animation
                != null)
            {
                return animationClipData
                    .Animation
                    .name;
            }

            return "Animation";
        }


        // =====================================================
        // Voice
        // =====================================================

        if (clip
            is VoiceClipData
                voiceClipData)
        {
            if (voiceClipData.Voice
                != null)
            {
                return voiceClipData
                    .Voice
                    .name;
            }

            return "Voice";
        }


        // =====================================================
        // Effect
        // =====================================================

        if (clip
            is EffectClipData
                effectClipData)
        {
            if (effectClipData.EffectPrefab
                != null)
            {
                return effectClipData
                    .EffectPrefab
                    .name;
            }

            return "Effect";
        }


        // =====================================================
        // Hitbox / Behitbox
        // =====================================================

        if (clip
            is BehitboxClipData
                behitboxClipData)
        {
            if (behitboxClipData.HitboxPrefab
                != null)
            {
                return behitboxClipData
                    .HitboxPrefab
                    .name;
            }

            return "Behitbox";
        }

        if (clip
            is HitboxClipData
                hitboxClipData)
        {
            if (hitboxClipData.HitboxPrefab
                != null)
            {
                return hitboxClipData
                    .HitboxPrefab
                    .name;
            }

            return "Hitbox";
        }


        return string.Empty;
    }


    // =========================================================
    // Animation
    // =========================================================

    private void CreateAnimationFields(
        AnimationClipData clip)
    {
        _animationField =
            new ObjectField(
                "Animation");

        _animationField.objectType =
            typeof(
                AnimationClip);

        _animationField.allowSceneObjects =
            false;

        _animationField.value =
            clip.Animation;


        _animationField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }


                AnimationClip animation =
                    evt.newValue
                        as AnimationClip;


                _controller.SetAnimation(
                    clip,
                    animation);
            });


        _content.Add(
            _animationField);
    }


    // =========================================================
    // Voice
    // =========================================================

    private void CreateVoiceFields(
        VoiceClipData clip)
    {
        _voiceField =
            new ObjectField(
                "Voice");

        _voiceField.objectType =
            typeof(
                AudioClip);

        _voiceField.allowSceneObjects =
            false;

        _voiceField.value =
            clip.Voice;


        _voiceField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }


                AudioClip voice =
                    evt.newValue
                        as AudioClip;


                _controller.SetVoice(
                    clip,
                    voice);
            });


        _content.Add(
            _voiceField);
    }


    // =========================================================
    // Effect
    // =========================================================

    private void CreateEffectFields(
        EffectClipData clip)
    {
        _effectField =
            new ObjectField(
                "Effect");

        _effectField.objectType =
            typeof(
                GameObject);

        _effectField.allowSceneObjects =
            false;

        _effectField.value =
            clip.EffectPrefab;

        _effectField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }

                GameObject effect =
                    evt.newValue
                        as GameObject;

                _controller.SetEffect(
                    clip,
                    effect);
            });

        _content.Add(
            _effectField);


        // =====================================================
        // Attach Bone
        // =====================================================

        TextField attachBoneField =
            new TextField(
                "Attach Bone");

        attachBoneField.value =
            clip.AttachBone ?? string.Empty;

        attachBoneField.tooltip =
            "骨骼名（空 = 角色根）；递归查找角色 Transform 树。" +
            "例如 RightHand / Head / Spine02";

        attachBoneField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }

                _controller.SetEffectAttachBone(
                    clip,
                    evt.newValue ?? string.Empty);
            });

        _content.Add(
            attachBoneField);


        // =====================================================
        // Local Offset
        // =====================================================

        Vector3Field localOffsetField =
            new Vector3Field(
                "Local Offset");

        localOffsetField.value =
            clip.LocalOffset;

        localOffsetField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }

                _controller.SetEffectLocalOffset(
                    clip,
                    evt.newValue);
            });

        _content.Add(
            localOffsetField);
    }


    // =========================================================
    // Hitbox / Behitbox
    // =========================================================

    private void CreateHitboxFields(
        HitboxClipData clip)
    {
        ObjectField hitboxField =
            new ObjectField(
                "Hitbox");

        hitboxField.objectType =
            typeof(
                GameObject);

        hitboxField.allowSceneObjects =
            false;

        hitboxField.value =
            clip.HitboxPrefab;

        hitboxField.tooltip =
            "挂载 Hitbox 脚本 + Collider 的 GameObject。" +
            "编辑器预览在时间段内 activate=true 并 collider.enabled=true；" +
            "游戏中 collider 始终 enabled，由 activate 决定生效。";

        hitboxField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }

                GameObject hitbox =
                    evt.newValue
                        as GameObject;

                _controller.SetHitbox(
                    clip,
                    hitbox);
            });

        _content.Add(
            hitboxField);


        // =====================================================
        // Attach Bone
        // =====================================================

        TextField attachBoneField =
            new TextField(
                "Attach Bone");

        attachBoneField.value =
            clip.AttachBone ?? string.Empty;

        attachBoneField.tooltip =
            "骨骼名（空 = 角色根）；递归查找角色 Transform 树。";

        attachBoneField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }

                _controller.SetHitboxAttachBone(
                    clip,
                    evt.newValue ?? string.Empty);
            });

        _content.Add(
            attachBoneField);


        // =====================================================
        // Local Offset
        // =====================================================

        Vector3Field localOffsetField =
            new Vector3Field(
                "Local Offset");

        localOffsetField.value =
            clip.LocalOffset;

        localOffsetField.RegisterValueChangedCallback(
            evt =>
            {
                if (_updating)
                {
                    return;
                }

                _controller.SetHitboxLocalOffset(
                    clip,
                    evt.newValue);
            });

        _content.Add(
            localOffsetField);
    }


    // =========================================================
    // Selection
    // =========================================================

    private void OnSelectionChanged(
        BaseClipData clip)
    {
        Refresh();
    }


    private void OnClipDataChanged(
        BaseClipData clip)
    {
        if (_controller == null)
        {
            return;
        }


        if (_controller.SelectedClip !=
            clip)
        {
            return;
        }


        // =====================================================
        // ??????????? Name
        // ????????? Inspector
        //
        // ??? TextField ????????ж??????
        // =====================================================

        if (IsAnyInputFieldFocused())
        {
            return;
        }


        Refresh();
    }


    // =========================================================
    // Is Any Input Field Focused
    //
    // 检查当前聚焦的元素是否是输入字段
    // （TextField / Vector3Field / ObjectField 及其子元素）
    // 通过向上遍历父节点判断
    //
    // 如果正在编辑任意输入字段，不刷新 Inspector
    // 否则字段会被 Refresh 重建导致失去焦点
    // =========================================================

    private bool IsAnyInputFieldFocused()
    {
        if (_content == null ||
            _content.panel == null ||
            _content.panel.focusController == null)
        {
            return false;
        }

        var focused =
            _content
                .panel
                .focusController
                .focusedElement;

        // focusedElement 类型是 Focusable
        // parent 属性只有 VisualElement 才有
        VisualElement e =
            focused as VisualElement;

        if (e == null)
        {
            return false;
        }

        while (e != null &&
               e != _content)
        {
            if (e is TextField ||
                e is Vector3Field ||
                e is ObjectField)
            {
                return true;
            }

            e = e.parent;
        }

        return false;
    }


    // =========================================================
    // Clip Type
    // =========================================================

    private string GetClipTypeName(
        BaseClipData clip)
    {
        if (clip
            is AnimationClipData)
        {
            return
                "Animation Clip";
        }


        if (clip
            is VoiceClipData)
        {
            return
                "Voice Clip";
        }


        if (clip
            is EffectClipData)
        {
            return
                "Effect Clip";
        }


        if (clip
            is BehitboxClipData)
        {
            return
                "Behitbox Clip";
        }


        if (clip
            is HitboxClipData)
        {
            return
                "Hitbox Clip";
        }


        return
            clip.GetType()
                .Name;
    }


}
