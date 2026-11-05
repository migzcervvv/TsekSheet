using OpenCvSharp;

namespace TsekSheet.OcrEval;

public static class Imaging
{
    public static readonly string[] Profiles = ["none", "deskew-adaptive", "deskew-denoise-clahe"];
    public static Mat Rotate(Mat source, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        var width = (int)Math.Ceiling(source.Width * Math.Abs(Math.Cos(radians)) + source.Height * Math.Abs(Math.Sin(radians))) + 24;
        var height = (int)Math.Ceiling(source.Height * Math.Abs(Math.Cos(radians)) + source.Width * Math.Abs(Math.Sin(radians))) + 24;
        using var matrix = Cv2.GetRotationMatrix2D(new Point2f(source.Width / 2f, source.Height / 2f), degrees, 1);
        matrix.Set(0, 2, matrix.At<double>(0, 2) + (width - source.Width) / 2d);
        matrix.Set(1, 2, matrix.At<double>(1, 2) + (height - source.Height) / 2d);
        var result = new Mat();
        Cv2.WarpAffine(source, result, matrix, new(width, height), InterpolationFlags.Cubic, BorderTypes.Constant, Scalar.White);
        return result;
    }
    public static double Skew(Mat gray)
    {
        using var edges = new Mat(); Cv2.Canny(gray, edges, 40, 120);
        var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 720, 60, gray.Width / 8d, 15);
        var angles = lines.Select(l => (Angle: Math.Atan2(l.P2.Y - l.P1.Y, l.P2.X - l.P1.X) * 180 / Math.PI,
            Weight: Math.Sqrt(Math.Pow(l.P2.X - l.P1.X, 2) + Math.Pow(l.P2.Y - l.P1.Y, 2))))
            .Where(l => Math.Abs(l.Angle) <= 12).OrderBy(l => l.Angle).ToArray();
        var half = angles.Sum(l => l.Weight) / 2; double sum = 0;
        foreach (var line in angles) { sum += line.Weight; if (sum >= half) return line.Angle; }
        return 0;
    }
    public static byte[] Prepare(byte[] encoded, string profile)
    {
        using var source = Cv2.ImDecode(encoded, ImreadModes.Color);
        if (source.Empty() || source.Width > 16384 || source.Height > 16384 || source.Total() > 64_000_000)
            throw new InvalidDataException("Image exceeds evaluation bounds.");
        if (profile == "none") return source.ToBytes(".png");
        if (!Profiles.Contains(profile)) throw new ArgumentException("Unknown preprocessing profile.");
        using var gray = new Mat(); Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        // Image-space slopes have the opposite sign to the OpenCV rotation angle.
        using var corrected = Rotate(gray, Skew(gray));
        using var output = new Mat();
        if (profile == "deskew-adaptive") Cv2.AdaptiveThreshold(corrected, output, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 41, 15);
        else
        {
            using var denoised = new Mat(); Cv2.FastNlMeansDenoising(corrected, denoised, 10, 7, 21);
            using var clahe = Cv2.CreateCLAHE(2, new(8, 8)); clahe.Apply(denoised, output);
        }
        return output.ToBytes(".png");
    }
}
