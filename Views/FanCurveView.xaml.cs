using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using SmartFanCooling.ViewModels;
using System;

namespace SmartFanCooling.Views
{
    public sealed partial class FanCurveView : UserControl
    {
        private int _draggingNodeIndex = -1;

        public FanCurveView()
        {
            this.InitializeComponent();
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName != null && (args.PropertyName.StartsWith("CurveP") || args.PropertyName == nameof(MainViewModel.ActiveProfile)))
                        {
                            RedrawFanCurveGraph();
                        }
                    };
                }
            };
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private void FanCurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawFanCurveGraph();

        public void RedrawFanCurveGraph()
        {
            if (FanCurveCanvas == null || ViewModel == null) return;

            double width = FanCurveCanvas.ActualWidth;
            double height = FanCurveCanvas.ActualHeight;
            if (width < 50 || height < 30) return;

            double topY = 15.0;
            double bottomY = 185.0;
            double rangeY = bottomY - topY;

            GridH_100.X2 = width;
            GridH_75.X2  = width;
            GridH_50.X2  = width;
            GridH_25.X2  = width;
            GridH_0.X2   = width;

            double marginX = 15;
            double usableW = Math.Max(10, width - (2 * marginX));
            double stepX = usableW / 6.0;

            double[] nodeXs = new double[7];
            for (int i = 0; i < 7; i++) nodeXs[i] = marginX + (i * stepX);

            GridV_30.X1 = GridV_30.X2 = nodeXs[0];
            GridV_40.X1 = GridV_40.X2 = nodeXs[1];
            GridV_50.X1 = GridV_50.X2 = nodeXs[2];
            GridV_60.X1 = GridV_60.X2 = nodeXs[3];
            GridV_70.X1 = GridV_70.X2 = nodeXs[4];
            GridV_80.X1 = GridV_80.X2 = nodeXs[5];
            GridV_90.X1 = GridV_90.X2 = nodeXs[6];

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

            CurvePolyline.Points = pts;
            CurvePolygonFill.Points = fillPts;

            double halfNode = 5.0;
            Canvas.SetLeft(NodeRect_30, nodeXs[0] - halfNode); Canvas.SetTop(NodeRect_30, nodeYs[0] - halfNode);
            Canvas.SetLeft(NodeRect_40, nodeXs[1] - halfNode); Canvas.SetTop(NodeRect_40, nodeYs[1] - halfNode);
            Canvas.SetLeft(NodeRect_50, nodeXs[2] - halfNode); Canvas.SetTop(NodeRect_50, nodeYs[2] - halfNode);
            Canvas.SetLeft(NodeRect_60, nodeXs[3] - halfNode); Canvas.SetTop(NodeRect_60, nodeYs[3] - halfNode);
            Canvas.SetLeft(NodeRect_70, nodeXs[4] - halfNode); Canvas.SetTop(NodeRect_70, nodeYs[4] - halfNode);
            Canvas.SetLeft(NodeRect_80, nodeXs[5] - halfNode); Canvas.SetTop(NodeRect_80, nodeYs[5] - halfNode);
            Canvas.SetLeft(NodeRect_90, nodeXs[6] - halfNode); Canvas.SetTop(NodeRect_90, nodeYs[6] - halfNode);

            double labelOffset = 12.0;
            Canvas.SetLeft(TxtLabel_30, nodeXs[0] - labelOffset);
            Canvas.SetLeft(TxtLabel_40, nodeXs[1] - labelOffset);
            Canvas.SetLeft(TxtLabel_50, nodeXs[2] - labelOffset);
            Canvas.SetLeft(TxtLabel_60, nodeXs[3] - labelOffset);
            Canvas.SetLeft(TxtLabel_70, nodeXs[4] - labelOffset);
            Canvas.SetLeft(TxtLabel_80, nodeXs[5] - labelOffset);
            Canvas.SetLeft(TxtLabel_90, nodeXs[6] - labelOffset);
        }

        private void FanCurveCanvas_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (ViewModel == null || FanCurveCanvas == null) return;
            var pt = e.GetCurrentPoint(FanCurveCanvas).Position;

            double width = FanCurveCanvas.ActualWidth;
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
                FanCurveCanvas.CapturePointer(e.Pointer);
                UpdateNodeFromPointer(pt.Y);
            }
        }

        private void FanCurveCanvas_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_draggingNodeIndex >= 0 && FanCurveCanvas != null)
            {
                var pt = e.GetCurrentPoint(FanCurveCanvas).Position;
                UpdateNodeFromPointer(pt.Y);
            }
        }

        private void FanCurveCanvas_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_draggingNodeIndex >= 0 && FanCurveCanvas != null)
            {
                _draggingNodeIndex = -1;
                FanCurveCanvas.ReleasePointerCapture(e.Pointer);
            }
        }

        private void UpdateNodeFromPointer(double pointerY)
        {
            if (ViewModel == null) return;
            double topY = 15.0;
            double bottomY = 185.0;
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

            RedrawFanCurveGraph();
        }
    }
}
