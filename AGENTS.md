# TsekSheet

TsekSheet is a local-first Windows desktop application using WPF, WebView2, React, TypeScript, C#, and Python for offline OCR.

## Development Rules

1. Follow a modular architecture with clear separation of responsibilities.
2. Keep business logic independent of WPF and WebView2.
3. Do not introduce databases, cloud services, or paid AI APIs.
4. Never modify original Excel files without explicit user approval.
5. Preserve workbook formulas, formatting, and worksheet integrity.
6. Write tests for workbook manipulation.
7. Create an implementation plan before making major architectural changes.
8. Keep implementations simple, maintainable, and testable.
9. Clearly report any failed or unexecuted tests.
