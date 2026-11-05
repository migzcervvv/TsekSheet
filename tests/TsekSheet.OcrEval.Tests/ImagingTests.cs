using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCvSharp;
using TsekSheet.OcrEval;

namespace TsekSheet.OcrEval.Tests;

[TestClass]
public sealed class ImagingTests
{
    [TestMethod]
    [DataRow(3d)] [DataRow(-3d)] [DataRow(8d)] [DataRow(-8d)]
    public void DeskewCorrectsReceiptRuleLinesInBothDirections(double angle)
    {
        using var image = new Mat(800, 600, MatType.CV_8UC1, Scalar.White);
        foreach (var row in new[] { 100, 300, 500, 700 }) Cv2.Line(image, new(50, row), new(550, row), Scalar.Black, 2);
        using var skewed = Imaging.Rotate(image, angle);
        Assert.AreEqual(-angle, Imaging.Skew(skewed), .6);
        using var fixedImage = Cv2.ImDecode(Imaging.Prepare(skewed.ToBytes(".png"), "deskew-adaptive"), ImreadModes.Grayscale);
        Assert.AreEqual(0, Imaging.Skew(fixedImage), .6);
    }
}
