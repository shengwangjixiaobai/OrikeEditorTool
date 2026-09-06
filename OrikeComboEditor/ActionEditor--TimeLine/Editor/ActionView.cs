using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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

        _leftTrackContainer.style.flexGrow = 1;

        _leftTrackContainer.style.flexShrink = 0;


        _rightTrackContainer =
            new VisualElement();

        _rightTrackContainer.style.flexDirection =
            FlexDirection.Column;

        _rightTrackContainer.style.flexGrow = 1;

        _rightTrackContainer.style.flexShrink = 0;
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
                continue;

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

        leftSpacing.style.flexShrink = 0;

        _leftTrackContainer.Add(
            leftSpacing);


        VisualElement rightSpacing =
            new VisualElement();

        rightSpacing.style.height =
            TrackSpacing;

        rightSpacing.style.flexShrink = 0;

        _rightTrackContainer.Add(
            rightSpacing);
    }

    public void UpdateLayout()
    {
        foreach (TrackView trackView
                 in _trackViews)
        {
            trackView.UpdateLayout();
        }
    }
}