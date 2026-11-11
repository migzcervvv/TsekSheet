using TsekSheet.Application.Verification;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Excel;

internal static class LegacyReceiptDestination
{
    internal static ExcelSectionDefinition Resolve(WorkflowProfile profile, TypedReviewRecord record, string sectionId, string? sheetName)
    {
        var section = profile.Layout is { } layout
            ? layout.Section(sectionId, sheetName ?? throw new FieldReviewException("SHEET_REQUIRED", "Choose a matching sheet."))
            : profile.ExcelSections?.SingleOrDefault(s => s.Id == sectionId) ?? throw new FieldReviewException("UNKNOWN_SECTION", "Choose a section from this receipt profile.");
        if (sheetName is not null && section.SheetName != sheetName)
            throw new FieldReviewException("UNKNOWN_SECTION", "Choose a section on the selected sheet.");
        if (profile.Layout is not { MappingMode: "account" } accountLayout) return section;
        var choice = record.Fields.Single(f => f.Kind == "accountChoice").LiteralText;
        var account = accountLayout.Accounts.SingleOrDefault(a => a.Column == choice)
            ?? throw new FieldReviewException("INVALID_ACCOUNT", "Choose an account from this profile.");
        var amount = accountLayout.Fields.Single(f => f.WritesTo == "selectedAccountColumn");
        return section with { ColumnMap = section.ColumnMap.Append(new KeyValuePair<string, string>(amount.Key, account.Column)).ToDictionary() };
    }
}
