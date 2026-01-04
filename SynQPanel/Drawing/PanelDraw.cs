using FlyleafLib.MediaPlayer;
using SynQPanel.Extensions;
using SynQPanel.Models;
using SynQPanel.Plugins;
using SynQPanel.Utils;
using Serilog;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;


namespace SynQPanel.Drawing
{
    readonly struct SelectedRectangle(SKRect rect, int rotation = 0)
    {
        public readonly SKRect Rect = rect;
        public readonly int Rotation = rotation;
    }

    internal class PanelDraw
    {
        private static readonly ILogger Logger = Log.ForContext<PanelDraw>();
        private static readonly Stopwatch _selectionStopwatch = new();

        static PanelDraw()
        {
            _selectionStopwatch.Start();
        }

        public static void Run(Profile profile, SkiaGraphics g, bool preview = false, float scale = 1, bool cache = true, string cacheHint = "default", FpsCounter? fpsCounter = null)
        {
            var stopwatch = Stopwatch.StartNew();

            List<SelectedRectangle> selectedRectangles = [];

            if (SKColor.TryParse(profile.BackgroundColor, out var backgroundColor))
            {
                g.Clear(backgroundColor);
            }

            // ✅ Only auto-draw profile-level BG for RSLCD imports
            bool isRslcdProfile =
                !string.IsNullOrWhiteSpace(profile.ImportedSensorPanelPath) &&
                profile.ImportedSensorPanelPath.EndsWith(".rslcd", StringComparison.OrdinalIgnoreCase);



            // 🔹 Draw profile-level background image (LCARS 1024x600 etc.) if set
            if (isRslcdProfile &&
        !string.IsNullOrWhiteSpace(profile.BackgroundImagePath) &&
        File.Exists(profile.BackgroundImagePath))
            {
                try
                {
                    var bgImage = GetProfileBackgroundImage(profile.BackgroundImagePath);
                    if (bgImage != null && bgImage.Loaded)
                    {
                        var bgWidth = profile.Width;
                        var bgHeight = profile.Height;

                        // Draw at (0,0) covering the full profile area
                        g.DrawImage(bgImage, 0, 0, bgWidth, bgHeight, 0,
                            cache: true, cacheHint: "PROFILE-BG");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[PROFILE-BG] Exception while drawing background: " + ex);
                }
            }

           
            // 🔸 Existing item drawing logic (unchanged)
            foreach (var displayItem in SharedModel.Instance.GetProfileDisplayItemsCopy(profile))
            {
                if (displayItem.Hidden) continue;
                Draw(g, preview, scale, cache, cacheHint, displayItem, selectedRectangles);
            }

            if (!preview && SharedModel.Instance.SelectedProfile == profile && selectedRectangles.Count != 0)
            {
                if (ConfigModel.Instance.Settings.ShowGridLines)
                {
                    var gridSpace = ConfigModel.Instance.Settings.GridLinesSpacing;
                    var gridColor = ConfigModel.Instance.Settings.GridLinesColor;

                    var verticalLines = profile.Width / gridSpace;
                    for (int i = 1; i < verticalLines; i++)
                    {
                        //draw vertical lines
                        g.DrawLine(i * gridSpace, 0, i * gridSpace, profile.Height, gridColor, 1);
                    }

                    var horizontalLines = profile.Height / gridSpace;
                    for (int j = 1; j < horizontalLines; j++)
                    {
                        //draw horizontal lines
                        g.DrawLine(0, j * gridSpace, profile.Width, j * gridSpace, gridColor, 1);
                    }
                }

                if (SKColor.TryParse(ConfigModel.Instance.Settings.SelectedItemColor, out var color))
                {
                    var elapsedMilliseconds = _selectionStopwatch.ElapsedMilliseconds;

                    // Define "on" and "off" durations
                    int onDuration = 600;  // Time the rectangle is visible
                    int offDuration = 400; // Time the rectangle is invisible
                    int cycleDuration = onDuration + offDuration; // Total cycle time

                    // Determine if we are in the "on" phase
                    if (elapsedMilliseconds % cycleDuration < onDuration) // "on" phase
                    {
                        foreach (var rectangle in selectedRectangles)
                        {
                            using var path = RectToPath(rectangle.Rect, rectangle.Rotation, profile.Width, profile.Height, 2);

                            if (path != null)
                            {
                                g.DrawPath(path, color, 2);
                            }
                        }
                    }
                }
                else
                {
                    Logger.Warning("Failed to parse selected item color: {Color}", ConfigModel.Instance.Settings.SelectedItemColor);
                }
            }

            fpsCounter?.Update(stopwatch.ElapsedMilliseconds);

            if (profile.ShowFps && fpsCounter != null)
            {
                var renderingEngine = "CPU";

                if (g is SkiaGraphics skiaGraphics && skiaGraphics.OpenGL)
                {
                    renderingEngine = "OpenGL";
                }

                var text = $"{renderingEngine} | FPS {fpsCounter.FramesPerSecond} | {fpsCounter.FrameTime}ms";
                var font = "Consolas";
                var fontStyle = "Regular";
                var fontSize = 10;

                var rect = new SKRect(0, 0, profile.Width, 15);

                g.FillRectangle("#84000000", (int)rect.Left, (int)rect.Top, (int)rect.Width, (int)rect.Height);
                g.DrawString(profile.Name, font, fontStyle, fontSize, "#FF00FF00", (int)rect.Left + 5, (int)rect.Top, width: (int)rect.Width, height: (int)rect.Height);
                g.DrawString(text, font, fontStyle, fontSize, "#FF00FF00", (int)rect.Left, (int)rect.Top, width: (int)rect.Width - 5, height: (int)rect.Height, rightAlign: true);
            }
        }



        public static SKPath? RectToPath(SKRect rect, int rotation, float maxWidth, float maxHeight, float penWidth = 2)
        {
            float halfPenWidth = penWidth / 2f;
            var canvasBounds = new SKRect(halfPenWidth, halfPenWidth,
                                          maxWidth - halfPenWidth, maxHeight - halfPenWidth);

            // Early check: if the rectangle is entirely outside canvas bounds before rotation
            if (!rect.IntersectsWith(canvasBounds) && rotation == 0)
            {
                return null;
            }

            var path = new SKPath();
            path.AddRect(rect);

            // Apply rotation if needed
            if (rotation != 0)
            {
                float centerX = rect.MidX;
                float centerY = rect.MidY;
                var rotationMatrix = SKMatrix.CreateRotationDegrees(rotation, centerX, centerY);
                path.Transform(rotationMatrix);
            }

            // Get the bounds of the transformed path
            var pathBounds = path.Bounds;

            // Check if the path is entirely outside the canvas bounds
            if (pathBounds.Right < halfPenWidth || pathBounds.Left > maxWidth - halfPenWidth ||
                pathBounds.Bottom < halfPenWidth || pathBounds.Top > maxHeight - halfPenWidth)
            {
                path.Dispose();
                return null;
            }

            // Check if the path is entirely within the canvas bounds
            if (pathBounds.Left >= halfPenWidth && pathBounds.Top >= halfPenWidth &&
                pathBounds.Right <= maxWidth - halfPenWidth && pathBounds.Bottom <= maxHeight - halfPenWidth)
            {
                return path;
            }

            // Path partially overlaps with canvas, perform intersection
            using var canvasPath = new SKPath();
            canvasPath.AddRect(canvasBounds);
            var clippedPath = new SKPath();

            if (path.Op(canvasPath, SKPathOp.Intersect, clippedPath))
            {
                path.Dispose();
                return clippedPath;
            }

            // Intersection failed (shouldn't happen if bounds checks are correct)
            path.Dispose();
            clippedPath.Dispose();
            return null;
        }

        private static void Draw(SkiaGraphics g, bool preview, float scale, bool cache, string cacheHint, DisplayItem displayItem, List<SelectedRectangle> selectedRectangles)
        {
            var x = (int)Math.Floor(displayItem.X * scale);
            var y = (int)Math.Floor(displayItem.Y * scale);

            switch (displayItem)
            {
                case ArcDisplayItem arc:
                    {
                        if (arc.Hidden) break;

                        PanelDrawHelpers.DrawArcDisplayItem(
                            g,            // Graphics
                            preview,      // same variable used for other cases
                            scale,        // same scale variable used for other cases
                            cache,        // pass through your cache object
                            cacheHint,    // pass through hint if you have one
                            arc,          // the arc item
                            selectedRectangles // same list used for selection visuals
                        );

                        break;
                    }

                case GroupDisplayItem groupDisplayItem:
                    {
                        foreach (var item in groupDisplayItem.DisplayItemsCopy)
                        {
                            if (item.Hidden) continue;
                            Draw(g, preview, scale, cache, cacheHint, item, selectedRectangles);
                        }
                        break;
                    }
                case TextDisplayItem textDisplayItem:
                    {
                        (var text, var color) = textDisplayItem.EvaluateTextAndColor();

                        // Handle table display items separately
                        if (textDisplayItem is TableSensorDisplayItem tableSensorDisplayItem
                            && tableSensorDisplayItem.GetValue() is SensorReading sensorReading
                            && sensorReading.ValueTable is DataTable table)
                        {
                            DrawTableSensorDisplay(g, tableSensorDisplayItem, x, y, scale, color, table);
                            break;
                        }

                        // Regular text display
                        if (textDisplayItem.Marquee && textDisplayItem.Width > 0)
                        {
                            DrawMarqueeText(g, textDisplayItem, text, color, x, y, scale);
                        }
                        else
                        {
                            DrawNormalText(g, textDisplayItem, text, color, x, y, scale);
                        }

                        break;
                    }
                
                
                case ImageDisplayItem imageDisplayItem:
                    {
                        // SensorImageDisplayItem extra visibility check
                        if (imageDisplayItem is SensorImageDisplayItem sensorImageDisplayItem)
                        {
                            if (!sensorImageDisplayItem.ShouldShow())
                            {
                                break;
                            }
                        }

                        // 1) Try normal cache path (like all other images)
                        LockedImage? cachedImage = Cache.GetLocalImage(imageDisplayItem);

                        // 2) If this is our special RSLCD background and cache failed, force-load from disk
                        if (cachedImage == null &&
                            string.Equals(imageDisplayItem.Name, "RslcdBackground", StringComparison.OrdinalIgnoreCase))
                        {
                            var path = imageDisplayItem.CalculatedPath;

                            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            {
                                try
                                {
                                    cachedImage = Cache.GetLocalImageFromPath(filePath: path, initialiseIfMissing: true, imageDisplayItem: imageDisplayItem);


                                    System.Diagnostics.Debug.WriteLine($"[RSLCD-FORCELOAD] Loaded BG directly from disk: {path}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[RSLCD-FORCELOAD] Failed to load BG from disk: {path} - {ex}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[RSLCD-FORCELOAD] BG path missing or not found: {path}");
                            }
                        }

                        var size = imageDisplayItem.EvaluateSize();
                        var scaledWidth = (int)size.Width;
                        var scaledHeight = (int)size.Height;

                        scaledWidth = (int)Math.Floor(scaledWidth * scale);
                        scaledHeight = (int)Math.Floor(scaledHeight * scale);

                        if (scaledWidth <= 0 || scaledHeight <= 0)
                        {
                            return;
                        }

                        if (cachedImage != null)
                        {
                            g.DrawImage(
                                cachedImage,
                                x,
                                y,
                                scaledWidth,
                                scaledHeight,
                                imageDisplayItem.Rotation,
                                cache: imageDisplayItem.Cache && cache,
                                cacheHint: cacheHint);

                            if (imageDisplayItem.Layer)
                            {
                                g.FillRectangle(
                                    imageDisplayItem.LayerColor,
                                    x,
                                    y,
                                    scaledWidth,
                                    scaledHeight,
                                    rotation: imageDisplayItem.Rotation);
                            }

                            if (!preview &&
                                imageDisplayItem.ShowPanel &&
                                cachedImage.CurrentTime != null &&
                                cachedImage.FrameRate != null &&
                                cachedImage.Duration != null &&
                                cachedImage.VideoPlayerStatus != null)
                            {
                                if (g is SkiaGraphics skiaGraphics)
                                {
                                    var canvas = skiaGraphics.Canvas;

                                    // Calculate progress
                                    var progress = cachedImage.Duration.Value.TotalMilliseconds > 0
                                        ? cachedImage.CurrentTime.Value.TotalMilliseconds / cachedImage.Duration.Value.TotalMilliseconds
                                        : 0;

                                    // Rotation is already -180 to 180
                                    var rotation = imageDisplayItem.Rotation;

                                    // Round to nearest 90° to find which edge is at bottom
                                    var nearestQuadrant = (int)Math.Round(rotation / 90f) * 90;

                                    // Determine overlay width based on which edge is at bottom
                                    var overlayWidth = (nearestQuadrant == 90 || nearestQuadrant == -90)
                                        ? scaledHeight
                                        : scaledWidth;

                                    // Create overlay bitmap
                                    using var overlayBitmap = CreateVideoOverlay(
                                        overlayWidth,
                                        progress,
                                        cachedImage.CurrentTime.Value,
                                        cachedImage.Duration.Value,
                                        cachedImage.FrameRate.Value,
                                        cachedImage.Volume,
                                        cachedImage.VideoPlayerStatus.Value
                                    );

                                    // Draw overlay with rotation
                                    canvas.Save();
                                    try
                                    {
                                        var imageCenterX = x + scaledWidth / 2f;
                                        var imageCenterY = y + scaledHeight / 2f;

                                        // Apply image rotation
                                        canvas.Translate(imageCenterX, imageCenterY);
                                        canvas.RotateDegrees(rotation);

                                        // Position overlay at bottom based on quadrant
                                        var (overlayX, overlayY) = nearestQuadrant switch
                                        {
                                            0 => (-scaledWidth / 2f, scaledHeight / 2f - 40),          // Original bottom
                                            90 => (-scaledHeight / 2f, scaledWidth / 2f - 40),         // Left edge is bottom
                                            180 or -180 => (-scaledWidth / 2f, -scaledHeight / 2f),    // Top edge is bottom  
                                            -90 => (-scaledHeight / 2f, scaledWidth / 2f - 40),        // Right edge is bottom
                                            _ => (-scaledWidth / 2f, scaledHeight / 2f - 40)
                                        };

                                        // Apply additional rotation to make overlay horizontal
                                        canvas.RotateDegrees(-nearestQuadrant);

                                        using var overlayPaint = new SKPaint { IsAntialias = true };
                                        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest);

                                        using var image = SKImage.FromBitmap(overlayBitmap);

                                        canvas.DrawImage(
                                            image,
                                            SKRect.Create(overlayX, overlayY, image.Width, image.Height),
                                            sampling,
                                            overlayPaint);
                                    }
                                    finally
                                    {
                                        canvas.Restore();
                                    }
                                }
                            }
                        }

                        break;
                    }


                case GaugeDisplayItem gaugeDisplayItem:
                    {
                        var imageDisplayItem = gaugeDisplayItem.EvaluateImage();

                        if (imageDisplayItem != null)
                        {
                            var cachedImage = Cache.GetLocalImage(imageDisplayItem);

                            if (cachedImage != null)
                            {
                                var scaledWidth = gaugeDisplayItem.Width;
                                var scaledHeight = gaugeDisplayItem.Height;

                                if (scaledWidth == 0)
                                {
                                    scaledWidth = cachedImage.Width;
                                }

                                if (scaledHeight == 0)
                                {
                                    scaledHeight = cachedImage.Height;
                                }

                                scaledWidth = (int)Math.Floor(scaledWidth * gaugeDisplayItem.Scale / 100.0f * scale);
                                scaledHeight = (int)Math.Floor(scaledHeight * gaugeDisplayItem.Scale / 100.0f * scale);

                                g.DrawImage(cachedImage, x, y, scaledWidth, scaledHeight, 0, 0, 0, cache, cacheHint);
                            }
                        }
                        break;
                    }
                case ChartDisplayItem chartDisplayItem:
                    {
                        var width = scale == 1 ? (int)chartDisplayItem.Width : (int)Math.Floor(chartDisplayItem.Width * scale);
                        var height = scale == 1 ? (int)chartDisplayItem.Height : (int)Math.Floor(chartDisplayItem.Height * scale);

                        using var graphBitmap = new SKBitmap(chartDisplayItem.Width, chartDisplayItem.Height);
                        using var canvas = new SKCanvas(graphBitmap);

                        using var g1 = new SkiaGraphics(canvas, g.FontScale);
                        GraphDraw.Run(chartDisplayItem, g1, preview);

                        if (chartDisplayItem.FlipX)
                        {
                            g.DrawBitmap(graphBitmap, x, y, width, height, flipX: true);
                        }
                        else
                        {
                            g.DrawBitmap(graphBitmap, x, y, width, height);
                        }

                        break;
                    }

                case ShapeDisplayItem shapeDisplayItem:
                    {
                        var width = scale == 1 ? (int)shapeDisplayItem.Width : (int)Math.Floor(shapeDisplayItem.Width * scale);
                        var height = scale == 1 ? (int)shapeDisplayItem.Height : (int)Math.Floor(shapeDisplayItem.Height * scale);

                        var centerX = x + width / 2;
                        var centerY = y + height / 2;

                        using var path = new SKPath();

                        switch (shapeDisplayItem.Type)
                        {
                            case ShapeDisplayItem.ShapeType.Rectangle:
                                {
                                    path.AddRect(SKRect.Create(x, y, width, height));
                                    break;
                                }
                            case ShapeDisplayItem.ShapeType.Capsule:
                                {
                                    path.AddRoundRect(SKRect.Create(x, y, width, height), shapeDisplayItem.CornerRadius, shapeDisplayItem.CornerRadius);
                                    break;
                                }
                            case ShapeDisplayItem.ShapeType.Ellipse:
                                {
                                    path.AddOval(SKRect.Create(x, y, width, height));
                                    break;
                                }
                            case ShapeDisplayItem.ShapeType.Triangle:
                                {
                                    // Top point
                                    var topX = centerX;
                                    var topY = y;

                                    // Bottom left point
                                    var bottomLeftX = x;
                                    var bottomLeftY = y + height;

                                    // Bottom right point
                                    var bottomRightX = x + width;
                                    var bottomRightY = y + height;

                                    // Create the triangle path
                                    path.MoveTo(topX, topY);
                                    path.LineTo(bottomLeftX, bottomLeftY);
                                    path.LineTo(bottomRightX, bottomRightY);
                                    path.Close();
                                    break;
                                }
                            case ShapeDisplayItem.ShapeType.Star:
                                {
                                    // Star that stretches to fit the bounding box
                                    var scaleX = width / 2;
                                    var scaleY = height / 2;

                                    // Calculate the 5 outer points with scaling
                                    var points = new SKPoint[10];
                                    for (int i = 0; i < 10; i++)
                                    {
                                        var angle = (i * 36 - 90) * Math.PI / 180;
                                        var r = (i % 2 == 0) ? 1f : 0.382f; // 1 for outer, 0.382 for inner

                                        // Apply different scaling for X and Y
                                        points[i] = new SKPoint(
                                            centerX + (float)(r * scaleX * Math.Cos(angle)),
                                            centerY + (float)(r * scaleY * Math.Sin(angle))
                                        );
                                    }

                                    // Create the path
                                    path.MoveTo(points[0]);
                                    for (int i = 1; i < 10; i++)
                                    {
                                        path.LineTo(points[i]);
                                    }
                                    path.Close();
                                    break;
                                }
                            case ShapeDisplayItem.ShapeType.Pentagon:
                                {
                                    var scaleX = width / 2;
                                    var scaleY = height / 2;

                                    for (int i = 0; i < 5; i++)
                                    {
                                        var angle = (i * 72 - 90) * Math.PI / 180;
                                        var pointX = centerX + scaleX * Math.Cos(angle);
                                        var pointY = centerY + scaleY * Math.Sin(angle);

                                        if (i == 0)
                                            path.MoveTo((float)pointX, (float)pointY);
                                        else
                                            path.LineTo((float)pointX, (float)pointY);
                                    }
                                    path.Close();
                                    break;
                                }

                            case ShapeDisplayItem.ShapeType.Hexagon:
                                {
                                    var scaleX = width / 2;
                                    var scaleY = height / 2;

                                    for (int i = 0; i < 6; i++)
                                    {
                                        var angle = (i * 60 - 90) * Math.PI / 180;
                                        var pointX = centerX + scaleX * Math.Cos(angle);
                                        var pointY = centerY + scaleY * Math.Sin(angle);

                                        if (i == 0)
                                            path.MoveTo((float)pointX, (float)pointY);
                                        else
                                            path.LineTo((float)pointX, (float)pointY);
                                    }
                                    path.Close();
                                    break;
                                }

                            case ShapeDisplayItem.ShapeType.Plus:
                                {
                                    var thickness = Math.Min(width, height) / 3;
                                    var horizontalY = centerY - thickness / 2;
                                    var verticalX = centerX - thickness / 2;

                                    // Create plus shape as single path
                                    path.MoveTo(verticalX, y);
                                    path.LineTo(verticalX + thickness, y);
                                    path.LineTo(verticalX + thickness, horizontalY);
                                    path.LineTo(x + width, horizontalY);
                                    path.LineTo(x + width, horizontalY + thickness);
                                    path.LineTo(verticalX + thickness, horizontalY + thickness);
                                    path.LineTo(verticalX + thickness, y + height);
                                    path.LineTo(verticalX, y + height);
                                    path.LineTo(verticalX, horizontalY + thickness);
                                    path.LineTo(x, horizontalY + thickness);
                                    path.LineTo(x, horizontalY);
                                    path.LineTo(verticalX, horizontalY);
                                    path.Close();
                                    break;
                                }
                            case ShapeDisplayItem.ShapeType.Arrow:
                                {
                                    var headHeight = height * 0.4f;
                                    var shaftWidth = width * 0.5f;

                                    // Arrow pointing up
                                    path.MoveTo(centerX, y);                                    // Tip
                                    path.LineTo(x + width, y + headHeight);                     // Right head
                                    path.LineTo(x + width * 0.75f, y + headHeight);
                                    path.LineTo(x + width * 0.75f, y + height);                // Right shaft bottom
                                    path.LineTo(x + width * 0.25f, y + height);                // Left shaft bottom
                                    path.LineTo(x + width * 0.25f, y + headHeight);
                                    path.LineTo(x, y + headHeight);                            // Left head
                                    path.Close();
                                    break;
                                }

                            case ShapeDisplayItem.ShapeType.Octagon:
                                {
                                    var scaleX = width / 2;
                                    var scaleY = height / 2;

                                    for (int i = 0; i < 8; i++)
                                    {
                                        var angle = (i * 45 - 90) * Math.PI / 180;
                                        var pointX = centerX + scaleX * Math.Cos(angle);
                                        var pointY = centerY + scaleY * Math.Sin(angle);

                                        if (i == 0)
                                            path.MoveTo((float)pointX, (float)pointY);
                                        else
                                            path.LineTo((float)pointX, (float)pointY);
                                    }
                                    path.Close();
                                    break;
                                }

                            case ShapeDisplayItem.ShapeType.Trapezoid:
                                {
                                    var topInset = width * 0.2f;

                                    path.MoveTo(x + topInset, y);                    // Top left
                                    path.LineTo(x + width - topInset, y);            // Top right
                                    path.LineTo(x + width, y + height);              // Bottom right
                                    path.LineTo(x, y + height);                      // Bottom left
                                    path.Close();
                                    break;
                                }

                            case ShapeDisplayItem.ShapeType.Parallelogram:
                                {
                                    var skew = width * 0.25f;

                                    path.MoveTo(x + skew, y);                        // Top left
                                    path.LineTo(x + width, y);                       // Top right
                                    path.LineTo(x + width - skew, y + height);       // Bottom right
                                    path.LineTo(x, y + height);                      // Bottom left
                                    path.Close();
                                    break;
                                }
                        }


                        if (shapeDisplayItem.Rotation != 0)
                        {
                            var matrix = SKMatrix.CreateRotationDegrees(shapeDisplayItem.Rotation, centerX, centerY);
                            path.Transform(matrix);
                        }

                        if (shapeDisplayItem.ShowFill)
                        {
                            if (SKColor.TryParse(shapeDisplayItem.FillColor, out var color))
                            {
                                if (shapeDisplayItem.ShowGradient && SKColor.TryParse(shapeDisplayItem.GradientColor, out var gradientColor) && SKColor.TryParse(shapeDisplayItem.GradientColor2, out var gradientColor2))
                                {
                                    g.FillPath(path, color, gradientColor, gradientColor2, shapeDisplayItem.GetGradientAnimationOffset(), shapeDisplayItem.GradientType);
                                }
                                else
                                {
                                    g.FillPath(path, color);
                                }
                            }
                        }

                        if (shapeDisplayItem.ShowFrame)
                        {
                            if (SKColor.TryParse(shapeDisplayItem.FrameColor, out var color))
                            {
                                if (shapeDisplayItem.ShowGradient && SKColor.TryParse(shapeDisplayItem.GradientColor, out var gradientColor) && SKColor.TryParse(shapeDisplayItem.GradientColor2, out var gradientColor2))
                                {
                                    g.DrawPath(path, color, shapeDisplayItem.FrameThickness * scale, gradientColor, gradientColor2, shapeDisplayItem.GetGradientAnimationOffset(), shapeDisplayItem.GradientType);
                                }
                                else
                                {
                                    g.DrawPath(path, color, shapeDisplayItem.FrameThickness * scale);
                                }
                            }
                        }

                        break;
                    }
            }

            if (!preview && displayItem.Selected && displayItem is not GroupDisplayItem)
            {
                selectedRectangles.Add(new SelectedRectangle(displayItem.EvaluateBounds(), displayItem.Rotation));
            }
        }

        private static SKBitmap CreateVideoOverlay(float imageWidth, double progress, TimeSpan currentTime, TimeSpan duration, double frameRate, float volume, Status playerStatus)
        {
            const int overlayHeight = 40;
            var overlayBitmap = new SKBitmap((int)imageWidth, overlayHeight);
            using var overlayCanvas = new SKCanvas(overlayBitmap);

            // Draw gradient
            var gradientRect = SKRect.Create(0, 0, imageWidth, overlayHeight);
            using var gradientPaint = new SKPaint();
            gradientPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, overlayHeight),
                [SKColor.Parse("#00000000"), SKColor.Parse("#40000000"), SKColor.Parse("#90000000")],
                [0.0f, 0.6f, 1.0f],
                SKShaderTileMode.Clamp
            );
            overlayCanvas.DrawRect(gradientRect, gradientPaint);

