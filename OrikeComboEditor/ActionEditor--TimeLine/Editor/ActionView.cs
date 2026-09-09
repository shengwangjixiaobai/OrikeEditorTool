using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

public class ActionView
{
    private readonly TimeLineController _controller;
    public const float TrackSpacing = 2f;


    private readonly ActionData _actionData;

    private readonly float _fps;
    private readonly float _frameWidth;

    private VisualElement _leftTrackContainer;
    private VisualElement _rightTrackContainer;

    private readonly List<TrackView> _trackViews =
        new List<TrackView>();

    // =========================================================
    // Marquee Selection
    // =========================================================

    private VisualElement _selectionBox;

    private bool _isSelecting;

    private int _selectionPointerId;

    private Vector2 _selectionStart;

    private Vector2 _selectionCurrent;

    private bool _marqueeAdditive;


    public ActionData ActionData =>
        _actionData;

    public VisualElement LeftElement =>
        _leftTrackContainer;

    public VisualElement RightElement =>
        _rightTrackContainer;


    public ActionView(
        ActionData actionData,
        float fps,
        float frameWidth,
        TimeLineController controller)
    {
        _actionData =
            actionData;

        _fps =
            fps;

        _frameWidth =
            frameWidth;

        _controller =
            controller;

        CreateContainers();

        BuildTracks();

        CreateSelectionBox();

        RegisterMarqueeEvents();

        RegisterEmptyAreaContextMenu();
    }


    // =========================================================
    // ????????????????
    // =========================================================

    private void CreateContainers()
    {
        _leftTrackContainer =
            new VisualElement();

        _leftTrackContainer.style.flexDirection =
            FlexDirection.Column;

        _leftTrackContainer.style.flexGrow =
            1;

        _leftTrackContainer.style.flexShrink =
            0;


        _rightTrackContainer =
            new VisualElement();

        _rightTrackContainer.style.flexDirection =
            FlexDirection.Column;

        _rightTrackContainer.style.flexGrow =
            1;

        _rightTrackContainer.style.flexShrink =
            0;

        _rightTrackContainer.style.position =
            Position.Relative;
    }


    // =========================================================
    // ???? Track
    // =========================================================

    private void BuildTracks()
    {
        if (_actionData == null ||
            _actionData.Tracks == null)
        {
            return;
        }

        for (int i = 0;
             i < _actionData.Tracks.Count;
             i++)
        {
            TrackData trackData =
                _actionData.Tracks[i];

            if (trackData == null)
            {
                continue;
            }

            TrackView trackView =
                new TrackView(
                    trackData,
                    _fps,
                    _frameWidth,
                    _controller);

            _trackViews.Add(
                trackView);

            _leftTrackContainer.Add(
                trackView.LeftElement);

            _rightTrackContainer.Add(
                trackView.RightElement);

            // ??????????????
            if (i <
                _actionData.Tracks.Count - 1)
            {
                AddTrackSpacing();
            }
        }
    }


    private void AddTrackSpacing()
    {
        VisualElement leftSpacing =
            new VisualElement();

        leftSpacing.style.height =
            TrackSpacing;

        leftSpacing.style.flexShrink =
            0;

        _leftTrackContainer.Add(
            leftSpacing);


        VisualElement rightSpacing =
            new VisualElement();

        rightSpacing.style.height =
            TrackSpacing;

        rightSpacing.style.flexShrink =
            0;

        _rightTrackContainer.Add(
            rightSpacing);
    }


    // =========================================================
    // ???????
    // =========================================================

    private void CreateSelectionBox()
    {
        _selectionBox =
            new VisualElement();

        _selectionBox.style.position =
            Position.Absolute;

        _selectionBox.style.left =
            0;

        _selectionBox.style.top =
            0;

        _selectionBox.style.width =
            0;

        _selectionBox.style.height =
            0;

        _selectionBox.style.backgroundColor =
            new Color(
                0.3f,
                0.6f,
                1f,
                0.18f);

        _selectionBox.style.borderLeftWidth =
            1;

        _selectionBox.style.borderRightWidth =
            1;

        _selectionBox.style.borderTopWidth =
            1;

        _selectionBox.style.borderBottomWidth =
            1;

        Color borderColor =
            new Color(
                0.3f,
                0.6f,
                1f);

        _selectionBox.style.borderLeftColor =
            borderColor;

        _selectionBox.style.borderRightColor =
            borderColor;

        _selectionBox.style.borderTopColor =
            borderColor;

        _selectionBox.style.borderBottomColor =
            borderColor;

        _selectionBox.pickingMode =
            PickingMode.Ignore;

        _selectionBox.style.display =
            DisplayStyle.None;

        _rightTrackContainer.Add(
            _selectionBox);
    }


