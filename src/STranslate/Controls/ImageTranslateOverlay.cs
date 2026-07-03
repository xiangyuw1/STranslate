using STranslate.Core;
using System.Windows;
using System.Windows.Media;

namespace STranslate.Controls;

/// <summary>
/// 在原图坐标系中绘制图片翻译译文的 retained-mode 矢量覆盖层。
/// </summary>
public sealed class ImageTranslateOverlay : FrameworkElement
{
    public ImageTranslateOverlayDocument? Document
    {
        get => (ImageTranslateOverlayDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(ImageTranslateOverlayDocument),
            typeof(ImageTranslateOverlay),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (Document is not { IsEmpty: false } document)
            return;

        // 先统一绘制背景，避免后一个块的背景覆盖前一个块的译文。
        foreach (var item in document.Items)
        {
            drawingContext.DrawRoundedRectangle(
                item.BackgroundBrush,
                null,
                item.Plan.OverlayRect,
                item.Plan.CornerRadius,
                item.Plan.CornerRadius);
        }

        foreach (var item in document.Items)
        {
            drawingContext.PushClip(new RectangleGeometry(item.Plan.TextClipRect));
            drawingContext.DrawText(
                item.ShadowText,
                new Point(item.TextPosition.X + 0.75, item.TextPosition.Y + 0.75));
            drawingContext.DrawText(item.FormattedText, item.TextPosition);
            drawingContext.Pop();
        }
    }
}
