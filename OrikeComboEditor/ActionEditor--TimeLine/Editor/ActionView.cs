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
    // 创建左右两个区域
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
    // 创建 Track
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

            // 最后一条不添加间隔
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
    // 框选区域
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
    // 框选事件
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

        // 如果鼠标按下的位置属于 Clip，
        // 就交给 ClipView 自己处理移动 / 缩放。
        if (FindClipView(
                evt.target as VisualElement) != null)
        {
            return;
        }

        _marqueeAdditive =
            evt.ctrlKey ||
            evt.commandKey;

        // 普通框选会先清除原来的选择。
        // Ctrl / Command 框选则保留原来的选择。
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
    // 获取框选矩形
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
    // 更新框选框显示
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
    // 更新框选结果
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


        // 普通框选：
        // 每次根据当前框选范围重新建立选择。
        if (!_marqueeAdditive)
        {
            _controller?.ClearSelection();
        }


        // Ctrl / Command 框选：
        // 将框选到的 Clip 添加到现有选择中。
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
    // 获取 Clip 在 ActionView 中的矩形
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
    // 查找当前元素是否属于 Clip
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
    // 右键空白区域菜单
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

        // 只有点击轨道列表下方真正的空白区域时，
        // 才显示添加 Track 菜单。
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