    // =========================================================
    // ??????
    // =========================================================

    private void RegisterMarqueeEvents()
    {
        _rightTrackContainer.RegisterCallback<
            PointerDownEvent>(
            OnMarqueePointerDown,
            TrickleDown.TrickleDown);

        _rightTrackContainer.RegisterCallback<
            PointerMoveEvent>(
            OnMarqueePointerMove);

        _rightTrackContainer.RegisterCallback<
            PointerUpEvent>(
            OnMarqueePointerUp);
    }


    private void OnMarqueePointerDown(
        PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        // ?????????λ?????? Clip??
        // ????? ClipView ?????????? / ?????
        if (FindClipView(
                evt.target as VisualElement) != null)
        {
            return;
        }

        // 点事件标记自己处理点击 / 拖拽，不启动框选
        if (IsPointEventMarker(
                evt.target as VisualElement))
        {
            return;
        }

        _marqueeAdditive =
            evt.ctrlKey ||
            evt.commandKey;

        // ?????????????????????
        // Ctrl / Command ???????????????
        if (!_marqueeAdditive)
        {
            _controller?.ClearSelection();
        }

        _isSelecting =
            true;

        _selectionPointerId =
            evt.pointerId;

        _selectionStart =
            evt.localPosition;

        _selectionCurrent =
            _selectionStart;

        _rightTrackContainer.CapturePointer(
            evt.pointerId);

        _selectionBox.style.display =
            DisplayStyle.Flex;

        UpdateSelectionBox();

        UpdateMarqueeSelection();

        evt.StopPropagation();
    }


    private void OnMarqueePointerMove(
        PointerMoveEvent evt)
    {
        if (!_isSelecting ||
            evt.pointerId !=
            _selectionPointerId)
        {
            return;
        }

        if (!_rightTrackContainer.HasPointerCapture(
                _selectionPointerId))
        {
            return;
        }

        _selectionCurrent =
            evt.localPosition;

        UpdateSelectionBox();

        UpdateMarqueeSelection();

        evt.StopPropagation();
    }


    private void OnMarqueePointerUp(
        PointerUpEvent evt)
    {
        if (!_isSelecting ||
            evt.pointerId !=
            _selectionPointerId)
        {
            return;
        }

        _selectionCurrent =
            evt.localPosition;

        UpdateSelectionBox();

        UpdateMarqueeSelection();

        if (_rightTrackContainer.HasPointerCapture(
                _selectionPointerId))
        {
            _rightTrackContainer.ReleasePointer(
                _selectionPointerId);
        }

        _isSelecting =
            false;

        _selectionBox.style.display =
            DisplayStyle.None;

        evt.StopPropagation();
    }


    // =========================================================
    // ??????????
    // =========================================================

    private Rect GetSelectionRect()
    {
        float x =
            Mathf.Min(
                _selectionStart.x,
                _selectionCurrent.x);

        float y =
            Mathf.Min(
                _selectionStart.y,
                _selectionCurrent.y);

        float width =
            Mathf.Abs(
                _selectionCurrent.x -
                _selectionStart.x);

        float height =
            Mathf.Abs(
                _selectionCurrent.y -
                _selectionStart.y);

        return new Rect(
            x,
            y,
            width,
            height);
    }


    // =========================================================
    // ???????????
    // =========================================================

    private void UpdateSelectionBox()
    {
        Rect rect =
            GetSelectionRect();

        _selectionBox.style.left =
            rect.x;

        _selectionBox.style.top =
            rect.y;

        _selectionBox.style.width =
            rect.width;

        _selectionBox.style.height =
            rect.height;
    }


    // =========================================================
    // ?????????
    // =========================================================

