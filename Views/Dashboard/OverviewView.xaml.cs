using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SmartFanCooling.ViewModels;
using SmartFanCooling.Models;
using System;

namespace SmartFanCooling.Views.Dashboard
{
    public sealed partial class OverviewView : UserControl
    {
        private int _draggingNodeIndex = -1;
        private Storyboard? _rotorStoryboard;
        private DoubleAnimation? _rotorAnimation;
        private RotateTransform? _rotorTransform;
        private ArcSegment? _fanArcSegment;

        public OverviewView()
        {
            this.InitializeComponent();

            this.Loaded += (s, e) =>
            {
                InitializeGaugeArcs();
                SetupGPUFanAnimation();
            };

            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == null) return;

                        if (args.PropertyName.StartsWith("CurveP") || args.PropertyName == nameof(MainViewModel.ActiveProfile))
                        {
                            RedrawOverviewFanCurveGraph();
                        }
                        else if (args.PropertyName == nameof(MainViewModel.FanPwm) || args.PropertyName == nameof(MainViewModel.FanRpm))
                        {
                            UpdateFanGaugeArc();
                            UpdateFanRotorSpeed();
                        }
                    };

                    UpdateFanGaugeArc();
                    UpdateFanRotorSpeed();
                }
            };
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private Microsoft.UI.Xaml.Shapes.Path? _fanTrackArcPath;
        private Microsoft.UI.Xaml.Shapes.Path? _fanArcPath;

        private void InitializeGaugeArcs()
        {
            if (GaugeArcCanvas != null && _fanTrackArcPath == null)
            {
                _fanTrackArcPath = new Microsoft.UI.Xaml.Shapes.Path
                {
                    Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 26, 36, 54)),
                    StrokeThickness = 10,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Data = Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                        typeof(Geometry),
                        "M 52,188 A 96 96 0 1 1 188,188"
                    ) as Geometry
                };
                GaugeArcCanvas.Children.Add(_fanTrackArcPath);

                _fanArcSegment = new ArcSegment
                {
                    Point = new Windows.Foundation.Point(52, 188),
                    Size = new Windows.Foundation.Size(96, 96),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = false
                };

                var figure = new PathFigure
                {
                    StartPoint = new Windows.Foundation.Point(52, 188),
                    IsClosed = false
                };
                figure.Segments.Add(_fanArcSegment);

                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);

                _fanArcPath = new Microsoft.UI.Xaml.Shapes.Path
                {
                    Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 240, 255)),
                    StrokeThickness = 10,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Data = geometry
                };
                GaugeArcCanvas.Children.Add(_fanArcPath);
            }

            if (FanRotorCanvas != null && FanRotorCanvas.Children.Count == 0)
            {
                int bladeCount = 7;
                double angleStep = 360.0 / bladeCount;
                for (int i = 0; i < bladeCount; i++)
                {
                    var bladePath = new Microsoft.UI.Xaml.Shapes.Path
                    {
                        Data = Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                            typeof(Geometry),
                            "M 120,55 C 102,20 128,4 148,2 C 142,28 130,46 120,55 Z"
                        ) as Geometry,
                        Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(90, 0, 240, 255)),
                        Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 240, 255)),
                        StrokeThickness = 1.5,
                        RenderTransform = new RotateTransform { Angle = i * angleStep, CenterX = 120, CenterY = 120 }
                    };
                    FanRotorCanvas.Children.Add(bladePath);
                }
            }

            UpdateFanGaugeArc();
        }

        private void SetupGPUFanAnimation()
        {
            if (FanRotorCanvas == null || _rotorStoryboard != null) return;

            _rotorTransform = new RotateTransform { CenterX = 120, CenterY = 120 };
            FanRotorCanvas.RenderTransform = _rotorTransform;

            _rotorAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(1.5)),
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard.SetTarget(_rotorAnimation, _rotorTransform);
            Storyboard.SetTargetProperty(_rotorAnimation, "Angle");

            _rotorStoryboard = new Storyboard();
            _rotorStoryboard.Children.Add(_rotorAnimation);
            _rotorStoryboard.Begin();

            UpdateFanRotorSpeed();
        }

        private void UpdateFanRotorSpeed()
        {
            if (_rotorStoryboard == null) return;

            int rpm = ViewModel?.FanRpm ?? 0;
            int pwm = ViewModel?.FanPwm ?? 0;

            double ratio = 0.0;
            if (rpm > 0)
            {
                ratio = rpm / 2800.0;
            }
            else if (pwm > 0)
            {
                ratio = pwm / 100.0;
            }

            if (_rotorStoryboard.GetCurrentState() == ClockState.Stopped || _rotorStoryboard.GetCurrentState() == ClockState.Filling)
            {
                _rotorStoryboard.Begin();
            }

            // Smooth GPU animation speed ratio based on fan speed (0.0 stops, >0 scales speed)
            _rotorStoryboard.SpeedRatio = ratio < 0.01 ? 0.0 : Math.Max(0.15, ratio * 2.5);
        }

        public void UpdateFanGaugeArc()
        {
            if (_fanArcSegment == null) return;

            int pwm = Math.Clamp(ViewModel?.FanPwm ?? 0, 0, 100);

            // Arc sweep from 135 deg to 405 deg (270 deg total range)
            double startAngleDeg = 135.0;
            double sweepRangeDeg = 270.0;
            double currentAngleDeg = startAngleDeg + (pwm / 100.0 * sweepRangeDeg);
            double currentAngleRad = currentAngleDeg * Math.PI / 180.0;

            double centerX = 120.0;
            double centerY = 120.0;
            double radius = 96.0;

            double endX = centerX + radius * Math.Cos(currentAngleRad);
            double endY = centerY + radius * Math.Sin(currentAngleRad);

            // Direct property update on ArcSegment (Zero Allocation, Direct GPU Render)
            _fanArcSegment.Point = new Windows.Foundation.Point(endX, endY);
            _fanArcSegment.IsLargeArc = (pwm / 100.0 * sweepRangeDeg) > 180.0;
        }

        private void ProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FanProfile profile && ViewModel != null)
            {
                ViewModel.SelectProfile(profile);
            }
        }

        private void OnPresetButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RpmPreset preset && ViewModel != null)
            {
                ViewModel.SelectRpmPreset(preset);
            }
        }

        private void OnDeletePresetClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is RpmPreset preset && ViewModel != null)
            {
                ViewModel.DeleteRpmPreset(preset);
            }
        }

        private void OverviewFanCurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawOverviewFanCurveGraph();

        public void RedrawOverviewFanCurveGraph()
        {
            if (OverviewFanCurveCanvas == null || ViewModel == null) return;

            double width = OverviewFanCurveCanvas.ActualWidth;
            double height = OverviewFanCurveCanvas.ActualHeight;
            if (width < 50 || height < 30) return;

            double topY = 10.0;
            double bottomY = 150.0;
            double rangeY = bottomY - topY;

            O_GridH_100.X2 = width;
            O_GridH_75.X2  = width;
            O_GridH_50.X2  = width;
            O_GridH_25.X2  = width;
            O_GridH_0.X2   = width;

            double marginX = 15;
            double usableW = Math.Max(10, width - (2 * marginX));
            double stepX = usableW / 6.0;

            double[] nodeXs = new double[7];
            for (int i = 0; i < 7; i++) nodeXs[i] = marginX + (i * stepX);

            O_GridV_30.X1 = O_GridV_30.X2 = nodeXs[0];
            O_GridV_40.X1 = O_GridV_40.X2 = nodeXs[1];
            O_GridV_50.X1 = O_GridV_50.X2 = nodeXs[2];
            O_GridV_60.X1 = O_GridV_60.X2 = nodeXs[3];
            O_GridV_70.X1 = O_GridV_70.X2 = nodeXs[4];
            O_GridV_80.X1 = O_GridV_80.X2 = nodeXs[5];
            O_GridV_90.X1 = O_GridV_90.X2 = nodeXs[6];

            int[] pwms = new int[]
            {
                ViewModel.CurveP30, ViewModel.CurveP40, ViewModel.CurveP50,
                ViewModel.CurveP60, ViewModel.CurveP70, ViewModel.CurveP80, ViewModel.CurveP90
            };

            double[] nodeYs = new double[7];
            for (int i = 0; i < 7; i++) nodeYs[i] = bottomY - (pwms[i] / 100.0 * rangeY);

            var pts = new Microsoft.UI.Xaml.Media.PointCollection();
            var fillPts = new Microsoft.UI.Xaml.Media.PointCollection();
            fillPts.Add(new Windows.Foundation.Point(nodeXs[0], bottomY));

            for (int i = 0; i < 7; i++)
            {
                var pt = new Windows.Foundation.Point(nodeXs[i], nodeYs[i]);
                pts.Add(pt);
                fillPts.Add(pt);
            }
            fillPts.Add(new Windows.Foundation.Point(nodeXs[6], bottomY));

            O_CurvePolyline.Points = pts;
            O_CurvePolygonFill.Points = fillPts;

            double halfNode = 4.0;
            Canvas.SetLeft(O_NodeRect_30, nodeXs[0] - halfNode); Canvas.SetTop(O_NodeRect_30, nodeYs[0] - halfNode);
            Canvas.SetLeft(O_NodeRect_40, nodeXs[1] - halfNode); Canvas.SetTop(O_NodeRect_40, nodeYs[1] - halfNode);
            Canvas.SetLeft(O_NodeRect_50, nodeXs[2] - halfNode); Canvas.SetTop(O_NodeRect_50, nodeYs[2] - halfNode);
            Canvas.SetLeft(O_NodeRect_60, nodeXs[3] - halfNode); Canvas.SetTop(O_NodeRect_60, nodeYs[3] - halfNode);
            Canvas.SetLeft(O_NodeRect_70, nodeXs[4] - halfNode); Canvas.SetTop(O_NodeRect_70, nodeYs[4] - halfNode);
            Canvas.SetLeft(O_NodeRect_80, nodeXs[5] - halfNode); Canvas.SetTop(O_NodeRect_80, nodeYs[5] - halfNode);
            Canvas.SetLeft(O_NodeRect_90, nodeXs[6] - halfNode); Canvas.SetTop(O_NodeRect_90, nodeYs[6] - halfNode);

            double labelOffset = 10.0;
            Canvas.SetLeft(O_TxtLabel_30, nodeXs[0] - labelOffset);
            Canvas.SetLeft(O_TxtLabel_40, nodeXs[1] - labelOffset);
            Canvas.SetLeft(O_TxtLabel_50, nodeXs[2] - labelOffset);
            Canvas.SetLeft(O_TxtLabel_60, nodeXs[3] - labelOffset);
            Canvas.SetLeft(O_TxtLabel_70, nodeXs[4] - labelOffset);
            Canvas.SetLeft(O_TxtLabel_80, nodeXs[5] - labelOffset);
            Canvas.SetLeft(O_TxtLabel_90, nodeXs[6] - labelOffset);
        }

        private void OverviewFanCurveCanvas_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (ViewModel == null || OverviewFanCurveCanvas == null) return;
            var pt = e.GetCurrentPoint(OverviewFanCurveCanvas).Position;

            double width = OverviewFanCurveCanvas.ActualWidth;
            double marginX = 15;
            double usableW = Math.Max(10, width - (2 * marginX));
            double stepX = usableW / 6.0;

            int closestIndex = -1;
            double minDist = 35;

            for (int i = 0; i < 7; i++)
            {
                double nodeX = marginX + (i * stepX);
                double distX = Math.Abs(pt.X - nodeX);
                if (distX < minDist)
                {
                    minDist = distX;
                    closestIndex = i;
                }
            }

            if (closestIndex >= 0)
            {
                _draggingNodeIndex = closestIndex;
                OverviewFanCurveCanvas.CapturePointer(e.Pointer);
                UpdateNodeFromPointer(pt.Y);
            }
        }

        private void OverviewFanCurveCanvas_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_draggingNodeIndex >= 0 && OverviewFanCurveCanvas != null)
            {
                var pt = e.GetCurrentPoint(OverviewFanCurveCanvas).Position;
                UpdateNodeFromPointer(pt.Y);
            }
        }

        private void OverviewFanCurveCanvas_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_draggingNodeIndex >= 0 && OverviewFanCurveCanvas != null)
            {
                _draggingNodeIndex = -1;
                OverviewFanCurveCanvas.ReleasePointerCapture(e.Pointer);
            }
        }

        private void UpdateNodeFromPointer(double pointerY)
        {
            if (ViewModel == null) return;
            double topY = 10.0;
            double bottomY = 150.0;
            double rangeY = bottomY - topY;

            int newPwm = (int)Math.Clamp(Math.Round((bottomY - pointerY) / rangeY * 100.0), 0, 100);

            switch (_draggingNodeIndex)
            {
                case 0: ViewModel.CurveP30 = newPwm; break;
                case 1: ViewModel.CurveP40 = newPwm; break;
                case 2: ViewModel.CurveP50 = newPwm; break;
                case 3: ViewModel.CurveP60 = newPwm; break;
                case 4: ViewModel.CurveP70 = newPwm; break;
                case 5: ViewModel.CurveP80 = newPwm; break;
                case 6: ViewModel.CurveP90 = newPwm; break;
            }

            RedrawOverviewFanCurveGraph();
        }
    }
}
