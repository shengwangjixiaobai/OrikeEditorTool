using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ObjectField = UnityEditor.UIElements.ObjectField;

public class TimeLineWindow : EditorWindow
{
    // =========================================================
    // Layout
    // =========================================================

    private const float InitialLeftPanelWidth = 240f;
    private const float MinLeftPanelWidth = 240f;
    private const float MaxLeftPanelWidth = 350f;

    private const float InspectorWidth = 280f;

    private const float SplitterWidth = 3f;
    private const float SplitterHitWidth = 10f;

    private const float HeaderRowHeight = 28f;
    private const float ToolbarHeight = 30f;

    private const float TopAreaHeight =
        HeaderRowHeight * 3f +
        ToolbarHeight;

    private const float RulerHeight = 32f;

    private const float TrackTopGap = 8f;


    // =========================================================
    // Timeline
    // =========================================================

    private const float InitialFrameWidth = 10f;

    private const int FramesPerTick = 20;

    private const int TimelineTickCount = 300;

    private const int TimelineFrameCount =
        TimelineTickCount *
        FramesPerTick;


    // =========================================================
    // Zoom
    // =========================================================

    private const float MinZoom = 0.5f;

    private const float MaxZoom = 4.0f;

    private const float ZoomFactor = 1.15f;

    private float _zoom = 1f;

    private float PixelPerFrame =>
        (InitialFrameWidth /
         FramesPerTick) *
        _zoom;

    private float CurrentFrameWidth =>
        InitialFrameWidth *
        _zoom;


    // =========================================================
    // Playhead
    // =========================================================

    private const float PlayheadWidth = 1f;

    private const float PlayheadHitWidth = 12f;

    private const float AutoScrollThreshold = 30f;

    private const float AutoScrollSpeed = 8f;


    // =========================================================
    // State
    // =========================================================

    private float _leftPanelWidth =
        InitialLeftPanelWidth;

    private int _currentFrame;


    public int CurrentFrame
    {
        get => _currentFrame;

        private set
        {
            int newFrame =
                Mathf.Clamp(
                    value,
                    0,
                    TimelineFrameCount);

            if (_currentFrame ==
                newFrame)
            {
                return;
            }

            _currentFrame =
                newFrame;

            UpdateCurrentFrameUI();

            PreviewCurrentFrame();
        }
    }


    // =========================================================
    // Playback
    // =========================================================

    private bool _isPlaying;

    private double _lastEditorTime;

    private float _playTime;


    // =========================================================
    // Preview
    // =========================================================

    private ActionPreviewSystem
        _previewSystem;


    // =========================================================
    // Root
    // =========================================================

    private VisualElement _root;


    // =========================================================
    // Left
    // =========================================================

    private VisualElement _leftPanel;

    private VisualElement _leftHeader;

    private VisualElement _trackNameArea;

    private EnumField _frameRateField;

    private ObjectField _characterField;

    private ObjectField _actionViewDataField;

    private Button _backward10Button;

    private Button _backward5Button;

    private Button _playButton;

    private Button _forward5Button;

    private Button _forward10Button;


    // =========================================================
    // Splitter
    // =========================================================

    private VisualElement _splitter;

    private VisualElement _splitterHitArea;

    private bool _isDraggingSplitter;

    private int _splitterPointerId;


    // =========================================================
    // Timeline
    // =========================================================

    private VisualElement _rightPanel;

    private VisualElement _timelineTop;

    private VisualElement _timelineBottom;

    private ScrollView _trackScrollView;

    private VisualElement _trackContent;

    private VisualElement _timelineTrackArea;

    private VisualElement _timelineRuler;

    private VisualElement _rulerContent;

    private ActionView _actionView;

    private TimeLineController _timeLineController;


    // =========================================================
    // Inspector
    // =========================================================

    private InspectorView _inspectorView;


    // =========================================================
    // Playhead
    // =========================================================

    private VisualElement _playheadHitArea;

    private VisualElement _centerLine;

    private Label _currentFrameLabel;

    private bool _isDraggingPlayhead;


    // =========================================================
    // FPS
    // =========================================================

    private enum FrameRate
    {
        FPS30 = 30,
        FPS60 = 60,
        FPS120 = 120
    }


    private int CurrentFrameRate
    {
        get
        {
            if (_frameRateField == null)
            {
                return 60;
            }

            return (int)(FrameRate)
                _frameRateField.value;
        }
    }


    private float CurrentTime =>
        (float)_currentFrame /
        CurrentFrameRate;


    // =========================================================
    // Open
    // =========================================================

    [MenuItem("Orike/Action Timeline")]
    public static void Open()
    {
        TimeLineWindow window =
            GetWindow<TimeLineWindow>();

        window.titleContent =
            new GUIContent(
                "Timeline");

        window.minSize =
            new Vector2(
                1080,
                400);
    }