            // Progress bar dimensions
            var progressBarHeight = 3f;
            var progressBarY = overlayHeight - 28f;
            var progressBarLeft = 12f;
            var progressBarWidth = imageWidth - 24f;

            var isLive = duration == TimeSpan.Zero;

            // Draw background progress bar
            using var backgroundPaint = new SKPaint { Color = SKColor.Parse("#4DFFFFFF"), IsAntialias = true };
            var backgroundRect = SKRect.Create(progressBarLeft, progressBarY, progressBarWidth, progressBarHeight);
            overlayCanvas.DrawRoundRect(backgroundRect, 1.5f, 1.5f, backgroundPaint);

            // Draw progress
            if (isLive || progress > 0)
            {
                var progressWidth = isLive ? progressBarWidth : (float)(progressBarWidth * Math.Min(progress, 1.0));
                var progressRect = SKRect.Create(progressBarLeft, progressBarY, progressWidth, progressBarHeight);
                
                if (isLive && playerStatus == Status.Playing)
                {
                    // Create animated gradient for live content
                    var animationTime = (float)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond % 3000) / 3000f;
                    
                    using var liveProgressPaint = new SKPaint { IsAntialias = true };
                    
                    // Create a smoother pulsing effect using sine wave
                    var pulse = (float)(Math.Sin(animationTime * Math.PI * 2) * 0.2 + 0.8);
                    
                    // YouTube Live style gradient - red to orange/yellow
                    var gradientColors = new SKColor[] {
                        SKColor.Parse("#CC0000"),  // Dark red
                        SKColor.Parse("#FF0000"),  // Red
                        SKColor.Parse("#FF3333"),  // Light red
                        SKColor.Parse("#FF6600"),  // Red-orange
                        SKColor.Parse("#FF9900"),  // Orange
                        SKColor.Parse("#FF6600"),  // Red-orange
                        SKColor.Parse("#FF3333"),  // Light red
                        SKColor.Parse("#FF0000"),  // Red
                        SKColor.Parse("#CC0000")   // Dark red
                    };
                    
                    // Fixed gradient positions for 9 colors
                    var gradientPositions = new float[] { 0f, 0.125f, 0.25f, 0.375f, 0.5f, 0.625f, 0.75f, 0.875f, 1f };
                    
                    // Create a wider gradient that moves across the bar
                    var gradientWidth = progressWidth * 2f;
                    var gradientOffset = -gradientWidth + (animationTime * gradientWidth * 2f);
                    
                    liveProgressPaint.Shader = SKShader.CreateLinearGradient(
                        new SKPoint(progressBarLeft + gradientOffset, progressBarY),
                        new SKPoint(progressBarLeft + gradientOffset + gradientWidth, progressBarY),
                        gradientColors,
                        gradientPositions,
                        SKShaderTileMode.Repeat
                    );
                    
                    overlayCanvas.DrawRoundRect(progressRect, 1.5f, 1.5f, liveProgressPaint);
                }
                else
                {
                    // Standard red bar for non-live or non-playing content
                    using var progressPaint = new SKPaint { Color = SKColor.Parse("#FF0000"), IsAntialias = true };
                    overlayCanvas.DrawRoundRect(progressRect, 1.5f, 1.5f, progressPaint);
                }

