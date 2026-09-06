using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class TrackView
{
    private readonly TimeLineController _controller;

    public const float TrackHeight = 36f;

    private readonly TrackData _trackData;

    private readonly float _fps;
    private readonly float _frameWidth;

    private VisualElement _leftElement;
    private VisualElement _rightElement;

    private readonly System.Collections.Generic.List<ClipView>
        _clipViews =
        new System.Collections.Generic.List<ClipView>();

    private int _objectPickerControlID;

    private float _pendingAddTime;

    private bool _waitingForObjectPicker;


    public TrackData TrackData =>
        _trackData;

    public VisualElement LeftElement =>
        _leftElement;

    public VisualElement RightElement =>
        _rightElement;


    public TrackView(
        TrackData trackData,
        float fps,
        float frameWidth,
        TimeLineController controller)
    {
        _trackData = trackData;

        _fps = fps;

        _frameWidth = frameWidth;

        _controller = controller;

        CreateLeftElement();

        CreateRightElement();

        BuildClipViews();

        RegisterTrackContextMenu();
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

        Label label =
            new Label();

        label.text =
            string.IsNullOrEmpty(
                _trackData?.TrackName)
                ? "Track"
                : _trackData.TrackName;

        label.style.flexGrow =
            1;

        label.style.unityTextAlign =
            TextAnchor.MiddleLeft;

        label.style.paddingLeft =
            6;

        label.style.fontSize =
            11;

        label.style.color =
            new Color(
                0.8f,
                0.8f,
                0.8f);

        label.pickingMode =
            PickingMode.Ignore;

        _leftElement.Add(
            label);
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
    // Build Clip Views
    // =========================================================

    private void BuildClipViews()
    {
        if (_trackData == null ||
            _trackData.Clips == null)
        {
            return;
        }

        foreach (BaseClipData clipData
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
    // Track Context Menu
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

        // 如果点击的是 Clip，本事件通常已经被 ClipView 拦截。
        // 这里根据 picked element 判断是否为空白区域。
        VisualElement picked =
            evt.target as VisualElement;

        if (picked != _rightElement)
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
                ? localX / pixelsPerSecond
                : 0f;

        time =
            Mathf.Max(
                0f,
                time);

        ShowTrackContextMenu(
            time);

        evt.StopPropagation();
    }


    private void ShowTrackContextMenu(
        float time)
    {
        GenericMenu menu =
            new GenericMenu();

        menu.AddItem(
            new GUIContent(
                "Add/Animation Clip"),
            false,
            () =>
            {
                OpenAnimationPicker(
                    time);
            });

        menu.AddSeparator("");

        menu.AddItem(
            new GUIContent("Paste"),
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

        _waitingForObjectPicker =
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
        if (!_waitingForObjectPicker)
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
            selectedObject as AnimationClip;

        if (animation != null)
        {
            _controller.AddAnimationClip(
                _trackData,
                animation,
                _pendingAddTime);
        }

        _waitingForObjectPicker =
            false;

        EditorApplication.update -=
            CheckAnimationPicker;
    }


    // =========================================================
    // Layout
    // =========================================================

    private void LayoutClips()
    {
        foreach (ClipView clipView
                 in _clipViews)
        {
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