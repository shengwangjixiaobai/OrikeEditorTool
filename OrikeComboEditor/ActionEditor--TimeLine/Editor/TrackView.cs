using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class TrackView
{
    private readonly TimeLineController _controller;

    public const float TrackHeight =
        36f;


    private readonly TrackData _trackData;

    private readonly float _fps;

    private readonly float _frameWidth;


    private VisualElement _leftElement;

    private VisualElement _rightElement;


    // =========================================================
    // Track Name
    // =========================================================

    private Label _trackNameLabel;

    private TextField _trackNameField;

    private bool _isEditingTrackName;


    private readonly List<ClipView>
        _clipViews =
            new List<ClipView>();


    // =========================================================
    // Animation Picker
    // =========================================================

    private int _objectPickerControlID;

    private float _pendingAddTime;

    private bool _waitingForAnimationPicker;


    // =========================================================
    // Voice Picker
    // =========================================================

    private int _voicePickerControlID;

    private bool _waitingForVoicePicker;


    // =========================================================
    // Public
    // =========================================================

    public TrackData TrackData =>
        _trackData;


    public VisualElement LeftElement =>
        _leftElement;


    public VisualElement RightElement =>
        _rightElement;


    public IReadOnlyList<ClipView>
        ClipViews =>
        _clipViews;


    // =========================================================
    // Constructor
    // =========================================================

    public TrackView(
        TrackData trackData,
        float fps,
        float frameWidth,
        TimeLineController controller)
    {
        _trackData =
            trackData;

        _fps =
            fps;

        _frameWidth =
            frameWidth;

        _controller =
            controller;


        CreateLeftElement();

        CreateRightElement();

        BuildClipViews();

        RegisterTrackContextMenu();


        if (_controller != null)
        {
            _controller.OnTrackDataChanged +=
                OnTrackDataChanged;
        }
    }


    // =========================================================
    // Left Track
    // =========================================================

    private void CreateLeftElement()
    {
        _leftElement =
            new VisualElement();


        _leftElement.style.height =
            TrackHeight;

        _leftElement.style.flexShrink =
            0;

        _leftElement.style.position =
            Position.Relative;

        _leftElement.style.borderLeftWidth =
            3;

        _leftElement.style.borderLeftColor =
            GetTrackColor();


        _leftElement.style.backgroundColor =
            new Color(
                0.18f,
                0.18f,
                0.18f);


        _leftElement.style.borderBottomWidth =
            1;

        _leftElement.style.borderBottomColor =
            new Color(
                0.08f,
                0.08f,
                0.08f);


        // =====================================================
        // Track Name Label
        // =====================================================

        _trackNameLabel =
            new Label();


        RefreshTrackName();


        _trackNameLabel.style.flexGrow =
            1;

        _trackNameLabel.style.height =
            TrackHeight;

        _trackNameLabel.style.unityTextAlign =
            TextAnchor.MiddleLeft;

        _trackNameLabel.style.paddingLeft =
            6;

        _trackNameLabel.style.paddingRight =
            28;

        _trackNameLabel.style.fontSize =
            11;

        _trackNameLabel.style.color =
            new Color(
                0.8f,
                0.8f,
                0.8f);


        // =====================================================
        // Double Click Edit
        // =====================================================

        _trackNameLabel.RegisterCallback<
            PointerDownEvent>(
            OnTrackNamePointerDown);


        _leftElement.Add(
            _trackNameLabel);


        CreateTrackMenuButton();
    }


    // =========================================================
    // Track Name Pointer Down
    // =========================================================

    private void OnTrackNamePointerDown(
        PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }


        if (evt.clickCount < 2)
        {
            return;
        }


        BeginEditTrackName();


        evt.StopPropagation();

        evt.PreventDefault();
    }


    // =========================================================
    // Begin Edit Track Name
    // =========================================================

    private void BeginEditTrackName()
    {
        if (_isEditingTrackName)
        {
            return;
        }


        if (_trackData == null)
        {
            return;
        }


        _isEditingTrackName =
            true;


        if (_trackNameField == null)
        {
            CreateTrackNameField();
        }


        _trackNameField.SetValueWithoutNotify(
            _trackData.TrackName);


        // 隐藏 Label
        _trackNameLabel.style.display =
            DisplayStyle.None;


        // 添加 TextField
        if (_trackNameField.parent !=
            _leftElement)
        {
            _leftElement.Insert(
                0,
                _trackNameField);
        }


        _trackNameField.style.display =
            DisplayStyle.Flex;


        // =====================================================
        // 延迟一帧获取焦点
        //
        // 防止 UI Toolkit 刚添加元素时 Focus 失败
        // =====================================================

        _trackNameField.schedule.Execute(
            () =>
            {
                if (_trackNameField == null)
                {
                    return;
                }

                if (!_isEditingTrackName)
                {
                    return;
                }


                _trackNameField.Focus();

                _trackNameField.SelectAll();
            });
    }


    // =========================================================
    // Create Track Name Field
    // =========================================================

    private void CreateTrackNameField()
    {
        _trackNameField =
            new TextField();


        _trackNameField.style.position =
            Position.Absolute;

        _trackNameField.style.left =
            3;

        _trackNameField.style.right =
            28;

        _trackNameField.style.top =
            0;

        _trackNameField.style.height =
            TrackHeight;

        _trackNameField.style.fontSize =
            11;

        _trackNameField.style.paddingLeft =
            6;


        _trackNameField.RegisterCallback<
            KeyDownEvent>(
            OnTrackNameKeyDown);


        _trackNameField.RegisterCallback<
            FocusOutEvent>(
            OnTrackNameFocusOut);


        // 防止 TextField 的鼠标事件继续影响
        // Timeline Root
        _trackNameField.RegisterCallback<
            PointerDownEvent>(
            evt =>
            {
                evt.StopPropagation();
            });
    }


    // =========================================================
    // Track Name Key Down
    // =========================================================

    private void OnTrackNameKeyDown(
        KeyDownEvent evt)
    {
        if (!_isEditingTrackName)
        {
            return;
        }


        // =====================================================
        // Enter Save
        // =====================================================

        if (evt.keyCode ==
                KeyCode.Return ||
            evt.keyCode ==
                KeyCode.KeypadEnter)
        {
            EndEditTrackName(
                true);


            evt.StopPropagation();

            evt.PreventDefault();

            return;
        }


        // =====================================================
        // Escape Cancel
        // =====================================================

        if (evt.keyCode ==
            KeyCode.Escape)
        {
            EndEditTrackName(
                false);


            evt.StopPropagation();

            evt.PreventDefault();
        }
    }


    // =========================================================
    // Track Name Focus Out
    // =========================================================

    private void OnTrackNameFocusOut(
        FocusOutEvent evt)
    {
        if (!_isEditingTrackName)
        {
            return;
        }


        EndEditTrackName(
            true);
    }


    // =========================================================
    // End Edit Track Name
    // =========================================================

    private void EndEditTrackName(
        bool save)
    {
        if (!_isEditingTrackName)
        {
            return;
        }


        _isEditingTrackName =
            false;


        // =====================================================
        // Save
        // =====================================================

        if (save &&
            _trackData != null &&
            _trackNameField != null)
        {
            string newName =
                _trackNameField.value;


            if (string.IsNullOrWhiteSpace(
                    newName))
            {
                newName =
                    "Track";
            }


            _controller?.SetTrackName(
                _trackData,
                newName);
        }


        // =====================================================
        // Remove Field
        // =====================================================

        if (_trackNameField != null)
        {
            _trackNameField.Blur();


            _trackNameField.style.display =
                DisplayStyle.None;


            if (_trackNameField.parent ==
                _leftElement)
            {
                _leftElement.Remove(
                    _trackNameField);
            }
        }


        // =====================================================
        // Show Label
        // =====================================================

        if (_trackNameLabel != null)
        {
            _trackNameLabel.style.display =
                DisplayStyle.Flex;
        }


        RefreshTrackName();
    }


    // =========================================================
    // Refresh Track Name
    // =========================================================

    private void RefreshTrackName()
    {
        if (_trackNameLabel == null)
        {
            return;
        }


        if (_trackData == null)
        {
            _trackNameLabel.text =
                "Track";

            return;
        }


        _trackNameLabel.text =
            string.IsNullOrEmpty(
                _trackData.TrackName)
                ? "Track"
                : _trackData.TrackName;
    }


    // =========================================================
    // Track Data Changed
    // =========================================================

    private void OnTrackDataChanged(
        TrackData trackData)
    {
        if (trackData !=
            _trackData)
        {
            return;
        }


        RefreshTrackName();
    }


    // =========================================================
    // Track Menu Button
    // =========================================================

    private void CreateTrackMenuButton()
    {
        VisualElement menuButton =
            new VisualElement();


        menuButton.style.width =
            24;

        menuButton.style.height =
            TrackHeight;

        menuButton.style.position =
            Position.Absolute;

        menuButton.style.right =
            0;

        menuButton.style.top =
            0;

        menuButton.style.justifyContent =
            Justify.Center;

        menuButton.style.alignItems =
            Align.Center;

        menuButton.tooltip =
            "Track Menu";


        Label dots =
            new Label(
                "\u22EE");

        dots.style.fontSize =
            16;

        dots.style.color =
            new Color(
                0.65f,
                0.65f,
                0.65f);

        dots.style.unityTextAlign =
            TextAnchor.MiddleCenter;

        dots.pickingMode =
            PickingMode.Ignore;


        menuButton.Add(
            dots);


        menuButton.RegisterCallback<
            PointerDownEvent>(
            OnTrackMenuButtonPointerDown);


        _leftElement.Add(
            menuButton);
    }


    private void OnTrackMenuButtonPointerDown(
        PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }


        ShowTrackMenu();


        evt.StopPropagation();

        evt.PreventDefault();
    }


    private void ShowTrackMenu()
    {
        if (_controller == null ||
            _trackData == null)
        {
            return;
        }


        GenericMenu menu =
            new GenericMenu();


        menu.AddItem(
            new GUIContent(
                "Delete Track"),
            false,
            () =>
            {
                _controller.DeleteTrack(
                    _trackData);
            });


        menu.ShowAsContext();
    }


    // =========================================================
    // Track Color
    // =========================================================

    private Color GetTrackColor()
    {
        if (_trackData != null &&
            _trackData.ClipType ==
            ClipType.Animation)
        {
            return new Color(
                0.25f,
                0.55f,
                1.0f);
        }


        if (_trackData != null &&
            _trackData.ClipType ==
            ClipType.Voice)
        {
            return new Color(
                0.25f,
                0.85f,
                0.45f);
        }


        return new Color(
            0.7f,
            0.7f,
            0.7f);
    }


    // =========================================================
    // Right Track
    // =========================================================

    private void CreateRightElement()
    {
        _rightElement =
            new VisualElement();


        _rightElement.style.height =
            TrackHeight;

        _rightElement.style.flexShrink =
            0;

        _rightElement.style.position =
            Position.Relative;

        _rightElement.style.overflow =
            Overflow.Hidden;

        _rightElement.style.backgroundColor =
            new Color(
                0.10f,
                0.10f,
                0.10f);


        _rightElement.style.borderBottomWidth =
            1;

        _rightElement.style.borderBottomColor =
            new Color(
                0.08f,
                0.08f,
                0.08f);
    }


    // =========================================================
    // Build Clips
    // =========================================================

    private void BuildClipViews()
    {
        if (_trackData == null ||
            _trackData.Clips == null)
        {
            return;
        }


        foreach (
            BaseClipData clipData
            in _trackData.Clips)
        {
            if (clipData == null)
            {
                continue;
            }


            ClipView clipView =
                CreateClipView(
                    clipData);


            _clipViews.Add(
                clipView);


            _rightElement.Add(
                clipView);
        }


        LayoutClips();
    }


    private ClipView CreateClipView(
        BaseClipData clipData)
    {
        return new ClipView(
            clipData,
            _fps,
            _frameWidth,
            _controller);
    }


    // =========================================================
    // Context Menu
    // =========================================================

    private void RegisterTrackContextMenu()
    {
        _rightElement.RegisterCallback<
            PointerDownEvent>(
            OnTrackPointerDown);
    }


    private void OnTrackPointerDown(
        PointerDownEvent evt)
    {
        if (evt.button != 1)
        {
            return;
        }


        // -----------------------------------------------------
        // 如果右键点击的是 Clip
        // ClipView 自己处理
        // -----------------------------------------------------

        VisualElement picked =
            evt.target
                as VisualElement;


        if (picked !=
            _rightElement)
        {
            return;
        }


        float localX =
            evt.localPosition.x;


        float pixelsPerSecond =
            _fps *
            _frameWidth;


        float time =
            pixelsPerSecond > 0f
                ? localX /
                  pixelsPerSecond
                : 0f;


        time =
            Mathf.Max(
                0f,
                time);


        ShowTrackContextMenu(
            time);


        evt.StopPropagation();
    }


    // =========================================================
    // Track Context Menu
    // =========================================================

    private void ShowTrackContextMenu(
        float time)
    {
        GenericMenu menu =
            new GenericMenu();


        if (_trackData == null)
        {
            return;
        }


        // -----------------------------------------------------
        // Animation Track
        // -----------------------------------------------------

        if (_trackData.ClipType ==
            ClipType.Animation)
        {
            menu.AddItem(
                new GUIContent(
                    "Add/Animation Clip"),
                false,
                () =>
                {
                    OpenAnimationPicker(
                        time);
                });
        }


        // -----------------------------------------------------
        // Voice Track
        // -----------------------------------------------------

        else if (_trackData.ClipType ==
                 ClipType.Voice)
        {
            menu.AddItem(
                new GUIContent(
                    "Add/Voice Clip"),
                false,
                () =>
                {
                    OpenVoicePicker(
                        time);
                });
        }


        menu.AddSeparator(
            "");


        // -----------------------------------------------------
        // Paste
        // -----------------------------------------------------

        menu.AddItem(
            new GUIContent(
                "Paste"),
            _controller != null &&
            _controller.HasClipboard,
            () =>
            {
                _controller.PasteClips(
                    _trackData,
                    time);
            });


        menu.ShowAsContext();
    }


    // =========================================================
    // Animation Picker
    // =========================================================

    private void OpenAnimationPicker(
        float time)
    {
        _pendingAddTime =
            time;


        _objectPickerControlID =
            GUIUtility.GetControlID(
                FocusType.Passive);


        _waitingForAnimationPicker =
            true;


        EditorGUIUtility.ShowObjectPicker<
            AnimationClip>(
            null,
            false,
            "",
            _objectPickerControlID);


        EditorApplication.update +=
            CheckAnimationPicker;
    }


    private void CheckAnimationPicker()
    {
        if (!_waitingForAnimationPicker)
        {
            EditorApplication.update -=
                CheckAnimationPicker;

            return;
        }


        int pickerID =
            EditorGUIUtility
                .GetObjectPickerControlID();


        if (pickerID !=
            _objectPickerControlID)
        {
            return;
        }


        Object selectedObject =
            EditorGUIUtility
                .GetObjectPickerObject();


        if (selectedObject == null)
        {
            return;
        }


        AnimationClip animation =
            selectedObject
                as AnimationClip;


        if (animation != null)
        {
            _controller.AddAnimationClip(
                _trackData,
                animation,
                _pendingAddTime);
        }


        _waitingForAnimationPicker =
            false;


        EditorApplication.update -=
            CheckAnimationPicker;
    }


    // =========================================================
    // Voice Picker
    // =========================================================

    private void OpenVoicePicker(
        float time)
    {
        _pendingAddTime =
            time;


        _voicePickerControlID =
            GUIUtility.GetControlID(
                FocusType.Passive);


        _waitingForVoicePicker =
            true;


        EditorGUIUtility.ShowObjectPicker<
            AudioClip>(
            null,
            false,
            "",
            _voicePickerControlID);


        EditorApplication.update +=
            CheckVoicePicker;
    }


    private void CheckVoicePicker()
    {
        if (!_waitingForVoicePicker)
        {
            EditorApplication.update -=
                CheckVoicePicker;

            return;
        }


        int pickerID =
            EditorGUIUtility
                .GetObjectPickerControlID();


        if (pickerID !=
            _voicePickerControlID)
        {
            return;
        }


        Object selectedObject =
            EditorGUIUtility
                .GetObjectPickerObject();


        if (selectedObject == null)
        {
            return;
        }


        AudioClip voice =
            selectedObject
                as AudioClip;


        if (voice != null)
        {
            _controller.AddVoiceClip(
                _trackData,
                voice,
                _pendingAddTime);
        }


        _waitingForVoicePicker =
            false;


        EditorApplication.update -=
            CheckVoicePicker;
    }


    // =========================================================
    // Layout
    // =========================================================

    private void LayoutClips()
    {
        foreach (
            ClipView clipView
            in _clipViews)
        {
            if (clipView == null)
            {
                continue;
            }


            clipView.UpdateLayout(
                _fps,
                _frameWidth);
        }
    }


    public void UpdateLayout()
    {
        LayoutClips();
    }

}
