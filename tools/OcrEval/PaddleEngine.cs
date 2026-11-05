using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace TsekSheet.OcrEval;

// Direct DB detection + CTC recognition; no third-party OCR wrapper or model downloads.
internal sealed class PaddleEngine : IOcrEngine
{
    private readonly EvalConfig config;
    private readonly InferenceSession detector, recognizer;
    private readonly string[] dictionary;
    public PaddleEngine(EvalConfig config)
    {
        this.config = config;
        OrtEnv.Instance().DisableTelemetryEvents();
        using var options = new SessionOptions { IntraOpNumThreads = config.Threads, InterOpNumThreads = 1, ExecutionMode = ExecutionMode.ORT_SEQUENTIAL, GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL, LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR };
        detector = new(config.DetectionModel, options);
        try { recognizer = new(config.RecognitionModel, options); }
        catch { detector.Dispose(); throw; }
        dictionary = new[] { "" }.Concat(File.ReadAllLines(config.Dictionary)).Append(" ").ToArray();
    }
    public Task<OcrResult> RecognizeAsync(byte[] png, CancellationToken token)
    {
        using var image = Cv2.ImDecode(png, ImreadModes.Color);
        var ratio = Math.Min(1, (double)config.DetectionMaxSide / Math.Max(image.Width, image.Height));
        var width = Math.Max(32, (int)Math.Round(image.Width * ratio / 32) * 32);
        var height = Math.Max(32, (int)Math.Round(image.Height * ratio / 32) * 32);
        using var resized = new Mat(); Cv2.Resize(image, resized, new(width, height));
        var tensor = Tensor(resized, width, height, detection: true);
        using var prediction = detector.Run([NamedOnnxValue.CreateFromTensor(detector.InputMetadata.Keys.Single(), tensor)]);
        var values = prediction.First().AsTensor<float>();
        var mapHeight = values.Dimensions[^2]; var mapWidth = values.Dimensions[^1];
        using var probability = new Mat(mapHeight, mapWidth, MatType.CV_32FC1);
        System.Runtime.InteropServices.Marshal.Copy(values.ToArray(), 0, probability.Data, mapHeight * mapWidth);
        using var binary = new Mat(); Cv2.Threshold(probability, binary, config.DetectionThreshold, 255, ThresholdTypes.Binary); binary.ConvertTo(binary, MatType.CV_8UC1);
        Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
        var lines = new List<(Point2f[] Quad, string Text, double Confidence, List<(string Text, double Confidence, double Start, double End)> Words)>();
        foreach (var contour in contours.Take(1000))
        {
            token.ThrowIfCancellationRequested();
            if (contour.Length < 3) continue;
            var box = Cv2.MinAreaRect(contour); if (Math.Min(box.Size.Width, box.Size.Height) < 3) continue;
            using var mask = new Mat(probability.Size(), MatType.CV_8UC1, Scalar.Black);
            Cv2.FillPoly(mask, [contour], Scalar.White);
            if (Cv2.Mean(probability, mask).Val0 < config.BoxThreshold) continue;
            // DB's unclip distance A*r/P. For a rectangular minimum box, the
            // minimum box of its round polygon offset expands each side by d.
            var distance = box.Size.Width * box.Size.Height * config.UnclipRatio / (2 * (box.Size.Width + box.Size.Height));
            box.Size = new(box.Size.Width + 2 * distance, box.Size.Height + 2 * distance);
            var quad = Order(box.Points().Select(p => new Point2f(Math.Clamp(p.X * image.Width / mapWidth, 0, image.Width - 1), Math.Clamp(p.Y * image.Height / mapHeight, 0, image.Height - 1))).ToArray());
            using var crop = Crop(image, quad);
            var (text, confidence, words) = Recognize(crop);
            if (text.Length > 0) lines.Add((quad, text, confidence, words));
        }
        var rows = new List<List<int>>();
        foreach (var i in Enumerable.Range(0, lines.Count).OrderBy(i => lines[i].Quad.Average(p => p.Y)))
        {
            var center = lines[i].Quad.Average(p => p.Y);
            var h = Math.Max(8, Length(lines[i].Quad[0], lines[i].Quad[3]));
            var row = rows.FirstOrDefault(r => Math.Abs(lines[r[0]].Quad.Average(p => p.Y) - center) <= h * .55);
            if (row is null) rows.Add(row = []); row.Add(i);
        }
        var textLines = new List<string>(); var outputWords = new List<WordBox>();
        foreach (var row in rows)
        {
            var ordered = row.OrderBy(i => lines[i].Quad.Min(p => p.X)).ToArray();
            textLines.Add(string.Join(' ', ordered.Select(i => lines[i].Text)));
            foreach (var i in ordered)
            {
                var line = lines[i];
                Point2f Along(Point2f a, Point2f b, double amount) => new((float)(a.X + (b.X - a.X) * amount), (float)(a.Y + (b.Y - a.Y) * amount));
                foreach (var word in line.Words)
                {
                    var corners = new[] { Along(line.Quad[0], line.Quad[1], word.Start), Along(line.Quad[0], line.Quad[1], word.End), Along(line.Quad[3], line.Quad[2], word.Start), Along(line.Quad[3], line.Quad[2], word.End) };
                    var left = corners.Min(p => p.X); var top = corners.Min(p => p.Y);
                    outputWords.Add(new(word.Text, word.Confidence, left, top, corners.Max(p => p.X) - left, corners.Max(p => p.Y) - top));
                }
            }
        }
        return Task.FromResult(new OcrResult(string.Join('\n', textLines), outputWords));
    }
    private (string Text, double Confidence, List<(string Text, double Confidence, double Start, double End)> Words) Recognize(Mat crop)
    {
        const int height = 48;
        var actualWidth = Math.Clamp((int)Math.Ceiling(height * (double)crop.Width / crop.Height), 1, 2048);
        var width = Math.Max(320, (actualWidth + 31) / 32 * 32);
        using var resized = new Mat(); Cv2.Resize(crop, resized, new(actualWidth, height));
        var tensor = Tensor(resized, width, height, detection: false);
        using var result = recognizer.Run([NamedOnnxValue.CreateFromTensor(recognizer.InputMetadata.Keys.Single(), tensor)]);
        var output = result.First().AsTensor<float>(); var steps = output.Dimensions[^2]; var classes = output.Dimensions[^1];
        if (classes != dictionary.Length) throw new InvalidDataException($"Recognizer dictionary mismatch: {dictionary.Length} versus {classes} classes.");
        var probabilities = output.ToArray();
        var decoded = new List<(string Text, float Confidence, int Time)>(); var previous = -1;
        for (var t = 0; t < steps; t++)
        {
            var offset = t * classes; var index = 0; var confidence = probabilities[offset];
            for (var c = 1; c < classes; c++) if (probabilities[offset + c] > confidence) { confidence = probabilities[offset + c]; index = c; }
            if (index != 0 && index != previous) decoded.Add((dictionary[index], confidence, t));
            previous = index;
        }
        var words = new List<(string, double, double, double)>(); var part = new List<(string Text, float Confidence, int Time)>();
        void Flush()
        {
            if (part.Count == 0) return;
            var start = Math.Clamp((double)part[0].Time / steps * width / actualWidth, 0, 1);
            var end = Math.Clamp((double)(part[^1].Time + 1) / steps * width / actualWidth, 0, 1);
            words.Add((string.Concat(part.Select(p => p.Text)), part.Average(p => p.Confidence), start, Math.Max(start, end))); part.Clear();
        }
        foreach (var character in decoded) { if (string.IsNullOrWhiteSpace(character.Text)) Flush(); else part.Add(character); } Flush();
        return (string.Concat(decoded.Select(p => p.Text)).Trim(), decoded.Count == 0 ? 0 : decoded.Average(p => p.Confidence), words);
    }
    private static unsafe DenseTensor<float> Tensor(Mat image, int width, int height, bool detection)
    {
        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width }); var data = tensor.Buffer.Span;
        var mean = new[] { .485f, .456f, .406f }; var std = new[] { .229f, .224f, .225f };
        for (var y = 0; y < height; y++)
        {
            var row = (byte*)image.Ptr(y);
            for (var x = 0; x < width; x++) for (var c = 0; c < 3; c++)
            {
                var value = x < image.Width ? row[x * 3 + c] / 255f : .5f;
                data[c * height * width + y * width + x] = detection ? (value - mean[c]) / std[c] : (value - .5f) / .5f;
            }
        }
        return tensor;
    }
    private static Point2f[] Order(Point2f[] points)
    {
        var x = points.OrderBy(p => p.X).ToArray(); var left = x.Take(2).OrderBy(p => p.Y).ToArray(); var right = x.Skip(2).OrderBy(p => p.Y).ToArray();
        return [left[0], right[0], right[1], left[1]];
    }
    private static double Length(Point2f a, Point2f b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static Mat Crop(Mat source, Point2f[] points)
    {
        var width = Math.Max(1, (int)Math.Max(Length(points[0], points[1]), Length(points[2], points[3])));
        var height = Math.Max(1, (int)Math.Max(Length(points[0], points[3]), Length(points[1], points[2])));
        using var transform = Cv2.GetPerspectiveTransform(points, [new(0, 0), new(width - 1, 0), new(width - 1, height - 1), new(0, height - 1)]);
        var crop = new Mat(); Cv2.WarpPerspective(source, crop, transform, new(width, height), InterpolationFlags.Cubic, BorderTypes.Replicate);
        if ((double)height / width >= 1.5) Cv2.Rotate(crop, crop, RotateFlags.Rotate90Clockwise);
        return crop;
    }
    public void Dispose() { recognizer.Dispose(); detector.Dispose(); }
}
