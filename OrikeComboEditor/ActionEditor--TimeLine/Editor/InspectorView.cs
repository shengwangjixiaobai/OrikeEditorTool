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


        // 优先使用自定义名称
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
        // 如果当前正在编辑 Name
        // 不重新创建 Inspector
        //
        // 防止 TextField 输入过程中丢失焦点
        // =====================================================

        if (_nameField != null &&
            _nameField.panel != null &&
            _nameField.focusController != null &&
            _nameField.focusController.focusedElement ==
            _nameField)
        {
            return;
        }


        Refresh();
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


        return
            clip.GetType()
                .Name;
    }


}