    private void UpdateMarqueeSelection()
    {
        Rect selectionRect =
            GetSelectionRect();

        List<BaseClipData> result =
            new List<BaseClipData>();

        foreach (TrackView trackView
                 in _trackViews)
        {
            if (trackView == null)
            {
                continue;
            }

            foreach (ClipView clipView
                     in trackView.ClipViews)
            {
                if (clipView == null)
                {
                    continue;
                }

                Rect clipRect =
                    GetClipRectInActionView(
                        clipView);

                if (selectionRect.Overlaps(
                        clipRect,
                        true))
                {
                    result.Add(
                        clipView.Data);
                }
            }
        }


        // ????????
        // ??θ??????????Χ??????????
        if (!_marqueeAdditive)
        {
            _controller?.ClearSelection();
        }


        // Ctrl / Command ?????
        // ????????? Clip ?????????????С?
        if (_controller != null)
        {
            foreach (BaseClipData clip
                     in result)
            {
                if (clip == null)
                {
                    continue;
                }

                _controller.SelectClip(
                    clip,
                    true);
            }
        }
    }


    // =========================================================
    // ??? Clip ?? ActionView ?е????
    // =========================================================

    private Rect GetClipRectInActionView(
        ClipView clipView)
    {
        if (clipView == null)
        {
            return new Rect();
        }

        Rect worldRect =
            clipView.worldBound;

        Vector2 topLeft =
            _rightTrackContainer.WorldToLocal(
                new Vector2(
                    worldRect.xMin,
                    worldRect.yMin));

        Vector2 bottomRight =
            _rightTrackContainer.WorldToLocal(
                new Vector2(
                    worldRect.xMax,
                    worldRect.yMax));

        return Rect.MinMaxRect(
            topLeft.x,
            topLeft.y,
            bottomRight.x,
            bottomRight.y);
    }


    // =========================================================
    // ???????????????? Clip
    // =========================================================

    private ClipView FindClipView(
        VisualElement element)
    {
        VisualElement current =
            element;

        while (current != null)
        {
            ClipView clipView =
                current as ClipView;

            if (clipView != null)
            {
                return clipView;
            }

            current =
                current.parent;
        }

        return null;
    }


    // =========================================================
    // Is Point Event Marker
    //
    // 沿父链查找点事件标记（标记带 USS 类名）
    // =========================================================

    private bool IsPointEventMarker(
        VisualElement element)
    {
        VisualElement current =
            element;

        while (current != null)
        {
            if (current.ClassListContains(
                    TrackView.PointEventMarkerClassName))
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }


    // =========================================================
    // ????????????
    // =========================================================

    private void RegisterEmptyAreaContextMenu()
    {
        _leftTrackContainer.RegisterCallback<
            PointerDownEvent>(
            OnEmptyAreaPointerDown);
    }


    private void OnEmptyAreaPointerDown(
        PointerDownEvent evt)
    {
        if (evt.button != 1)
        {
            return;
        }

        VisualElement picked =
            evt.target as VisualElement;

        // ??е??????б??·????????????????
        // ????????? Track ?????
        if (picked != _leftTrackContainer)
        {
            return;
        }

        ShowAddTrackContextMenu();

        evt.StopPropagation();
    }


    private void ShowAddTrackContextMenu()
    {
        if (_controller == null)
        {
            return;
        }

        GenericMenu menu =
            new GenericMenu();

        menu.AddItem(
            new GUIContent(
                "Add/Animation Track"),
            false,
            () =>
            {
                _controller.AddAnimationTrack();
            });

        menu.AddItem(
            new GUIContent(
                "Add/Voice Track"),
            false,
            () =>
            {
                _controller.AddVoiceTrack();
            });

        menu.AddItem(
            new GUIContent(
                "Add/Effect Track"),
            false,
            () =>
            {
                _controller.AddEffectTrack();
            });

        menu.AddItem(
            new GUIContent(
                "Add/Hitbox Track"),
            false,
            () =>
            {
                _controller.AddHitboxTrack();
            });

        menu.AddItem(
            new GUIContent(
                "Add/Behitbox Track"),
            false,
            () =>
            {
                _controller.AddBehitboxTrack();
            });

        menu.AddItem(
            new GUIContent(
                "Add/State Event Track"),
            false,
            () =>
            {
                _controller.AddStateEventTrack();
            });

        menu.ShowAsContext();
    }


    // =========================================================
    // Update Layout
    // =========================================================

    public void UpdateLayout()
    {
        foreach (TrackView trackView
                 in _trackViews)
        {
            trackView.UpdateLayout();
        }
    }


}