    private void OnEnable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }


    // =========================================================
    // Create GUI
    // =========================================================

    private void CreateGUI()
    {
        BuildUI();

        StyleSheet styleSheet =
            AssetDatabase.LoadAssetAtPath<
                StyleSheet>(
                    "Assets/GameLogic/OrikeScript/OrikeComboEditor/ActionEditor--TimeLine/Editor/TimeLineWindow.uss");

        if (styleSheet != null)
        {
            _root.styleSheets.Add(
                styleSheet);
        }
    }


    private void OnDisable()
    {
        StopPlayback();

        EditorApplication.update -= OnEditorUpdate;

        if (_previewSystem != null)
        {
            _previewSystem.StopPreview();
        }

        if (_timeLineController != null)
        {
            _timeLineController.ClearSelection();
        }
    }


    // =========================================================
    // Build UI
    // =========================================================

    private void BuildUI()
    {
        _root =
            rootVisualElement;

        _root.Clear();

        ConfigureRoot();

        CreateLeftPanel();

        CreateSplitter();

        CreateRightPanel();

        CreateInspectorPanel();

        RegisterKeyboardEvents();

        UpdateCurrentFrameUI();
    }


    private void ConfigureRoot()
    {
        _root.style.flexDirection =
            FlexDirection.Row;

        _root.style.flexGrow =
            1;

        _root.style.backgroundColor =
            new Color(
                0.12f,
                0.12f,
                0.12f);
    }


    // =========================================================
    // Keyboard
    // =========================================================

    private void RegisterKeyboardEvents()
    {
        if (_root == null)
        {
            return;
        }

        // Root 可以获得键盘焦点
        _root.focusable = true;
        _root.tabIndex = 0;

        // 使用 TrickleDown，保证即使当前焦点在
        // Timeline 内部的其他 VisualElement 上，
        // 快捷键事件也能传到 Root。
        _root.RegisterCallback<KeyDownEvent>(
            OnRootKeyDown,
            TrickleDown.TrickleDown);

        // 点击 Timeline 任意区域后，把焦点交给 Root
        _root.RegisterCallback<PointerDownEvent>(
            OnRootPointerDown,
            TrickleDown.TrickleDown);
    }

    private void OnRootPointerDown(
        PointerDownEvent evt)
    {
        if (_root == null)
        {
            return;
        }

        // 如果点击的是文本输入框、ObjectField 等控件，
        // 不强制抢走它们的焦点。
        if (evt.target is TextField ||
            evt.target is FloatField ||
            evt.target is IntegerField ||
            evt.target is ObjectField)
        {
            return;
        }

        _root.Focus();
    }

    private void OnRootKeyDown(
        KeyDownEvent evt)
    {
        if (_timeLineController == null)
        {
            return;
        }

        bool modifier =
            evt.ctrlKey ||
            evt.commandKey;

        // =========================================================
        // Ctrl + C
        // =========================================================

        if (modifier &&
            evt.keyCode == KeyCode.C)
        {
            if (_timeLineController.SelectedClips.Count > 0)
            {
                _timeLineController.CopySelectedClips();

                evt.StopPropagation();
                evt.PreventDefault();
            }

            return;
        }

        // =========================================================
        // Ctrl + V
        // =========================================================

        if (modifier &&
            evt.keyCode == KeyCode.V)
        {
            if (_timeLineController.SelectedClip != null &&
                _timeLineController.HasClipboard)
            {
                PasteKeyboard();

                evt.StopPropagation();
                evt.PreventDefault();
            }

            return;
        }

        // =========================================================
        // Delete
        // =========================================================

        if (evt.keyCode == KeyCode.Delete)
        {
            if (_timeLineController.SelectedClips.Count > 0)
            {
                _timeLineController.DeleteSelectedClips();

                evt.StopPropagation();
                evt.PreventDefault();
            }

            return;
        }
    }


    private void PasteKeyboard()
    {
        if (_timeLineController == null)
        {
            return;
        }

        BaseClipData selectedClip =
            _timeLineController.SelectedClip;

        if (selectedClip == null)
        {
            return;
        }

        TrackData track =
            _timeLineController.GetTrackForClip(
                selectedClip);

        if (track == null)
        {
            return;
        }

        if (!_timeLineController.HasClipboard)
        {
            return;
        }

        // 粘贴到当前 Clip 后面
        float pasteTime =
            selectedClip.EndTime;

        _timeLineController.PasteClips(
            track,
            pasteTime);
    }


    // =========================================================
    // Left Panel
    // =========================================================

    private void CreateLeftPanel()
    {
        _leftPanel =
            new VisualElement();

        ApplyLeftPanelWidth();

        _leftPanel.style.flexShrink =
            0;

        _leftPanel.style.flexDirection =
            FlexDirection.Column;

        _leftPanel.style.backgroundColor =
            new Color(
                0.16f,
                0.16f,
                0.16f);

        _root.Add(
            _leftPanel);

        CreateLeftHeader();

        CreateTrackNameArea();
    }


    private void ApplyLeftPanelWidth()
    {
        _leftPanel.style.width =
            _leftPanelWidth;

        _leftPanel.style.minWidth =
            _leftPanelWidth;

        _leftPanel.style.maxWidth =
            _leftPanelWidth;
    }


    // =========================================================
    // Left Header
    // =========================================================

    private void CreateLeftHeader()
    {
        _leftHeader =
            new VisualElement();

        _leftHeader.style.height =
            TopAreaHeight;

        _leftHeader.style.flexShrink =
            0;

        _leftHeader.style.flexDirection =
            FlexDirection.Column;

        _leftHeader.style.paddingLeft =
            4;

        _leftHeader.style.paddingRight =
            4;

        _leftHeader.style.paddingTop =
            4;

        _leftHeader.style.paddingBottom =
            4;

        _leftPanel.Add(
            _leftHeader);


        // FPS
        VisualElement fpsRow =
            CreateHeaderRow();

        _leftHeader.Add(
            fpsRow);

        Label fpsLabel =
            new Label("FPS");

        fpsLabel.style.width =
            45;

        fpsLabel.style.unityTextAlign =
            TextAnchor.MiddleLeft;

        fpsLabel.style.fontSize =
            11;

        fpsRow.Add(
            fpsLabel);

        _frameRateField =
            new EnumField(
                FrameRate.FPS60);

        _frameRateField.style.flexGrow =
            1;

        _frameRateField.style.height =
            22;

        _frameRateField.RegisterValueChangedCallback(
            evt =>
            {
                RebuildActionView();

                PreviewCurrentFrame();
            });

        fpsRow.Add(
            _frameRateField);


        // Character
        VisualElement characterRow =
            CreateHeaderRow();

        _leftHeader.Add(
            characterRow);

        Label characterLabel =
            new Label("Character");

        characterLabel.style.width =
            65;

        characterLabel.style.unityTextAlign =
            TextAnchor.MiddleLeft;

        characterLabel.style.fontSize =
            11;

        characterRow.Add(
            characterLabel);

        _characterField =
            new ObjectField();

        _characterField.objectType =
            typeof(GameObject);

        _characterField.allowSceneObjects =
            true;

        _characterField.style.flexGrow =
            1;

        _characterField.style.height =
            22;

        _characterField.RegisterValueChangedCallback(
            evt =>
            {
                GameObject character =
                    evt.newValue as GameObject;

                UpdatePreviewCharacter(
                    character);

                PreviewCurrentFrame();
            });

        characterRow.Add(
            _characterField);


        // ActionData
        VisualElement actionRow =
            CreateHeaderRow();

        _leftHeader.Add(
            actionRow);

        Label actionLabel =
            new Label("ActionData");

        actionLabel.style.width =
            65;

        actionLabel.style.unityTextAlign =
            TextAnchor.MiddleLeft;

        actionLabel.style.fontSize =
            11;

        actionRow.Add(
            actionLabel);

        _actionViewDataField =
            new ObjectField();

        _actionViewDataField.objectType =
            typeof(ActionData);

        _actionViewDataField.allowSceneObjects =
            false;

        _actionViewDataField.style.flexGrow =
            1;

        _actionViewDataField.style.height =
            22;

        _actionViewDataField.RegisterValueChangedCallback(
            evt =>
            {
                ActionData actionData =
                    evt.newValue as ActionData;

                if (actionData != null)
                {
                    actionData.RefreshData();

                    EditorUtility.SetDirty(
                        actionData);
                }

                UpdatePreviewActionData(
                    actionData);

                RebuildActionView();

                PreviewCurrentFrame();
            });

        actionRow.Add(
            _actionViewDataField);


        // Playback
        VisualElement playbackRow =
            new VisualElement();

        playbackRow.style.height =
            ToolbarHeight;

        playbackRow.style.flexShrink =
            0;

        playbackRow.style.flexDirection =
            FlexDirection.Row;

        playbackRow.style.alignItems =
            Align.Center;

        playbackRow.style.justifyContent =
            Justify.Center;

        _leftHeader.Add(
            playbackRow);


        _backward10Button =
            CreatePlaybackButton(
                "|<<");

        _backward10Button.tooltip =
            "Backward 20 Frames";

        _backward10Button.clicked +=
            () =>
            {
                CurrentFrame -= 20;
            };

        playbackRow.Add(
            _backward10Button);


        _backward5Button =
            CreatePlaybackButton(
                "|<");

        _backward5Button.tooltip =
            "Backward 1 Frame";

        _backward5Button.clicked +=
            () =>
            {
                CurrentFrame -= 1;
            };

        playbackRow.Add(
            _backward5Button);


        _playButton =
            CreatePlaybackButton(
                "Play");

        _playButton.tooltip =
            "Play / Pause";

        _playButton.clicked +=
            TogglePlay;

        playbackRow.Add(
            _playButton);


        _forward5Button =
            CreatePlaybackButton(
                ">|");

        _forward5Button.tooltip =
            "Forward 1 Frame";

        _forward5Button.clicked +=
            () =>
            {
                CurrentFrame += 1;
            };

        playbackRow.Add(
            _forward5Button);


        _forward10Button =
            CreatePlaybackButton(
                ">>|");

        _forward10Button.tooltip =
            "Forward 20 Frames";

        _forward10Button.clicked +=
            () =>
            {
                CurrentFrame += 20;
            };

        playbackRow.Add(
            _forward10Button);
    }


    private VisualElement CreateHeaderRow()
    {
        VisualElement row =
            new VisualElement();

        row.style.height =
            HeaderRowHeight;

        row.style.flexShrink =
            0;

        row.style.flexDirection =
            FlexDirection.Row;

        row.style.alignItems =
            Align.Center;

        return row;
    }


    private Button CreatePlaybackButton(
        string text)
    {
        Button button =
            new Button();

        button.text =
            text;

        button.style.width =
            42;

        button.style.height =
            26;

        button.style.marginLeft =
            1;

        button.style.marginRight =
            1;

        return button;
    }


    // =========================================================
    // Track Name
    // =========================================================

    private void CreateTrackNameArea()
    {
        _trackNameArea =
            new VisualElement();

        _trackNameArea.style.flexGrow =
            1;

        _trackNameArea.style.flexShrink =
            1;

        _trackNameArea.style.overflow =
            Overflow.Hidden;

        _trackNameArea.style.paddingTop =
            TrackTopGap;

        _leftPanel.Add(
            _trackNameArea);
    }


    // =========================================================
    // Splitter
    // =========================================================

    private void CreateSplitter()
    {
        _splitter =
            new VisualElement();

        _splitter.style.width =
            SplitterWidth;

        _splitter.style.minWidth =
            SplitterWidth;

        _splitter.style.maxWidth =
            SplitterWidth;

        _splitter.style.flexShrink =
            0;

        _splitter.style.backgroundColor =
            new Color(
                0.055f,
                0.055f,
                0.055f);

        _splitterHitArea =
            new VisualElement();

        _splitterHitArea.style.position =
            Position.Absolute;

        _splitterHitArea.style.left =
            -(SplitterHitWidth -
              SplitterWidth) / 2f;

        _splitterHitArea.style.top =
            0;

        _splitterHitArea.style.bottom =
            0;

        _splitterHitArea.style.width =
            SplitterHitWidth;

        _splitterHitArea.pickingMode =
            PickingMode.Position;

        _splitter.Add(
            _splitterHitArea);

        _splitter.AddToClassList(
            "timeline-splitter");

        _splitterHitArea.AddToClassList(
            "timeline-splitter-hit-area");

        _splitterHitArea.RegisterCallback<
            PointerDownEvent>(
            OnSplitterPointerDown);

        _splitterHitArea.RegisterCallback<
            PointerMoveEvent>(
            OnSplitterPointerMove);

        _splitterHitArea.RegisterCallback<
            PointerUpEvent>(
            OnSplitterPointerUp);

        _root.Add(
            _splitter);
    }


    private void OnSplitterPointerDown(
        PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        _isDraggingSplitter =
            true;

        _splitterPointerId =
            evt.pointerId;

        _splitterHitArea.CapturePointer(
            evt.pointerId);

        evt.StopPropagation();
    }


    private void OnSplitterPointerMove(
        PointerMoveEvent evt)
    {
        if (!_isDraggingSplitter)
        {
            return;
        }

        if (!_splitterHitArea
                .HasPointerCapture(
                    _splitterPointerId))
        {
            return;
        }

        Vector2 rootPosition =
            _root.WorldToLocal(
                evt.position);

        float newWidth =
            rootPosition.x;

        _leftPanelWidth =
            Mathf.Clamp(
                newWidth,
                MinLeftPanelWidth,
                MaxLeftPanelWidth);

        ApplyLeftPanelWidth();

        evt.StopPropagation();
    }


    private void OnSplitterPointerUp(
        PointerUpEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        if (_splitterHitArea
            .HasPointerCapture(
                _splitterPointerId))
        {
            _splitterHitArea.ReleasePointer(
                _splitterPointerId);
        }

        _isDraggingSplitter =
            false;

        evt.StopPropagation();
    }


    // =========================================================
    // Right Timeline Panel
    // =========================================================

    private void CreateRightPanel()
    {
        _rightPanel =
            new VisualElement();

        _rightPanel.style.flexGrow =
            1;

        _rightPanel.style.flexShrink =
            1;

        _rightPanel.style.flexDirection =
            FlexDirection.Column;

        _rightPanel.style.overflow =
            Overflow.Hidden;

        _root.Add(
            _rightPanel);


        _timelineTop =
            new VisualElement();

        _timelineTop.style.height =
            TopAreaHeight;

        _timelineTop.style.flexShrink =
            0;

        _timelineTop.style.overflow =
            Overflow.Hidden;

        _rightPanel.Add(
            _timelineTop);


        _timelineBottom =
            new VisualElement();

        _timelineBottom.style.flexGrow =
            1;

        _timelineBottom.style.flexShrink =
            1;

        _timelineBottom.style.position =
            Position.Relative;

        _timelineBottom.style.overflow =
            Overflow.Hidden;

        _rightPanel.Add(
            _timelineBottom);

        CreateRuler();

        CreateTrackArea();

        CreatePlayhead();

        CreateCurrentFrameLabel();
    }


    // =========================================================
    // Inspector
    // =========================================================

    private void CreateInspectorPanel()
    {
        _inspectorView =
            new InspectorView(
                _timeLineController);

        _inspectorView.style.width =
            InspectorWidth;

        _inspectorView.style.minWidth =
            240;

        _inspectorView.style.maxWidth =
            360;

        _inspectorView.style.flexShrink =
            0;

        _root.Add(
            _inspectorView);
    }


    // =========================================================
    // Ruler
    // =========================================================

    private void CreateRuler()
    {
        _timelineRuler =
            new VisualElement();

        _timelineRuler.style.height =
            TopAreaHeight;

        _timelineRuler.style.flexShrink =
            0;

        _timelineRuler.style.position =
            Position.Relative;

        _timelineRuler.style.overflow =
            Overflow.Hidden;

        _timelineRuler.style.backgroundColor =
            new Color(
                0.14f,
                0.14f,
                0.14f);

        _timelineTop.Add(
            _timelineRuler);


        _rulerContent =
            new VisualElement();

        _rulerContent.style.position =
            Position.Absolute;

        _rulerContent.style.top =
            0;

        _rulerContent.style.left =
            0;

        _rulerContent.style.width =
            TimelineTickCount *
            CurrentFrameWidth;

        _rulerContent.style.height =
            RulerHeight;

        _rulerContent.style.overflow =
            Overflow.Hidden;

        _rulerContent.style.backgroundColor =
            new Color(
                0.14f,
                0.14f,
                0.14f);

        _timelineRuler.Add(
            _rulerContent);

        DrawFrameRuler();


        VisualElement alignmentArea =
            new VisualElement();

        alignmentArea.style.position =
            Position.Absolute;

        alignmentArea.style.top =
            RulerHeight;

        alignmentArea.style.bottom =
            0;

        alignmentArea.style.left =
            0;

        alignmentArea.style.right =
            0;

        alignmentArea.style.backgroundColor =
            new Color(
                0.18f,
                0.18f,
                0.18f);

        alignmentArea.pickingMode =
            PickingMode.Ignore;

        _timelineRuler.Add(
            alignmentArea);

        _rulerContent.BringToFront();

        _timelineRuler.RegisterCallback<
            PointerDownEvent>(
            OnRulerPointerDown);

        _timelineRuler.RegisterCallback<
            WheelEvent>(
            OnRulerWheel);
    }


    private void DrawFrameRuler()
    {
        _rulerContent.Clear();

        float frameWidth =
            CurrentFrameWidth;

        for (int tickIndex = 0;
             tickIndex <= TimelineTickCount;
             tickIndex++)
        {
            float x =
                tickIndex *
                frameWidth;

            bool isMajor =
                tickIndex % 5 == 0;

            int frame =
                tickIndex *
                FramesPerTick;

            VisualElement tick =
                new VisualElement();

            tick.style.position =
                Position.Absolute;

            tick.style.left =
                x;

            tick.style.bottom =
                0;

            tick.style.width =
                1;

            tick.style.height =
                isMajor
                    ? 14
                    : 7;

            tick.style.backgroundColor =
                isMajor
                    ? new Color(
                        0.70f,
                        0.70f,
                        0.70f)
                    : new Color(
                        0.35f,
                        0.35f,
                        0.35f);

            tick.pickingMode =
                PickingMode.Ignore;

            _rulerContent.Add(
                tick);

            if (isMajor)
            {
                Label label =
                    new Label(
                        frame.ToString());

                label.style.position =
                    Position.Absolute;

                label.style.left =
                    x + 3;

                label.style.top =
                    3;

                label.style.fontSize =
                    10;

                label.style.color =
                    new Color(
                        0.75f,
                        0.75f,
                        0.75f);

                label.pickingMode =
                    PickingMode.Ignore;

                _rulerContent.Add(
                    label);
            }
        }
    }


    // =========================================================
    // Ruler Zoom
    // =========================================================

    private void OnRulerWheel(
        WheelEvent evt)
    {
        if (_timelineRuler == null ||
            _trackScrollView == null)
        {
            return;
        }

        float wheel =
            evt.delta.y;

        if (Mathf.Approximately(
                wheel,
                0f))
        {
            return;
        }

        Vector2 mouseLocal =
            _timelineRuler.WorldToLocal(
                evt.mousePosition);

        float mouseX =
            mouseLocal.x;

        float oldScroll =
            _trackScrollView
                .scrollOffset.x;

        float oldContentX =
            oldScroll +
            mouseX;

        float frameAtMouse =
            oldContentX /
            PixelPerFrame;

        float newZoom;

        if (wheel < 0f)
        {
            newZoom =
                _zoom *
                ZoomFactor;
        }
        else
        {
            newZoom =
                _zoom /
                ZoomFactor;
        }

        newZoom =
            Mathf.Clamp(
                newZoom,
                MinZoom,
                MaxZoom);

        if (Mathf.Approximately(
                newZoom,
                _zoom))
        {
            return;
        }

        _zoom =
            newZoom;

        float newContentWidth =
            TimelineTickCount *
            CurrentFrameWidth;

        _rulerContent.style.width =
            newContentWidth;

        _trackContent.style.width =
            newContentWidth;

        _trackContent.style.minWidth =
            newContentWidth;

        _timelineTrackArea.style.width =
            newContentWidth;

        _timelineTrackArea.style.minWidth =
            newContentWidth;

        DrawFrameRuler();

        RebuildActionView();

        float newContentX =
            frameAtMouse *
            PixelPerFrame;

        float newScroll =
            newContentX -
            mouseX;

        _timelineRuler.schedule.Execute(
            () =>
            {
                if (_trackScrollView == null)
                {
                    return;
                }

                float viewportWidth =
                    _timelineBottom
                        .resolvedStyle
                        .width;

                float maxScroll =
                    Mathf.Max(
                        0f,
                        newContentWidth -
                        viewportWidth);

                newScroll =
                    Mathf.Clamp(
                        newScroll,
                        0f,
                        maxScroll);

                Vector2 offset =
                    _trackScrollView
                        .scrollOffset;

                offset.x =
                    newScroll;

                _trackScrollView.scrollOffset =
                    offset;

                UpdateRulerPosition(
                    newScroll);

                UpdateCurrentFrameUI();
            });

        evt.StopPropagation();
    }


    // =========================================================
    // Ruler Click
    // =========================================================

    private void OnRulerPointerDown(
        PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        Vector2 localPosition =
            _timelineRuler.WorldToLocal(
                evt.position);

        float scrollOffset =
            _trackScrollView != null
                ? _trackScrollView
                    .scrollOffset.x
                : 0f;

        float contentX =
            localPosition.x +
            scrollOffset;

        int frame =
            Mathf.RoundToInt(
                contentX /
                PixelPerFrame);

        CurrentFrame =
            frame;

        evt.StopPropagation();
    }


    // =========================================================
    // Track Area
    // =========================================================

    private void CreateTrackArea()
    {
        _trackScrollView =
            new ScrollView(
                ScrollViewMode.Horizontal);

        _trackScrollView.style.flexGrow =
            1;

        _trackScrollView.style.flexShrink =
            1;

        _trackScrollView.style.marginTop =
            TrackTopGap;

        _trackScrollView.horizontalScrollerVisibility =
            ScrollerVisibility.AlwaysVisible;

        _trackScrollView.verticalScrollerVisibility =
            ScrollerVisibility.Hidden;

        _timelineBottom.Add(
            _trackScrollView);


        _trackContent =
            new VisualElement();

        float contentWidth =
            TimelineTickCount *
            CurrentFrameWidth;

        _trackContent.style.width =
            contentWidth;

        _trackContent.style.minWidth =
            contentWidth;

        _trackContent.style.flexShrink =
            0;

        _trackContent.style.position =
            Position.Relative;

        _trackScrollView.Add(
            _trackContent);


        _timelineTrackArea =
            new VisualElement();

        _timelineTrackArea.style.width =
            contentWidth;

        _timelineTrackArea.style.minWidth =
            contentWidth;

        _timelineTrackArea.style.position =
            Position.Relative;

        _timelineTrackArea.style.flexShrink =
            0;

        _timelineTrackArea.style.backgroundColor =
            new Color(
                0.10f,
                0.10f,
                0.10f);

        _trackContent.Add(
            _timelineTrackArea);


        _trackScrollView
            .horizontalScroller
            .valueChanged +=
            value =>
            {
                UpdateRulerPosition(
                    value);

                UpdateCurrentFrameUI();
            };
    }


    private void UpdateRulerPosition(
        float scrollOffset)
    {
        if (_rulerContent == null)
        {
            return;
        }

        _rulerContent.style.left =
            -scrollOffset;
    }


    // =========================================================
    // Playhead
    // =========================================================

    private void CreatePlayhead()
    {
        _playheadHitArea =
            new VisualElement();

        _playheadHitArea.style.position =
            Position.Absolute;

        _playheadHitArea.style.top =
            0;

        _playheadHitArea.style.bottom =
            0;

        _playheadHitArea.style.width =
            PlayheadHitWidth;

        _playheadHitArea.style.backgroundColor =
            Color.clear;

        _playheadHitArea.pickingMode =
            PickingMode.Position;


        _centerLine =
            new VisualElement();

        _centerLine.style.position =
            Position.Absolute;

        _centerLine.style.top =
            0;

        _centerLine.style.bottom =
            0;

        _centerLine.style.width =
            PlayheadWidth;

        _centerLine.style.backgroundColor =
            new Color(
                1f,
                0.25f,
                0.25f);

        _centerLine.pickingMode =
            PickingMode.Ignore;

        _playheadHitArea.Add(
            _centerLine);


        _playheadHitArea.RegisterCallback<
            PointerDownEvent>(
            OnPlayheadPointerDown);

        _playheadHitArea.RegisterCallback<
            PointerMoveEvent>(
            OnPlayheadPointerMove);

        _playheadHitArea.RegisterCallback<
            PointerUpEvent>(
            OnPlayheadPointerUp);

        _rightPanel.Add(
            _playheadHitArea);
    }


    private void OnPlayheadPointerDown(
        PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        StopPlayback();

        _isDraggingPlayhead =
            true;

        _playheadHitArea.CapturePointer(
            evt.pointerId);

        UpdatePlayheadFromMouse(
            evt.position);

        evt.StopPropagation();
    }


    private void OnPlayheadPointerMove(
        PointerMoveEvent evt)
    {
        if (!_isDraggingPlayhead)
        {
            return;
        }

        if (!_playheadHitArea
                .HasPointerCapture(
                    evt.pointerId))
        {
            return;
        }

        HandleAutoScroll(
            evt.position);

        UpdatePlayheadFromMouse(
            evt.position);

        evt.StopPropagation();
    }


    private void OnPlayheadPointerUp(
        PointerUpEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        if (_playheadHitArea
            .HasPointerCapture(
                evt.pointerId))
        {
            _playheadHitArea.ReleasePointer(
                evt.pointerId);
        }

        _isDraggingPlayhead =
            false;

        evt.StopPropagation();
    }


    private void UpdatePlayheadFromMouse(
        Vector2 mousePosition)
    {
        Vector2 localPosition =
            _rightPanel.WorldToLocal(
                mousePosition);

        float visibleX =
            localPosition.x;

        float scrollOffset =
            _trackScrollView != null
                ? _trackScrollView
                    .scrollOffset.x
                : 0f;

        float contentX =
            visibleX +
            scrollOffset;

        int frame =
            Mathf.RoundToInt(
                contentX /
                PixelPerFrame);

        CurrentFrame =
            frame;
    }


    // =========================================================
    // Auto Scroll
    // =========================================================

    private void HandleAutoScroll(
        Vector2 mousePosition)
    {
        Vector2 localPosition =
            _rightPanel.WorldToLocal(
                mousePosition);

        float mouseX =
            localPosition.x;

        float viewportWidth =
            _timelineBottom
                .resolvedStyle
                .width;

        float currentScroll =
            _trackScrollView
                .scrollOffset.x;

        float newScroll =
            currentScroll;

        if (mouseX >
            viewportWidth -
            AutoScrollThreshold)
        {
            float distance =
                mouseX -
                (
                    viewportWidth -
                    AutoScrollThreshold
                );

            float speed =
                Mathf.Clamp(
                    distance,
                    1f,
                    AutoScrollThreshold);

            newScroll +=
                speed /
                AutoScrollThreshold *
                AutoScrollSpeed;
        }
        else if (mouseX <
                 AutoScrollThreshold)
        {
            float distance =
                AutoScrollThreshold -
                mouseX;

            float speed =
                Mathf.Clamp(
                    distance,
                    1f,
                    AutoScrollThreshold);

            newScroll -=
                speed /
                AutoScrollThreshold *
                AutoScrollSpeed;
        }

        float contentWidth =
            TimelineTickCount *
            CurrentFrameWidth;

        float viewportTrackWidth =
            _timelineBottom
                .resolvedStyle
                .width;

        float maxScroll =
            Mathf.Max(
                0f,
                contentWidth -
                viewportTrackWidth);

        newScroll =
            Mathf.Clamp(
                newScroll,
                0f,
                maxScroll);

        if (!Mathf.Approximately(
                newScroll,
                currentScroll))
        {
            Vector2 offset =
                _trackScrollView
                    .scrollOffset;

            offset.x =
                newScroll;

            _trackScrollView.scrollOffset =
                offset;
        }
    }


    // =========================================================
    // Current Frame UI
    // =========================================================

    private void UpdateCurrentFrameUI()
    {
        if (_currentFrameLabel != null)
        {
            _currentFrameLabel.text =
                $"Frame: {_currentFrame}";
        }

        if (_centerLine == null ||
            _playheadHitArea == null)
        {
            return;
        }

        float contentX =
            _currentFrame *
            PixelPerFrame;

        float scrollOffset =
            _trackScrollView != null
                ? _trackScrollView
                    .scrollOffset.x
                : 0f;

        float screenX =
            contentX -
            scrollOffset;

        float halfHitWidth =
            PlayheadHitWidth /
            2f;

        float playheadLeft =
            screenX -
            halfHitWidth;

        float playheadRight =
            playheadLeft +
            PlayheadHitWidth;

        float viewportWidth =
            _rightPanel != null
                ? _rightPanel
                    .resolvedStyle
                    .width
                : 0f;

        if (playheadLeft < 0f)
        {
            playheadLeft = 0f;
        }

        if (viewportWidth > 0f &&
            playheadRight >
            viewportWidth)
        {
            playheadLeft =
                viewportWidth -
                PlayheadHitWidth;
        }

        _playheadHitArea.style.left =
            playheadLeft;

        float lineLeft =
            screenX -
            playheadLeft;

        lineLeft =
            Mathf.Clamp(
                lineLeft,
                0f,
                PlayheadHitWidth -
                PlayheadWidth);

        _centerLine.style.left =
            lineLeft;
    }


    // =========================================================
    // Current Frame Label
    // =========================================================

    private void CreateCurrentFrameLabel()
    {
        _currentFrameLabel =
            new Label(
                "Frame: 0");

        _currentFrameLabel.style.position =
            Position.Absolute;

        _currentFrameLabel.style.right =
            8;

        _currentFrameLabel.style.bottom =
            22;

        _currentFrameLabel.style.fontSize =
            11;

        _currentFrameLabel.style.color =
            new Color(
                0.85f,
                0.85f,
                0.85f);

        _currentFrameLabel.style.backgroundColor =
            new Color(
                0.08f,
                0.08f,
                0.08f);

        _currentFrameLabel.style.paddingLeft =
            6;

        _currentFrameLabel.style.paddingRight =
            6;

        _currentFrameLabel.style.paddingTop =
            3;

        _currentFrameLabel.style.paddingBottom =
            3;

        _currentFrameLabel.pickingMode =
            PickingMode.Ignore;

        _timelineBottom.Add(
            _currentFrameLabel);
    }


    // =========================================================
    // Preview
    // =========================================================

    private void UpdatePreviewCharacter(
        GameObject character)
    {
        if (_previewSystem == null)
        {
            ActionData actionData =
                GetCurrentActionData();

            _previewSystem =
                new ActionPreviewSystem(
                    character,
                    actionData);
        }
        else
        {
            _previewSystem.SetCharacter(
                character);
        }
    }


    private void UpdatePreviewActionData(
        ActionData actionData)
    {
        if (_previewSystem == null)
        {
            GameObject character =
                GetCurrentCharacter();

            _previewSystem =
                new ActionPreviewSystem(
                    character,
                    actionData);
        }
        else
        {
            _previewSystem.SetActionData(
                actionData);
        }
    }


    private GameObject GetCurrentCharacter()
    {
        if (_characterField == null)
        {
            return null;
        }

        return _characterField.value
            as GameObject;
    }


    private ActionData GetCurrentActionData()
    {
        if (_actionViewDataField == null)
        {
            return null;
        }

        return _actionViewDataField.value
            as ActionData;
    }


    private void PreviewCurrentFrame()
    {
        if (_previewSystem == null)
        {
            return;
        }

        _previewSystem.Preview(
            CurrentTime,
            CurrentFrameRate,
            _isPlaying);
    }


    // =========================================================
    // Playback
    // =========================================================

    private void TogglePlay()
    {
        if (_isPlaying)
        {
            StopPlayback();
        }
        else
        {
            StartPlayback();
        }
    }


    private void StartPlayback()
    {
        if (_isPlaying)
        {
            return;
        }

        if (CurrentFrame >=
            TimelineFrameCount)
        {
            CurrentFrame = 0;
        }

        _isPlaying = true;

        _playTime =
            CurrentTime;

        _lastEditorTime =
            EditorApplication
                .timeSinceStartup;

        UpdatePlayButtonText();
    }


    private void StopPlayback()
    {
        _isPlaying = false;

        if (_previewSystem != null)
        {
            _previewSystem.StopPreview();
        }

        UpdatePlayButtonText();
    }


    private void OnEditorUpdate()
    {
        // Voice 的“一帧播放”依靠持续的 Editor Update
        // 自动检测是否已经到达停止时间。
        if (_previewSystem != null)
        {
            _previewSystem.Update();
        }

        if (_isPlaying)
        {
            OnEditorPlaybackUpdate();
        }

        if (_isPlaying || _previewSystem != null)
        {
            Repaint();
        }
    }


    private void OnEditorPlaybackUpdate()
    {
        if (!_isPlaying)
        {
            return;
        }

        double currentEditorTime =
            EditorApplication
                .timeSinceStartup;

        float deltaTime =
            (float)(
                currentEditorTime -
                _lastEditorTime);

        _lastEditorTime =
            currentEditorTime;

        _playTime +=
            deltaTime;

        float maxTime =
            (float)TimelineFrameCount /
            CurrentFrameRate;

        if (_playTime >= maxTime)
        {
            _playTime =
                maxTime;

            CurrentFrame =
                TimelineFrameCount;

            StopPlayback();

            return;
        }

        int frame =
            Mathf.RoundToInt(
                _playTime *
                CurrentFrameRate);

        CurrentFrame =
            frame;

        Repaint();
    }


    private void UpdatePlayButtonText()
    {
        if (_playButton == null)
        {
            return;
        }

        _playButton.text =
            _isPlaying
                ? "Pause"
                : "Play";
    }


    // =========================================================
    // Action View
    // =========================================================

    private void RebuildActionView()
    {
        if (_actionView != null)
        {
            if (_actionView.LeftElement != null)
            {
                _actionView.LeftElement
                    .RemoveFromHierarchy();
            }

            if (_actionView.RightElement != null)
            {
                _actionView.RightElement
                    .RemoveFromHierarchy();
            }

            _actionView = null;
        }


        if (_timeLineController != null)
        {
            _timeLineController.OnStructureChanged -=
                RebuildActionView;

            _timeLineController.ClearSelection();

            _timeLineController = null;
        }


        ActionData actionData =
            GetCurrentActionData();

        if (actionData == null)
        {
            if (_inspectorView != null)
            {
                _inspectorView.RemoveFromHierarchy();
                _inspectorView = null;
            }

            return;
        }


        _timeLineController =
            new TimeLineController(
                actionData,
                CurrentFrameRate);


        _timeLineController.OnStructureChanged +=
            RebuildActionView;


        _actionView =
            new ActionView(
                actionData,
                CurrentFrameRate,
                PixelPerFrame,
                _timeLineController);


        _trackNameArea.Add(
            _actionView.LeftElement);


        _timelineTrackArea.Add(
            _actionView.RightElement);


        if (_inspectorView != null)
        {
            _inspectorView.RemoveFromHierarchy();
            _inspectorView = null;
        }


        CreateInspectorPanel();
    }
}