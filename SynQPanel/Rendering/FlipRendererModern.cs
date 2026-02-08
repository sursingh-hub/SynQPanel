using SkiaSharp;
using SynQPanel.Drawing;
using SynQPanel.Models;
using System;

namespace SynQPanel.Rendering
{
    internal static class FlipRendererModern
    {
        public static void Draw(
            SkiaGraphics g,
            LockedImage currentLocked,
            LockedImage? nextLocked,
            int x,
            int y,
            int w,
            int h,
            int value,
            int digitCount,
            string imageFolder,
            float progress,
            float scale,
            string cacheHint
        )
        {
            // Sampling options (SkiaSharp-correct)
            var sampling = new SKSamplingOptions(
                SKFilterMode.Linear,
                SKMipmapMode.None
            );

            // ==========================
            // DRAW (AUTHORITATIVE FLIP)
            // ==========================
            currentLocked.AccessSK(w, h, image =>
            {
                int halfH = h / 2;
                int srcHalf = image.Height / 2;
                int dstHalf = halfH;

                float p = Math.Clamp(progress, 0f, 1f);

                using var paint = new SKPaint
                {
                    IsAntialias = true
                };

                // ================================
                // LAYER 1: BACK CARD (NEXT TOP HALF)
                // ================================
                if (nextLocked != null && nextLocked.Loaded)
                {
                    nextLocked.AccessSK(w, h, nextImage =>
                    {
                        g.Canvas.DrawImage(
                            nextImage,
                            new SKRect(0, 0, nextImage.Width, srcHalf),
                            new SKRect(x, y, x + w, y + dstHalf),
                            sampling,
                            paint
                        );
                    }, cache: true, cacheHint: cacheHint, grContext: null);
                }
                else
                {
                    // Fallback: draw CURRENT top half
                    g.Canvas.DrawImage(
                        image,
                        new SKRect(0, 0, image.Width, srcHalf),
                        new SKRect(x, y, x + w, y + dstHalf),
                        sampling,
                        paint
                    );
                }


                // ================================
                // LAYER 2: BOTTOM HALF SWITCH
                // ================================
                const float bottomSwitchPoint = 0.55f;

                if (p < bottomSwitchPoint)
                {
                    g.Canvas.DrawImage(
                        image,
                        new SKRect(0, srcHalf, image.Width, image.Height),
                        new SKRect(x, y + dstHalf, x + w, y + h),
                        sampling,
                        paint
                    );
                }
                else if (nextLocked != null && nextLocked.Loaded)
                {
                    nextLocked.AccessSK(w, h, nextImage =>
                    {
                        g.Canvas.DrawImage(
                            nextImage,
                            new SKRect(0, srcHalf, nextImage.Width, nextImage.Height),
                            new SKRect(x, y + dstHalf, x + w, y + h),
                            sampling,
                            paint
                        );
                    }, cache: true, cacheHint: cacheHint, grContext: null);
                }
                else
                {
                    // Fallback: keep CURRENT bottom half
                    g.Canvas.DrawImage(
                        image,
                        new SKRect(0, srcHalf, image.Width, image.Height),
                        new SKRect(x, y + dstHalf, x + w, y + h),
                        sampling,
                        paint
                    );
                }


                // ================================
                // LAYER 3: FRONT FLIP (TOP HALF)
                // ================================
                if (p < 0.95f)
                {
                    float flipProgress = p / 0.95f;
                    float scaleY = (float)Math.Cos(flipProgress * Math.PI / 2.0);
                    scaleY = Math.Max(0.02f, scaleY);

                    float pivotX = x + w / 2f;
                    float pivotY = y + dstHalf;

                    g.Canvas.Save();
                    g.Canvas.Translate(pivotX, pivotY);
                    g.Canvas.Scale(1f, scaleY);

                    float depthPush = flipProgress * 6f;
                    g.Canvas.Translate(0, -depthPush);

                    g.Canvas.DrawImage(
                        image,
                        new SKRect(0, 0, image.Width, srcHalf),
                        new SKRect(-w / 2f, -dstHalf, w / 2f, 0),
                        sampling,
                        paint
                    );

                    g.Canvas.Restore();
                }

                // ================================
                // HINGE SHADOW
                // ================================
                float hingeStrength = (float)Math.Sin(p * Math.PI);
                byte hingeAlpha = (byte)(hingeStrength * 120);

                if (hingeAlpha > 0)
                {
                    using var hingePaint = new SKPaint
                    {
                        Color = new SKColor(0, 0, 0, hingeAlpha),
                        IsAntialias = true
                    };

                    g.Canvas.DrawRect(
                        new SKRect(x, y + dstHalf - 1, x + w, y + dstHalf + 2),
                        hingePaint
                    );
                }
            });
        }
    }
}
