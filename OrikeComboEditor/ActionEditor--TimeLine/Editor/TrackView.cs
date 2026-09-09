using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class TrackView
{
    private readonly TimeLineController _controller;

    public const float TrackHeight =
        36f;

    // Point Event 标记尺寸（长方形 + 三角形）
    public const string PointEventMarkerClassName =
        "point-event-marker";

    private const float MarkerWidth =
        9f;

    private const float MarkerHeight =
        14f;

    // 三角形尖端相对 marker 左边缘的 x 偏移
    private const float MarkerTipOffsetX =
        4f;

    // 标记底部距轨道底边的间距
    private const float MarkerBottomMargin =
        3f;


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
    // Point Event Markers
    // =========================================================

    private readonly List<VisualElement>
        _pointEventMarkers =
            new List<VisualElement>();


    // =========================================================
    // Point Event 拖拽状态
    // =========================================================

    private PointEventData _draggingPointEvent;

    private int _draggingPointerId =
        -1;

    private float _lastDragPointerX;


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
    // Effect Picker
    // =========================================================

    private int _effectPickerControlID;

    private bool _waitingForEffectPicker;


    // =========================================================
    // Hitbox Picker
    // =========================================================

    private int _hitboxPickerControlID;

    private bool _waitingForHitboxPicker;


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

        BuildPointEventMarkers();

        RegisterTrackContextMenu();


        if (_controller != null)
        {
            _controller.OnTrackDataChanged +=
                OnTrackDataChanged;

            _controller.OnPointEventSelectionChanged +=
                OnPointEventSelectionChanged;

            _controller.OnPointEventChanged +=
                OnPointEventChanged;
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


        // ???? Label
        _trackNameLabel.style.display =
            DisplayStyle.None;


        // ???? TextField
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
        // ????????????
        //
        // ??? UI Toolkit ?????????? Focus ???
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


        // ??? TextField ???????????????
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


        if (_trackData != null &&
            _trackData.ClipType ==
            ClipType.Effect)
        {
            return new Color(
                0.85f,
                0.45f,
                0.25f);
        }


        if (_trackData != null &&
            _trackData.ClipType ==
            ClipType.Hitbox)
        {
            return new Color(
                0.95f,
                0.25f,
                0.25f);
        }


        if (_trackData != null &&
            _trackData.ClipType ==
            ClipType.Behitbox)
        {
            return new Color(
                0.65f,
                0.25f,
                0.85f);
        }


        if (_trackData != null &&
            _trackData.ClipType ==
            ClipType.StateEvent)
        {
            return new Color(
                1f,
                0.85f,
                0.25f);
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


    // =========================================================
    // Build Point Event Markers
    //
    // 点事件显示为菱形标记，不是 Clip 片段
    // =========================================================

    private void BuildPointEventMarkers()
    {
        // 清除旧标记
        foreach (
            VisualElement marker
            in _pointEventMarkers)
        {
            _rightElement.Remove(
                marker);
        }

        _pointEventMarkers.Clear();


        if (_trackData == null ||
            _trackData.PointEvents == null)
        {
            return;
        }


        foreach (
            PointEventData pointEvent
            in _trackData.PointEvents)
        {
            if (pointEvent == null)
            {
                continue;
            }


            VisualElement marker =
                CreatePointEventMarker(
                    pointEvent);


            _pointEventMarkers.Add(
                marker);


            _rightElement.Add(
                marker);
        }


        LayoutPointEventMarkers();
    }


    private VisualElement
        CreatePointEventMarker(
            PointEventData pointEvent)
    {
        VisualElement marker =
            new VisualElement();

        marker.AddToClassList(
            PointEventMarkerClassName);


        // 标记形状：长方形 + 三角形（书签/小旗子样式）
        // 三角形尖端对齐事件时间点
        marker.style.width =
            MarkerWidth;

        marker.style.height =
            MarkerHeight;

        marker.style.position =
            Position.Absolute;

        marker.tooltip =
            string.IsNullOrEmpty(
                pointEvent.Name)
                ? pointEvent.EventType
                    .ToString()
                : pointEvent.Name;


        // 用 generateVisualContent 绘制形状
        marker.generateVisualContent +=
            ctx =>
            {
                DrawPointEventMarker(
                    ctx,
                    pointEvent);
            };


        // 左键：选中 + 拖拽移动
        // 右键：选中 + 菜单
        marker.RegisterCallback<
            PointerDownEvent>(
            evt =>
            {
                if (evt.button == 0)
                {
                    _controller
                        .SelectPointEvent(
                            pointEvent);

                    _draggingPointEvent =
                        pointEvent;

                    _draggingPointerId =
                        evt.pointerId;

                    _lastDragPointerX =
                        evt.position.x;

                    marker.CapturePointer(
                        evt.pointerId);

                    evt.StopPropagation();
                }
                else if (evt.button == 1)
                {
                    _controller
                        .SelectPointEvent(
                            pointEvent);

                    ShowPointEventContextMenu(
                        pointEvent);

                    evt.StopPropagation();
                }
            });


        // 拖拽移动：按像素增量换算时间
        marker.RegisterCallback<
            PointerMoveEvent>(
            evt =>
            {
                if (_draggingPointEvent !=
                        pointEvent ||
                    _draggingPointerId !=
                        evt.pointerId)
                {
                    return;
                }

                if (!marker.HasPointerCapture(
                        evt.pointerId))
                {
                    return;
                }


                float pixelsPerSecond =
                    _fps *
                    _frameWidth;

                if (pixelsPerSecond <=
                    0f)
                {
                    return;
                }


                float deltaPixel =
                    evt.position.x -
                    _lastDragPointerX;

                _lastDragPointerX =
                    evt.position.x;

                float newTime =
                    pointEvent.Time +
                    deltaPixel /
                        pixelsPerSecond;

                _controller.SetPointEventTime(
                    pointEvent,
                    newTime);

                evt.StopPropagation();
            });


        // 松开：结束拖拽
        marker.RegisterCallback<
            PointerUpEvent>(
            evt =>
            {
                if (_draggingPointEvent !=
                        pointEvent ||
                    _draggingPointerId !=
                        evt.pointerId)
                {
                    return;
                }

                if (marker.HasPointerCapture(
                        evt.pointerId))
                {
                    marker.ReleasePointer(
                        evt.pointerId);
                }

                _draggingPointEvent =
                    null;

                _draggingPointerId =
                    -1;

                evt.StopPropagation();
            });


        return marker;
    }


    // =========================================================
    // Draw Point Event Marker
    //
    // 形状：上方长方形 + 下方三角形
    // 三角形尖端（底部中心）对齐事件时间点
    // 先画外轮廓（边框色），再画内填充（小一圈）
    // =========================================================

    private void DrawPointEventMarker(
        MeshGenerationContext ctx,
        PointEventData pointEvent)
    {
        bool selected =
            _controller != null &&
            _controller.SelectedPointEvent ==
                pointEvent;

        // 填充始终白色；未选中灰色描边，选中黄色描边
        Color borderColor =
            selected
                ? new Color(
                    1f,
                    0.85f,
                    0.1f)
                : new Color(
                    0.45f,
                    0.45f,
                    0.45f);

        Color fillColor =
            new Color(
                1f,
                1f,
                1f);


        Vertex[] vertices =
            new Vertex[10];


        // 外轮廓（边框色）扇形
        SetMarkerVertex(
            ref vertices[0],
            0f,
            0f,
            borderColor);

        SetMarkerVertex(
            ref vertices[1],
            8f,
            0f,
            borderColor);

        SetMarkerVertex(
            ref vertices[2],
            8f,
            7f,
            borderColor);

        SetMarkerVertex(
            ref vertices[3],
            4f,
            13f,
            borderColor);

        SetMarkerVertex(
            ref vertices[4],
            0f,
            7f,
            borderColor);


        // 内部填充（小一圈）扇形
        SetMarkerVertex(
            ref vertices[5],
            1f,
            1f,
            fillColor);

        SetMarkerVertex(
            ref vertices[6],
            7f,
            1f,
            fillColor);

        SetMarkerVertex(
            ref vertices[7],
            7f,
            7f,
            fillColor);

        SetMarkerVertex(
            ref vertices[8],
            4f,
            12f,
            fillColor);

        SetMarkerVertex(
            ref vertices[9],
            1f,
            7f,
            fillColor);


        MeshWriteData mesh =
            ctx.Allocate(
                10,
                18);

        mesh.SetAllVertices(
            vertices);

        mesh.SetAllIndices(
            new ushort[]
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,

                5, 6, 7,
                5, 7, 8,
                5, 8, 9,
            });
    }


    private static void SetMarkerVertex(
        ref Vertex vertex,
        float x,
        float y,
        Color color)
    {
        vertex.position =
            new Vector3(
                x,
                y,
                0f);

        vertex.tint =
            color;

        vertex.uv =
            Vector2.zero;
    }


    private void LayoutPointEventMarkers()
    {
        if (_trackData == null ||
            _trackData.PointEvents == null)
        {
            return;
        }


        float pixelsPerSecond =
            _fps *
            _frameWidth;


        for (int i = 0;
             i < _trackData.PointEvents.Count;
             i++)
        {
            if (i >= _pointEventMarkers.Count)
            {
                break;
            }


            PointEventData pointEvent =
                _trackData.PointEvents[i];

            VisualElement marker =
                _pointEventMarkers[i];


            float x =
                pointEvent.Time *
                pixelsPerSecond;


            // 三角形尖端（marker 局部 x = 4）对齐时间点
            marker.style.left =
                x - MarkerTipOffsetX;

            // 位置靠近轨道底边
            marker.style.top =
                TrackHeight -
                MarkerHeight -
                MarkerBottomMargin;
        }
    }


    // =========================================================
    // Point Event 选中变化：刷新标记高亮
    // =========================================================

    private void OnPointEventSelectionChanged(
        PointEventData pointEvent)
    {
        foreach (
            VisualElement marker
            in _pointEventMarkers)
        {
            marker.MarkDirtyRepaint();
        }
    }


    // =========================================================
    // Point Event 数据变化：重新布局 + 刷新高亮
    // =========================================================

    private void OnPointEventChanged(
        PointEventData pointEvent)
    {
        LayoutPointEventMarkers();

        foreach (
            VisualElement marker
            in _pointEventMarkers)
        {
            marker.MarkDirtyRepaint();
        }
    }

    private void ShowPointEventContextMenu(
        PointEventData pointEvent)
    {
        GenericMenu menu =
            new GenericMenu();


        menu.AddItem(
            new GUIContent(
                "Delete"),
            false,
            () =>
            {
                _controller.DeletePointEvent(
                    _trackData,
                    pointEvent);
            });


        menu.ShowAsContext();
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
        // ????????????? Clip
        // ClipView ???????
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


        // -----------------------------------------------------
        // Effect Track
        // -----------------------------------------------------

        else if (_trackData.ClipType ==
                 ClipType.Effect)
        {
            menu.AddItem(
                new GUIContent(
                    "Add/Effect Clip"),
                false,
                () =>
                {
                    OpenEffectPicker(
                        time);
                });
        }


        // -----------------------------------------------------
        // Hitbox Track
        // -----------------------------------------------------

        else if (_trackData.ClipType ==
                 ClipType.Hitbox)
        {
            menu.AddItem(
                new GUIContent(
                    "Add/Hitbox Clip"),
                false,
                () =>
                {
                    OpenHitboxPicker(
                        time);
                });
        }


        // -----------------------------------------------------
        // Behitbox Track
        // -----------------------------------------------------

        else if (_trackData.ClipType ==
                 ClipType.Behitbox)
        {
            menu.AddItem(
                new GUIContent(
                    "Add/Behitbox Clip"),
                false,
                () =>
                {
                    OpenHitboxPicker(
                        time);
                });
        }


        // -----------------------------------------------------
        // State Event Track
        // -----------------------------------------------------

        else if (_trackData.ClipType ==
                 ClipType.StateEvent)
        {
            EventType[] stateEventTypes =
                EventFactory.GetStateEventTypes();

            foreach (
                EventType seType
                in stateEventTypes)
            {
                menu.AddItem(
                    new GUIContent(
                        "Add/State Event Clip/" +
                        seType),
                    false,
                    () =>
                    {
                        _controller.AddStateEventClip(
                            _trackData,
                            seType,
                            time);
                    });
            }
        }


        // -----------------------------------------------------
        // Add Point Event（所有轨道都可以加点事件）
        // -----------------------------------------------------

        EventType[] pointEventTypes =
            EventFactory.GetPointEventTypes();

        foreach (
            EventType peType
            in pointEventTypes)
        {
            menu.AddItem(
                new GUIContent(
                    "Add Point Event/" +
                    peType),
                false,
                () =>
                {
                    _controller.AddPointEvent(
                        _trackData,
                        peType,
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
    // Effect Picker
    // =========================================================

    private void OpenEffectPicker(
        float time)
    {
        _pendingAddTime =
            time;

        _effectPickerControlID =
            GUIUtility.GetControlID(
                FocusType.Passive);

        _waitingForEffectPicker =
            true;

        EditorGUIUtility.ShowObjectPicker<
            GameObject>(
            null,
            false,
            "",
            _effectPickerControlID);

        EditorApplication.update +=
            CheckEffectPicker;
    }


    private void CheckEffectPicker()
    {
        if (!_waitingForEffectPicker)
        {
            EditorApplication.update -=
                CheckEffectPicker;

            return;
        }

        int pickerID =
            EditorGUIUtility
                .GetObjectPickerControlID();

        if (pickerID !=
            _effectPickerControlID)
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

        GameObject effectPrefab =
            selectedObject
                as GameObject;

        if (effectPrefab != null)
        {
            _controller.AddEffectClip(
                _trackData,
                effectPrefab,
                _pendingAddTime);
        }

        _waitingForEffectPicker =
            false;

        EditorApplication.update -=
            CheckEffectPicker;
    }


    // =========================================================
    // Hitbox Picker
    // =========================================================

    private void OpenHitboxPicker(
        float time)
    {
        _pendingAddTime =
            time;

        _hitboxPickerControlID =
            GUIUtility.GetControlID(
                FocusType.Passive);

        _waitingForHitboxPicker =
            true;

        EditorGUIUtility.ShowObjectPicker<
            GameObject>(
            null,
            false,
            "",
            _hitboxPickerControlID);

        EditorApplication.update +=
            CheckHitboxPicker;
    }


    private void CheckHitboxPicker()
    {
        if (!_waitingForHitboxPicker)
        {
            EditorApplication.update -=
                CheckHitboxPicker;

            return;
        }

        int pickerID =
            EditorGUIUtility
                .GetObjectPickerControlID();

        if (pickerID !=
            _hitboxPickerControlID)
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

        GameObject hitboxPrefab =
            selectedObject
                as GameObject;

        if (hitboxPrefab != null &&
            _trackData != null)
        {
            if (_trackData.ClipType ==
                ClipType.Hitbox)
            {
                _controller.AddHitboxClip(
                    _trackData,
                    hitboxPrefab,
                    _pendingAddTime);
            }
            else if (_trackData.ClipType ==
                     ClipType.Behitbox)
            {
                _controller.AddBehitboxClip(
                    _trackData,
                    hitboxPrefab,
                    _pendingAddTime);
            }
        }

        _waitingForHitboxPicker =
            false;

        EditorApplication.update -=
            CheckHitboxPicker;
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

        LayoutPointEventMarkers();
    }


    // =========================================================
    // Rebuild Point Event Markers
    //
    // 结构变化时由外部调用
    // =========================================================

    public void RebuildPointEventMarkers()
    {
        BuildPointEventMarkers();
    }

}