                // Draw progress circle (only for non-live content)
                if (!isLive && progress <= 1.0)
                {
                    var circleX = progressBarLeft + progressWidth;
                    var circleY = progressBarY + (progressBarHeight / 2f);

                    using var circlePaint = new SKPaint { Color = SKColor.Parse("#FF0000"), IsAntialias = true };
                    overlayCanvas.DrawCircle(circleX, circleY, 5f, circlePaint);

                    using var circleBorderPaint = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
                    overlayCanvas.DrawCircle(circleX, circleY, 5f, circleBorderPaint);
                }
            }

            var percent = (int)(volume * 100);
            var icon = percent switch
            {
                0 => "🔇",
                < 33 => "🔈",
                < 66 => "🔉",
                _ => "🔊"
            };

            // Measure text widths for positioning
            using var measurePaint = new SKPaint();

            if (isLive)
            {
                // Draw LIVE indicator
                using var liveFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI Symbol", SKFontStyle.Bold), 12f);
                using var livePaint = new SKPaint { Color = SKColor.Parse("#FF0000"), IsAntialias = true };

                var liveText = playerStatus == Status.Playing ? "LIVE" : "CONNECTING..";
                overlayCanvas.DrawText(liveText, progressBarLeft, overlayHeight - 8f, liveFont, livePaint);

                // Measure the LIVE text width
                var liveTextWidth = liveFont.MeasureText(liveText);

                // Draw volume icon to the right of LIVE
                using var iconFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI Emoji", SKFontStyle.Normal), 14f);
                using var iconPaint = new SKPaint { Color = SKColor.Parse("#FFFF00"), IsAntialias = true }; // Yellow color
                overlayCanvas.DrawText(icon, progressBarLeft + liveTextWidth + 10f, overlayHeight - 8f, iconFont, iconPaint);
            }
            else
            {
                // Draw time display for non-live content
                var timeDisplay = $"{currentTime:hh\\:mm\\:ss\\.fff} / {duration:hh\\:mm\\:ss\\.fff}";
                using var timeFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal), 12f);
                using var timePaint = new SKPaint { Color = SKColor.Parse("#CCFFFFFF"), IsAntialias = true };
                overlayCanvas.DrawText(timeDisplay, progressBarLeft, overlayHeight - 8f, timeFont, timePaint);

                // Measure the time text width
                var timeTextWidth = timeFont.MeasureText(timeDisplay);

                // Draw volume icon to the right of time
                using var iconFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI Emoji", SKFontStyle.Normal), 14f);
                using var iconPaint = new SKPaint { Color = SKColor.Parse("#FFFFFF"), IsAntialias = true }; // White color
                overlayCanvas.DrawText(icon, progressBarLeft + timeTextWidth + 10f, overlayHeight - 8f, iconFont, iconPaint);
            }


            // Draw right side display
            var frameRateText = $"{frameRate:F0} FPS";
            var rightDisplayText = $"{frameRateText}";

            using var rightFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI Symbol", SKFontStyle.Normal), 11f);
            using var rightPaint = new SKPaint { Color = SKColor.Parse("#CCFFFFFF"), IsAntialias = true };
            var rightTextWidth = rightFont.MeasureText(rightDisplayText);
            overlayCanvas.DrawText(rightDisplayText, progressBarLeft + progressBarWidth - rightTextWidth, overlayHeight - 8f, rightFont, rightPaint);

            return overlayBitmap;
        }

        private static void DrawTableSensorDisplay(SkiaGraphics g, TableSensorDisplayItem tableSensorDisplayItem, 
            int x, int y, float scale, string color, DataTable table)
        {
            var fontSize = (int)Math.Floor(tableSensorDisplayItem.FontSize * scale);
            var format = tableSensorDisplayItem.TableFormat;
            var maxRows = tableSensorDisplayItem.MaxRows;
            var formatParts = format.Split('|');

            if (formatParts.Length == 0) return;

            (float fWidth, float fHeight) = g.MeasureString("A", tableSensorDisplayItem.Font, tableSensorDisplayItem.FontStyle, 
                fontSize, tableSensorDisplayItem.Bold, tableSensorDisplayItem.Italic, 
                tableSensorDisplayItem.Underline, tableSensorDisplayItem.Strikeout);

            var tWidth = 0;
            for (int i = 0; i < formatParts.Length; i++)
            {
                var split = formatParts[i].Split(':');
                if (split.Length == 2 && 
                    int.TryParse(split[0], out var column) && 
                    column < table.Columns.Count && 
                    int.TryParse(split[1], out var length))
                {
                    // Draw header if enabled
                    if (tableSensorDisplayItem.ShowHeader)
                    {
                        g.DrawString(table.Columns[column].ColumnName, 
                            tableSensorDisplayItem.Font, tableSensorDisplayItem.FontStyle, fontSize, color,
                            x + tWidth, y,
                            i != 0 && tableSensorDisplayItem.RightAlign, tableSensorDisplayItem.CenterAlign, 
                            tableSensorDisplayItem.Bold, tableSensorDisplayItem.Italic, 
                            tableSensorDisplayItem.Underline, tableSensorDisplayItem.Strikeout, 
                            tableSensorDisplayItem.Wrap, tableSensorDisplayItem.Ellipsis, length, 0);
                    }

                    // Draw rows
                    var rows = Math.Min(table.Rows.Count, maxRows);
                    for (int j = 0; j < rows; j++)
                    {
                        if (table.Rows[j][column] is IPluginData pluginData)
                        {
                            g.DrawString(pluginData.ToString(), 
                                tableSensorDisplayItem.Font, tableSensorDisplayItem.FontStyle, fontSize, color,
                                x + tWidth, (int)(y + (fHeight * (j + (tableSensorDisplayItem.ShowHeader ? 1 : 0)))),
                                i != 0 && tableSensorDisplayItem.RightAlign, tableSensorDisplayItem.CenterAlign, 
                                tableSensorDisplayItem.Bold, tableSensorDisplayItem.Italic, 
                                tableSensorDisplayItem.Underline, tableSensorDisplayItem.Strikeout, 
                                tableSensorDisplayItem.Wrap, tableSensorDisplayItem.Ellipsis, length, 0);
                        }
                    }

                    tWidth += length + 10;
                }
            }
        }

        private static void DrawMarqueeText(SkiaGraphics g, TextDisplayItem textDisplayItem, 
            string text, string color, int x, int y, float scale)
        {
            var fontSize = (int)Math.Floor(textDisplayItem.FontSize * scale);
            var drawWidth = (int)(textDisplayItem.Width * scale);
            
            // Validate marquee speed to prevent division by zero
            var scrollSpeed = Math.Max(textDisplayItem.MarqueeSpeed, 1) * 1.0;
            var padding = (int)(textDisplayItem.MarqueeSpacing * scale); // Apply scale to spacing
            
            // Measure text once
            var (textWidth, textHeight) = g.MeasureString(text, textDisplayItem.Font, textDisplayItem.FontStyle,
                fontSize, textDisplayItem.Bold, textDisplayItem.Italic, textDisplayItem.Underline,
                textDisplayItem.Strikeout, false, false, 0, 0);

            if (textWidth <= drawWidth)
            {
                // Text fits, draw normally without marquee
                DrawNormalText(g, textDisplayItem, text, color, x, y, scale);
                return;
            }

            // Calculate animation
            var currentTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            var totalDistance = textWidth + padding;
            var duration = totalDistance / scrollSpeed * 1000; // milliseconds
            var progress = (currentTime % duration) / duration;
            var offset = (int)(progress * totalDistance);

            // Calculate height for clipping
            var clipHeight = textDisplayItem.Height > 0 
                ? (int)(textDisplayItem.Height * scale) 
                : (int)textHeight;

            // Set up clipping
            g.Canvas.Save();
            g.Canvas.ClipRect(new SKRect(x, y, x + drawWidth, y + clipHeight));

            // Draw text twice for infinite scroll
            // Note: Disable alignment for marquee as position is controlled by offset
            g.DrawString(text, textDisplayItem.Font, textDisplayItem.FontStyle, fontSize, color, 
                x - offset, y, false, false, // Disable alignment for marquee
                textDisplayItem.Bold, textDisplayItem.Italic, textDisplayItem.Underline, 
                textDisplayItem.Strikeout, false, false, 0);

            g.DrawString(text, textDisplayItem.Font, textDisplayItem.FontStyle, fontSize, color, 
                x - offset + (int)textWidth + padding, y, false, false, // Disable alignment for marquee
                textDisplayItem.Bold, textDisplayItem.Italic, textDisplayItem.Underline, 
                textDisplayItem.Strikeout, false, false, 0);

            g.Canvas.Restore();
        }

        private static void DrawNormalText(SkiaGraphics g, TextDisplayItem textDisplayItem, 
            string text, string color, int x, int y, float scale)
        {
            var fontSize = (int)Math.Floor(textDisplayItem.FontSize * scale);
            var drawWidth = (int)(textDisplayItem.Width * scale);
            
            g.DrawString(text, textDisplayItem.Font, textDisplayItem.FontStyle, fontSize, color, 
                x, y, textDisplayItem.RightAlign, textDisplayItem.CenterAlign,
                textDisplayItem.Bold, textDisplayItem.Italic, textDisplayItem.Underline, 
                textDisplayItem.Strikeout, textDisplayItem.Wrap, textDisplayItem.Ellipsis, drawWidth);
        }





        public static class PanelDrawHelpers
        {
            // Public helper: convert hex or decimal BGR -> System.Drawing.Color
            public static Color ColorFromDisplayItem(string? hexOrNull, int? decimalBgr = null, Color? fallback = null)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(hexOrNull))
                    {
                        return ColorTranslator.FromHtml(hexOrNull);
                    }
                    if (decimalBgr.HasValue)
                    {
                        int dec = decimalBgr.Value;
                        int b = dec & 0xFF;
                        int g = (dec >> 8) & 0xFF;
                        int r = (dec >> 16) & 0xFF;
                        return Color.FromArgb(r, g, b);
                    }
                }
                catch { /* ignore and fallback */ }

                return fallback ?? Color.White;
            }

            // Public static DrawArcDisplayItem — signature accepts everything it needs.
            // Adjust parameter order or types to match your caller as needed.
            // Skia-friendly arc renderer
            public static void DrawArcDisplayItem(
    SynQPanel.Drawing.SkiaGraphics g,
    bool preview,
    float scale,
    object cache,
    object cacheHint,
    ArcDisplayItem arc,
    System.Collections.Generic.IList<SynQPanel.Drawing.SelectedRectangle> selectedRectangles)
            {
                try
                {
                    if (g == null || arc == null) return;

                    // Get bounds from the display item (keeps consistent with other widgets)
                    var bounds = arc.EvaluateBounds(); // SKRect
                    float x = bounds.Left;
                    float y = bounds.Top;
                    float w = bounds.Width;
                    float h = bounds.Height;

                    // Avoid degenerate sizes
                    if (w <= 0 || h <= 0)
                    {
                        var paintPlaceholder = new SKPaint { IsStroke = true, StrokeWidth = 2f * scale, Color = SKColors.White };
                        g.Canvas.DrawCircle(x + 10 * scale, y + 10 * scale, 8 * scale, paintPlaceholder);
                        return;
                    }

                    // Evaluate sensor value (best-effort) to compute sweep percent
                    double valuePercent = 0.0;
                    try
                    {
                        // ArcDisplayItem should expose GetValue similar to GaugeDisplayItem
                        var getValueMethod = arc.GetType().GetMethod("GetValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        SensorReading? reading = null;
                        if (getValueMethod != null)
                            reading = getValueMethod.Invoke(arc, null) as SensorReading?;
                        else
                        {
                            // fallback: try public GetValue() if strongly-typed
                            try { reading = arc.GetValue(); } catch { /* ignore */ }
                        }

                        if (reading.HasValue)
                        {
                            var minProp = arc.GetType().GetProperty("MinValue"); // double?
                            var maxProp = arc.GetType().GetProperty("MaxValue");
                            double minv = minProp != null ? Convert.ToDouble(minProp.GetValue(arc) ?? 0) : 0.0;
                            double maxv = maxProp != null ? Convert.ToDouble(maxProp.GetValue(arc) ?? 100) : 100.0;

                            if (Math.Abs(maxv - minv) > double.Epsilon)
                            {
                                valuePercent = (reading.Value.ValueNow - minv) / (maxv - minv);
                                valuePercent = Math.Clamp(valuePercent, 0.0, 1.0);
                            }
                        }
                    }
                    catch { /* swallow - show empty arc if sensor unavailable */ }

                    // Colors: try to get color from the item (text/color helper)
                    var (_, colorStr) = arc.EvaluateTextAndColor(); // returns (text, colorHex)
                    SKColor mainColor = SKColors.White;
                    try
                    {
                        if (!string.IsNullOrEmpty(colorStr))
                        {
                            if (TryParseHexColor(colorStr.Trim(), out var parsed))
                                mainColor = parsed;
                        }
                    }
                    catch { mainColor = SKColors.White; }

                    // Basic geometry
                    float stroke = Math.Max(1f, 6f * scale);
                    float pad = stroke / 2f;
                    var rect = new SKRect(x + pad, y + pad, x + w - pad, y + h - pad);

                    // Determine start and sweep (try reflectively first; else defaults)
                    float startAngleDeg = 150f;
                    float sweepTotalDeg = 240f;

                    try
                    {
                        var startProp = arc.GetType().GetProperty("StartAngle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var sweepProp = arc.GetType().GetProperty("SweepAngle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (startProp != null)
                        {
                            var val = startProp.GetValue(arc);
                            if (val != null) startAngleDeg = Convert.ToSingle(val);
                        }
                        if (sweepProp != null)
                        {
                            var val = sweepProp.GetValue(arc);
                            if (val != null) sweepTotalDeg = Convert.ToSingle(val);
                        }
                    }
                    catch { /* ignore reflection issues, keep defaults */ }

                    float sweepFillDeg = (float)(sweepTotalDeg * valuePercent);

                    // Paints
                    var bgPaint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = stroke,
                        Color = new SKColor(0x33, 0x33, 0x33), // dark gray
                        IsAntialias = true,
                        StrokeCap = SKStrokeCap.Round
                    };

                    var fgPaint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = stroke,
                        Color = mainColor,
                        IsAntialias = true,
                        StrokeCap = SKStrokeCap.Round
                    };

                    // draw background arc (full)
                    g.Canvas.DrawArc(rect, startAngleDeg, sweepTotalDeg, false, bgPaint);

                    // draw filled sweep arc if > 0
                    if (sweepFillDeg > 0.0001f)
                    {
                        g.Canvas.DrawArc(rect, startAngleDeg, sweepFillDeg, false, fgPaint);
                    }

                    // optional: draw center text (value) if you want — uses arc.EvaluateText()
                    try
                    {
                        var txt = arc.EvaluateText();
                        if (!string.IsNullOrEmpty(txt))
                        {
                            var textPaint = new SKPaint
                            {
                                Color = mainColor,
                                TextSize = Math.Max(10f, 14f * scale),
                                IsAntialias = true,
                            };

                            var measured = new SKRect();
                            textPaint.MeasureText(txt, ref measured);
                            var textX = x + (w - measured.Width) / 2f - measured.Left;
                            var textY = y + (h + measured.Height) / 2f - measured.Bottom;
                            g.Canvas.DrawText(txt, textX, textY, textPaint);
                        }
                    }
                    catch { /* silent */ }

                    // selection highlighting: use SelectedRectangle.Rect (your struct)
                    try
                    {
                        if (selectedRectangles != null)
                        {
                            foreach (var sel in selectedRectangles)
                            {
                                var selRect = sel.Rect; // SKRect

                                if (RectsIntersect(selRect, bounds))
                                {
                                    var selPaint = new SKPaint
                                    {
                                        Style = SKPaintStyle.Stroke,
                                        Color = SKColors.Yellow.WithAlpha(0x90),
                                        StrokeWidth = Math.Max(2f, stroke / 2f),
                                        IsAntialias = true
                                    };
                                    g.Canvas.DrawOval(rect, selPaint);
                                    break; // draw only one highlight
                                }
                            }
                        }
                    }
                    catch { /* selection drawing must not crash renderer */ }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[PanelDrawHelpers.DrawArcDisplayItem] Error: " + ex);
                }
            }

            // Helper: robust SKColor hex parser
            private static bool TryParseHexColor(string hex, out SKColor color)
            {
                color = SKColors.White;
                if (string.IsNullOrEmpty(hex)) return false;

                hex = hex.TrimStart('#').Trim();

                try
                {
                    if (hex.Length == 6)
                    {
                        // RRGGBB
                        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                        color = new SKColor(r, g, b);
                        return true;
                    }
                    else if (hex.Length == 8)
                    {
                        // AARRGGBB
                        byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                        byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                        byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                        byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                        color = new SKColor(r, g, b, a);
                        return true;
                    }
                    // fallback simple parse attempts
                    int val = int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                    // if we reached here maybe single int - create color anyway
                    byte r2 = (byte)((val >> 16) & 0xFF);
                    byte g2 = (byte)((val >> 8) & 0xFF);
                    byte b2 = (byte)(val & 0xFF);
                    color = new SKColor(r2, g2, b2);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // Helper: SKRect intersection (safe, no external dependency)
            private static bool RectsIntersect(SKRect a, SKRect b)
            {
                return !(a.Left >= b.Right || a.Right <= b.Left || a.Top >= b.Bottom || a.Bottom <= b.Top);
            }


        }






        private static readonly Dictionary<string, LockedImage> _profileBackgroundCache = new();

        private static LockedImage? GetProfileBackgroundImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (_profileBackgroundCache.TryGetValue(path, out var existing))
            {
                // If we already loaded it and it is marked as Loaded, just reuse
                if (existing.Loaded)
                    return existing;

                // Otherwise, fall through and try to recreate
            }

            try
            {
                var img = Cache.GetLocalImageFromPath(path, initialiseIfMissing: true, imageDisplayItem: null);

                _profileBackgroundCache[path] = img;

                Debug.WriteLine($"[PROFILE-BG] Loaded background image for path: {path}");
                return img;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE-BG] Failed to load background image '{path}': {ex}");
                return null;
            }
        }





    }
}
