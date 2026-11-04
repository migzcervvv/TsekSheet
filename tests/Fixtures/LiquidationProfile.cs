using System.Text.Json;
using TsekSheet.Application.Workflows;

namespace TsekSheet.TestFixtures;

// Synthetic layout reproduces the planner's column/marker shape; no owner data.
internal static class LiquidationProfile
{
    internal static string Json => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, id = "synthetic-liquidation", revision = 1, displayName = "Synthetic liquidation",
        sheetMatch = new { headerRow = 2, anchors = new[] { Anchor("C", "PARTICULARS"), Anchor("D", "VENDOR"), Anchor("E", "TIN"), Anchor("G", "INVOICE") } },
        fields = new object[]
        {
            Field("itemNo", "Item no.", "B", true), Field("particulars", "Particulars", "C", true),
            Field("vendor", "Vendor", "D", false), Field("tin", "TIN", "E", false), Field("address", "Address", "F", false),
            Field("invoice", "Invoice (SI#/OR#)", "G", false), Field("cvNo", "CV#", "H", false),
            new { key = "account", label = "Account", kind = "accountChoice", required = true },
            new { key = "amount", label = "Amount", kind = "money", required = true, writesTo = "selectedAccountColumn" }
        },
        accounts = Columns.Select((c, i) => new { code = "FAKE-" + (i % 4), label = "Synthetic account " + i, column = c, vatable = i < 4 }).ToArray(),
        vatRule = new { note = "Data only; D12 requires amounts as entered.", convertFromInclusive = "OWNER_DECISION_PENDING" },
        formulaColumns = new[] { "N", "O", "AF" },
        sections = new object[]
        {
            new { id = "expenses", label = "Expenses", start = new { headerRowOffset = 2 }, end = new { rowBefore = Marker("TOTAL", 1) }, subtotalRow = Marker("TOTAL", 1), insertMode = "entireRow", fillBlankRowsFirst = true },
            new { id = "credits", label = "Credits", start = new { afterRow = Marker("TOTAL", 1), offset = 3 }, end = new { rowBefore = Marker("TOTAL credit", 1) }, subtotalRow = Marker("TOTAL credit", 1), insertMode = "entireRow", fillBlankRowsFirst = true }
        },
        blankRowRule = ExcelLayoutProfile.SupportedBlankRule,
        protectedRows = new[] { Marker("TOTAL", 2) },
        lookups = new[] { new { id = "vendorMaster", sheet = "VENDOR", keyColumns = new { name = "A", tin = "B", address = "C" }, firstDataRow = 2, status = "planned (T2d)" } }
    });
    internal static readonly string[] Columns = ["J", "K", "L", "M", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "AA", "AB", "AC", "AD", "AE"];
    internal static WorkflowProfile Create() => WorkflowProfiles.Parse(Json);
    private static object Anchor(string column, string text) => new Dictionary<string, object> { ["column"] = column, ["equals"] = text };
    private static object Marker(string text, int occurrence) => new Dictionary<string, object> { ["column"] = "C", ["equals"] = text, ["occurrence"] = occurrence };
    private static object Field(string key, string label, string column, bool required) => new { key, label, column, kind = "text", required };
}
