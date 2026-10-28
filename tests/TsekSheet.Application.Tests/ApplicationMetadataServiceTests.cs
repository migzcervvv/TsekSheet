using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Foundation;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class ApplicationMetadataServiceTests
{
    [TestMethod]
    public void ApplicationName_ReturnsProductName()
    {
        var service = new ApplicationMetadataService();

        Assert.AreEqual("TsekSheet", service.ApplicationName);
    }
}
