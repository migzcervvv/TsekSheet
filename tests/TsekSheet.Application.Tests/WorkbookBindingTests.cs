using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Excel;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class WorkbookBindingTests
{
    [TestMethod]
    public void RfpDocumentTypeDefinesManualVerificationAndPacketRolesWithoutExcel()
    {
        var type = DocumentTypes.Rfp;
        Assert.AreEqual(new DocumentTypeReference("rfp", 1), type.Identity);
        Assert.IsTrue(type.OneRecordPerPacket);
        CollectionAssert.AreEqual(new[] { "rfp-form", "invoice", "supporting-document" }, type.PageRoles.ToArray());
        CollectionAssert.AreEquivalent(new[] { "payee", "rfpNo", "amount" }, type.Fields.Where(f => f.Required).Select(f => f.Key).ToArray());
        Assert.AreEqual("money", type.Fields.Single(f => f.Key == "vat").ValueType);
        Assert.IsFalse(type.Fields.Single(f => f.Key == "vat").Required);
        var legacy = WorkflowProfiles.BuiltInProfiles.Single(p => p.Id == "payables-rfp");
        CollectionAssert.AreEqual(legacy.Fields.ToArray(), DocumentTypes.ReviewFields(legacy));
    }

    [TestMethod]
    public void PayablesBindsTwentyTwoTargetsAndDestinationRequirementsIndependently()
    {
        var json = ParseBuiltIn();
        var target = FirstTarget(json);
        target["requiredFields"]!.AsArray().Add("invoice");
        var binding = WorkbookBindings.Parse(json.ToJsonString(), DocumentTypes.Rfp);
        Assert.AreEqual(1, binding.SchemaVersion);
        Assert.AreEqual(DocumentTypes.Rfp.Identity, binding.DocumentType);
        Assert.AreEqual(22, binding.Targets.Count);
        Assert.AreEqual(14, binding.Targets.Count(t => t.Id.StartsWith("member-company-", StringComparison.Ordinal)));
        var legacy = WorkflowProfiles.BuiltInProfiles.Single(p => p.Id == "payables-rfp").Layout!;
        foreach (var destination in binding.Targets)
        {
            var section = legacy.Section(destination.Id, destination.Sheet);
            CollectionAssert.AreEquivalent(section.ColumnMap.ToArray(), destination.ColumnMap.ToArray());
            CollectionAssert.AreEqual(legacy.SheetMatch.Anchors.ToArray(), destination.HeaderSignature!.Anchors.ToArray());
            Assert.AreEqual(legacy.SheetMatch.HeaderRow, destination.HeaderSignature.HeaderRow);
        }
        Assert.IsTrue(binding.Targets[0].RequiredFields.Contains("invoice"));
        Assert.IsFalse(DocumentTypes.Rfp.Fields.Single(f => f.Key == "invoice").Required);
    }

    [TestMethod]
    [DataRow("field", "Unknown document field")]
    [DataRow("required", "Unknown document field")]
    [DataRow("column", "valid and distinct")]
    [DataRow("duplicate-column", "valid and distinct")]
    [DataRow("duplicate-target", "unique target IDs")]
    [DataRow("reference", "unique Excel table or defined name")]
    [DataRow("schema-version", "Unsupported binding schemaVersion")]
    [DataRow("document-revision", "installed document type and revision")]
    [DataRow("insertMode", "insertMode")]
    [DataRow("hybridNewRowMode", "hybridNewRowMode")]
    [DataRow("formulaCopy", "formulaCopy")]
    [DataRow("condition", "condition")]
    public void MalformedOrObsoleteBindingDataIsRejected(string fault, string message)
    {
        var json = ParseBuiltIn();
        var target = FirstTarget(json);
        var columns = target["columnMap"]!.AsObject();
        switch (fault)
        {
            case "field": columns.Add("unrecognized", "P"); break;
            case "required": target["requiredFields"]!.AsArray().Add("unrecognized"); break;
            case "column": columns["amount"] = "XFE"; break;
            case "duplicate-column": columns["amount"] = "E"; break;
            case "duplicate-target": json["targets"]![1]!["id"] = "employee"; break;
            case "reference": target["reference"] = "=OFFSET(A1,0,0)"; break;
            case "schema-version": json["schemaVersion"] = 2; break;
            case "document-revision": json["documentType"]!["revision"] = 2; break;
            default: target.Add(fault, "forbidden"); break;
        }
        var error = Assert.ThrowsExactly<WorkbookException>(() => WorkbookBindings.Parse(json.ToJsonString(), DocumentTypes.Rfp));
        Assert.AreEqual("INVALID_BINDING", error.Code);
        StringAssert.Contains(error.Message, message);
    }

    private static JsonObject ParseBuiltIn() => JsonNode.Parse(WorkbookBindings.Configuration(WorkbookBindings.Payables))!.AsObject();
    private static JsonObject FirstTarget(JsonObject json) => json["targets"]![0]!.AsObject();
}
