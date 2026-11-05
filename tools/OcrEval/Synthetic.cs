using System.Globalization;
using OpenCvSharp;

namespace TsekSheet.OcrEval;

public static class Synthetic
{
    public static void Generate(string directory, int count)
    {
        if (count is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(count));
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new IOException("Generation requires an empty destination; existing inputs are never overwritten.");
        Directory.CreateDirectory(directory);
        using var truth = new StreamWriter(new FileStream(Path.Combine(directory, "truth.csv"), FileMode.CreateNew));
        using var vendors = new StreamWriter(new FileStream(Path.Combine(directory, "vendors.csv"), FileMode.CreateNew));
        truth.WriteLine("file,vendor,tin,invoice,date,amount"); vendors.WriteLine("name,tin");
        var random = new Random(20261105);
        for (var i = 1; i <= count; i++)
        {
            var vendor = $"SYNTHETIC BAYAN STORE {i:00}";
            var tin = $"{100 + i % 800:000}-000-{i % 1000:000}-00000";
            var invoice = $"TS-{i:00000}"; var date = new DateTime(2026, 11, 1).AddDays(i % 25);
            var amount = 150m + i * 37.25m; var sales = decimal.Round(amount / 1.12m, 2);
            using var image = new Mat(1320, 1000, MatType.CV_8UC3, Scalar.White);
            void Text(string text, int y, double size = 1.2, int thickness = 2) => Cv2.PutText(image, text, new(60, y), HersheyFonts.HersheySimplex, size, Scalar.Black, thickness, LineTypes.AntiAlias);
            Text(vendor, 85, 1.4, 3); Text("123 DEMO ST, SAMPLE CITY", 145); Text("FAKE DATA - OCR EVALUATION ONLY", 200, .95);
            Text("VAT REGISTERED TIN " + tin, 265, 1.05);
            Cv2.Line(image, new(60, 300), new(940, 300), Scalar.Black, 2);
            Text((i % 2 == 0 ? "SI# " : "OR# ") + invoice, 360); Text("DATE: " + date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture), 420);
            Text("QTY  DESCRIPTION           AMOUNT", 500, 1.05);
            Text(" 2   SAMPLE GOODS           100.00", 560, 1.05); Text(" 1   DEMO SUPPLIES          150.00", 620, 1.05);
            Cv2.Line(image, new(60, 670), new(940, 670), Scalar.Black, 2);
            Text($"VATable sales     PHP {sales:0.00}", 745); Text($"VAT 12%           PHP {amount - sales:0.00}", 805);
            Text($"TOTAL             PHP {amount:0.00}", 895, 1.35, 3);
            Text("CASH PAYMENT", 980); Text("THANK YOU - KEEP THIS RECEIPT", 1060, 1.05);
            Text("NOT VALID AS A REAL TAX INVOICE", 1160, .95);
            foreach (var variant in new[] { "clean", "blur", "noise", "skew", "jpeg20", "lowcontrast" })
            {
                var file = $"receipt-{i:0000}__{variant}" + (variant == "jpeg20" ? ".jpg" : ".png");
                using var output = image.Clone();
                if (variant == "blur") Cv2.GaussianBlur(image, output, new(5, 5), 1.4);
                if (variant == "noise")
                {
                    Cv2.SetTheRNG((ulong)(20261105 + i));
                    using var noise = new Mat(image.Size(), MatType.CV_16SC3); Cv2.Randn(noise, Scalar.All(0), Scalar.All(18));
                    using var wide = new Mat(); image.ConvertTo(wide, MatType.CV_16SC3); Cv2.Add(wide, noise, wide); wide.ConvertTo(output, MatType.CV_8UC3);
                }
                if (variant == "lowcontrast") image.ConvertTo(output, MatType.CV_8UC3, .25, 180);
                byte[] bytes;
                if (variant == "skew")
                {
                    using var skewed = Imaging.Rotate(image, (i % 2 == 0 ? -1 : 1) * (3 + random.NextDouble() * 5));
                    bytes = skewed.ToBytes(".png");
                }
                else bytes = output.ToBytes(variant == "jpeg20" ? ".jpg" : ".png", variant == "jpeg20" ? [new ImageEncodingParam(ImwriteFlags.JpegQuality, 20)] : []);
                using (var stream = new FileStream(Path.Combine(directory, file), FileMode.CreateNew)) stream.Write(bytes);
                truth.WriteLine(Csv.Line([file, vendor, tin, invoice, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), amount.ToString("0.00", CultureInfo.InvariantCulture)]));
            }
            vendors.WriteLine(Csv.Line([vendor, tin]));
        }
    }
}
