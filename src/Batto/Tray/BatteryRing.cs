using System.Windows;
using System.Windows.Media;
using Batto.Models;

namespace Batto.Tray;

/// <summary>
/// WPF ring that shows a battery percentage as an arc. Used in the hover flyout.
/// Percent &lt; 0 means no reading.
/// </summary>
public class BatteryRing : FrameworkElement
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(int), typeof(BatteryRing),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsConnectedProperty = DependencyProperty.Register(
        nameof(IsConnected), typeof(bool), typeof(BatteryRing),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Percent
    {
        get => (int)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public bool IsConnected
    {
        get => (bool)GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        const double stroke = 3;
        double r = size / 2 - stroke / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        var trackBrush = new SolidColorBrush(IsConnected
            ? Color.FromArgb(60, 255, 255, 255)
            : Color.FromArgb(30, 255, 255, 255));
        dc.DrawEllipse(null, new Pen(trackBrush, stroke), center, r, r);

        if (!IsConnected || Percent < 0) return;

        var sysColor = BatteryRangeHelper.GetDrawingColor(BatteryRangeHelper.GetFaixa(Percent));
        var arcBrush = new SolidColorBrush(Color.FromRgb(sysColor.R, sysColor.G, sysColor.B));
        var arcPen = new Pen(arcBrush, stroke)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        double pct = Math.Clamp(Percent, 0, 100) / 100.0;
        if (pct >= 0.999)
        {
            dc.DrawEllipse(null, arcPen, center, r, r);
            return;
        }
        if (pct <= 0) return;

        double angle = pct * 2 * Math.PI;
        var start = new Point(center.X, center.Y - r);
        var end = new Point(center.X + r * Math.Sin(angle), center.Y - r * Math.Cos(angle));

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, angle > Math.PI, SweepDirection.Clockwise, true));
        dc.DrawGeometry(null, arcPen, new PathGeometry(new[] { figure }));
    }
}
