using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ClipView : VisualElement
{
    public const float DefaultHeight = 28f;

    private const float ResizeEdgeWidth = 6f;

    private enum EditMode
    {
        None,
        Move,
        ResizeLeft,
        ResizeRight
    }

    protected BaseClipData ClipData;

    private readonly TimeLineController _controller;

    private readonly float _fps;
    private readonly float _frameWidth;

    private Label _nameLabel;

    private EditMode _editMode =
        EditMode.None;

    private bool _isEditing;

    private int _pointerId;

    private float _lastPointerX;


    public BaseClipData Data =>
        ClipData;


    public ClipView(
        BaseClipData clipData,
        float fps,
        float frameWidth,
        TimeLineController controller)
    {
        ClipData = clipData;

        _fps = fps;

        _frameWidth = frameWidth;

        _controller = controller;

        style.position =
            Position.Absolute;

        style.height =
            DefaultHeight;

        CreateVisual();

        RegisterEvents();

        UpdateLayout(
            fps,
            frameWidth);
    }


    // =========================================================
    // Visual
    // =========================================================

    private void CreateVisual()
    {
        style.backgroundColor =
            new Color(
                0.25f,
                0.25f,
                0.25f);

        style.borderBottomWidth =
            3;

        style.borderBottomColor =
            GetBottomBorderColor();

        _nameLabel =
            new Label();

        _nameLabel.style.flexGrow =
            1;

        _nameLabel.style.unityTextAlign =
            TextAnchor.MiddleCenter;

        _nameLabel.style.fontSize =
            11;

        _nameLabel.style.color =
            new Color(
                0.9f,
                0.9f,
                0.9f);

        _nameLabel.pickingMode =
            PickingMode.Ignore;

        Add(
            _nameLabel);

        UpdateName();
    }


    // =========================================================
    // Events
    // =========================================================

    private void RegisterEvents()
    {
        RegisterCallback<PointerDownEvent>(
            OnPointerDown);

        RegisterCallback<PointerMoveEvent>(
            OnPointerMove);

        RegisterCallback<PointerUpEvent>(
            OnPointerUp);

        RegisterCallback<PointerLeaveEvent>(
            OnPointerLeave);

        RegisterCallback<PointerCaptureOutEvent>(
            OnPointerCaptureOut);

        if (_controller != null)
        {
            _controller.OnSelectionChanged +=
                OnSelectionChanged;

            _controller.OnMultiSelectionChanged +=
                OnMultiSelectionChanged;

            _controller.OnClipDataChanged +=
                OnClipDataChanged;
        }
    }


    // =========================================================
    // Pointer Down
    // =========================================================

    private void OnPointerDown(
        PointerDownEvent evt)
    {
        // -----------------------------------------------------
        // Right Click
        // -----------------------------------------------------

        if (evt.button == 1)
        {
            if (!_controller.IsSelected(
                    ClipData))
            {
                _controller.SelectClip(
                    ClipData,
                    false);
            }

            ShowContextMenu(
                evt.position);

            evt.StopPropagation();

            return;
        }


        // -----------------------------------------------------
        // Left Click
        // -----------------------------------------------------

        if (evt.button != 0)
        {
            return;
        }

        bool additive =
            evt.ctrlKey ||
            evt.commandKey;

        _controller.SelectClip(
            ClipData,
            additive);

        // 多选时不进入移动/缩放
        if (additive)
        {
            evt.StopPropagation();
            return;
        }

        _editMode =
            GetEditMode(
                evt.localPosition.x);

        _isEditing =
            true;

        _pointerId =
            evt.pointerId;

        _lastPointerX =
            evt.position.x;

        this.CapturePointer(
            evt.pointerId);

        evt.StopPropagation();
    }


    // =========================================================
    // Context Menu
    // =========================================================

    private void ShowContextMenu(
        Vector2 mousePosition)
    {
        GenericMenu menu =
            new GenericMenu();

        menu.AddItem(
            new GUIContent("Copy"),
            false,
            () =>
            {
                _controller.CopySelectedClips();
            });

        menu.AddItem(
            new GUIContent("Paste"),
            _controller.HasClipboard,
            () =>
            {
                TrackData track =
                    _controller.GetTrackForClip(
                        ClipData);

                if (track == null)
                {
                    return;
                }

                _controller.PasteClips(
                    track,
                    ClipData.EndTime);
            });

        menu.AddSeparator("");

        menu.AddItem(
            new GUIContent("Delete"),
            false,
            () =>
            {
                _controller.DeleteSelectedClips();
            });

        menu.ShowAsContext();
    }


    // =========================================================
    // Pointer Move
    // =========================================================

    private void OnPointerMove(
        PointerMoveEvent evt)
    {
        if (!_isEditing)
        {
            UpdateCursor(
                evt.localPosition.x);

            return;
        }

        if (!this.HasPointerCapture(
                _pointerId))
        {
            return;
        }

        float deltaPixel =
            evt.position.x -
            _lastPointerX;

        _lastPointerX =
            evt.position.x;

        float deltaTime =
            PixelToTime(
                deltaPixel);

        switch (_editMode)
        {
            case EditMode.Move:

                _controller.MoveClip(
                    ClipData,
                    deltaTime);

                break;

            case EditMode.ResizeLeft:

                _controller.ResizeClipLeft(
                    ClipData,
                    deltaTime);

                break;

            case EditMode.ResizeRight:

                _controller.ResizeClipRight(
                    ClipData,
                    deltaTime);

                break;
        }

        evt.StopPropagation();
    }


    // =========================================================
    // Pointer Up
    // =========================================================

    private void OnPointerUp(
        PointerUpEvent evt)
    {
        if (!_isEditing)
        {
            return;
        }

        if (this.HasPointerCapture(
                _pointerId))
        {
            this.ReleasePointer(
                _pointerId);
        }

        StopEditing();

        evt.StopPropagation();
    }


    // =========================================================
    // Pointer Leave
    // =========================================================

    private void OnPointerLeave(
        PointerLeaveEvent evt)
    {
        if (_isEditing)
        {
            return;
        }

        SetDefaultCursor();
    }


    // =========================================================
    // Pointer Capture Out
    // =========================================================

    private void OnPointerCaptureOut(
        PointerCaptureOutEvent evt)
    {
        StopEditing();
    }


    private void StopEditing()
    {
        _isEditing = false;

        _editMode =
            EditMode.None;
    }


    // =========================================================
    // Edit Mode
    // =========================================================

    private EditMode GetEditMode(
        float localX)
    {
        float width =
            resolvedStyle.width;

        if (localX <= ResizeEdgeWidth)
        {
            return EditMode.ResizeLeft;
        }

        if (localX >=
            width - ResizeEdgeWidth)
        {
            return EditMode.ResizeRight;
        }

        return EditMode.Move;
    }


    // =========================================================
    // Cursor
    // =========================================================

    private void UpdateCursor(
        float localX)
    {
        EditMode mode =
            GetEditMode(
                localX);

        bool isResizing =
            mode == EditMode.ResizeLeft ||
            mode == EditMode.ResizeRight;

        EnableInClassList(
            "timeline-splitter",
            isResizing);
    }


    private void SetDefaultCursor()
    {
        RemoveFromClassList(
            "timeline-splitter");

        style.cursor =
            StyleKeyword.Null;
    }


    // =========================================================
    // Pixel To Time
    // =========================================================

    private float PixelToTime(
        float pixel)
    {
        float pixelsPerSecond =
            _fps *
            _frameWidth;

        if (pixelsPerSecond <= 0f)
        {
            return 0f;
        }

        return pixel /
               pixelsPerSecond;
    }


    // =========================================================
    // Selection Changed
    // =========================================================

    private void OnSelectionChanged(
        BaseClipData selectedClip)
    {
        UpdateSelectionVisual();
    }


    private void OnMultiSelectionChanged(
        System.Collections.Generic.IReadOnlyList<BaseClipData>
            selectedClips)
    {
        UpdateSelectionVisual();
    }


    private void UpdateSelectionVisual()
    {
        bool selected =
            _controller != null &&
            _controller.IsSelected(
                ClipData);

        if (selected)
        {
            style.borderTopWidth = 2;
            style.borderLeftWidth = 2;
            style.borderRightWidth = 2;

            style.borderTopColor =
                new Color(
                    0.9f,
                    0.7f,
                    0.2f);

            style.borderLeftColor =
                new Color(
                    0.9f,
                    0.7f,
                    0.2f);

            style.borderRightColor =
                new Color(
                    0.9f,
                    0.7f,
                    0.2f);
        }
        else
        {
            style.borderTopWidth = 0;
            style.borderLeftWidth = 0;
            style.borderRightWidth = 0;
        }
    }


    // =========================================================
    // Clip Data Changed
    // =========================================================

    private void OnClipDataChanged(
        BaseClipData clipData)
    {
        if (clipData != ClipData)
        {
            return;
        }

        UpdateName();

        UpdateLayout(
            _fps,
            _frameWidth);
    }


    // =========================================================
    // Border
    // =========================================================

    protected virtual Color GetBottomBorderColor()
    {
        return new Color(
            0.3f,
            0.6f,
            1f);
    }


    // =========================================================
    // Name
    // =========================================================

    private void UpdateName()
    {
        if (ClipData
            is AnimationClipData animationClipData)
        {
            if (animationClipData.Animation != null)
            {
                _nameLabel.text =
                    animationClipData.Animation.name;
            }
            else
            {
                _nameLabel.text =
                    "Animation";
            }
        }
        else
        {
            _nameLabel.text =
                "Clip";
        }
    }


    // =========================================================
    // Layout
    // =========================================================

    public virtual void UpdateLayout(
        float fps,
        float frameWidth)
    {
        if (ClipData == null)
        {
            return;
        }

        float startFrame =
            ClipData.StartTime *
            fps;

        float lengthFrame =
            ClipData.Length *
            fps;

        float x =
            startFrame *
            frameWidth;

        float width =
            lengthFrame *
            frameWidth;

        style.left =
            x;

        style.width =
            Mathf.Max(
                width,
                4f);
    }
}