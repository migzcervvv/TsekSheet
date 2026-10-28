# ADR-0001: Desktop Framework and Processing Stack

Status: Accepted

Date: 2026-10-28

## Context

[AGENTS.md](../../../AGENTS.md), [GOALS.md](../../../GOALS.md), and the [architecture overview](../overview.md) establish TsekSheet as an offline-first Windows desktop application. It must process scanned receipts and PDFs locally, require human verification of extracted information, and support safe editing of existing Excel workbooks.

Workbook operations include selected-sheet workflows, independent working copies, merge, replacement, new-sheet creation, and template-driven row insertion. Formulas, formatting, references, and workbook integrity must be preserved. Original workbooks remain untouched until an explicit authorized save. The first use case is accounting reimbursement, but the architecture must support generic, configurable spreadsheet workflows.

The product requires a desktop shell, a React + TypeScript interface, a testable workbook-processing core, and local OCR. It must not introduce a database, cloud services, cloud processing, or paid AI APIs. Non-Windows applications are outside MVP scope. Framework selection must favor these requirements and simple component boundaries over speculative portability.

## Decision

Use the following stack:

| Technology | Responsibility |
| --- | --- |
| WPF | Windows desktop host: application lifecycle, windows, native file dialogs, composition, and shutdown. |
| WebView2 | Host locally bundled React + TypeScript assets and carry validated messages between the interface and C#. |
| React + TypeScript | Workbook navigation and editing interactions, document review, OCR verification, integration previews, and save status. |
| C#/.NET | Application orchestration, domain rules, in-memory workbook sessions, workbook processing, generic templates, validation, and explicit saving. |
| Python | Offline PDF/image processing and OCR, invoked through a supervised local child process. |

Keep WPF, WebView2, and C# application services in one desktop process. Run Python separately for OCR. Package the frontend, Python runtime, OCR models, and required assets for local operation. The host process boundary does not imply a single operating-system process for the embedded browser runtime.

Use a versioned JSON message contract over the WebView2 web-message channel. Validate origin, command names, payloads, request IDs, and session revisions. C# remains authoritative for workbook state and verification rules. Do not expose unrestricted .NET objects or arbitrary filesystem access to React. No local HTTP server is required.

Use a versioned structured request/result contract for OCR. Python receives controlled local inputs and returns extraction candidates with source associations; it does not edit workbooks. The application requires human verification before applying extracted values.

This ADR accepts the framework and responsibility boundaries. It does not select a workbook library, OCR engine, framework version, installer technology, or formula-calculation engine.

## Alternatives Considered

These alternatives were considered against the project's required stack and boundaries. This decision does not claim benchmark results or a completed prototype comparison.

| Alternative | Fit | Reason not selected |
| --- | --- | --- |
| Tauri | Can support a web interface in a desktop application. | Its Rust host would add a host-language boundary around the required C# application and workbook services. The project has no MVP requirement that justifies that integration work. |
| Electron | Can host React + TypeScript in a desktop shell. | It would introduce a Node.js desktop host and a separate integration boundary for C# workbook processing, alongside Python OCR. WPF keeps host composition and application services together in .NET. |
| Avalonia | Offers a .NET desktop UI approach with broader platform ambitions. | Cross-platform delivery is outside MVP scope. Selecting it would still require an embedded-web-host strategy for the required React interface without resolving a current product need. |

## Reasons for Choosing WPF + WebView2

- The decision directly implements the Windows and technology constraints already established in the authoritative project documents.
- WPF places native window management, dialogs, lifecycle, and C# service composition in the same .NET host, keeping the path from user commands to application services small.
- WebView2 provides the boundary for the required React + TypeScript interface while allowing assets to be delivered locally. Core workflows need no hosted frontend or network service.
- C# owns workbook operations and save authorization behind a typed contract. React can focus on interaction and presentation without becoming a second workbook-processing implementation.
- Python OCR stays behind a process adapter with explicit inputs, outputs, cancellation, and failure handling. Recognition failures do not need to compromise workbook session state.
- Business rules can be exercised without starting WPF, WebView2, or Python. The desktop shell remains a replaceable adapter, with narrow interfaces only where they improve testing.

## Trade-offs and Consequences

### Accepted Costs

- The desktop host targets Windows. A later platform expansion would require a new host decision and integration work even if core services remain reusable.
- The product ships and maintains .NET, web frontend, and Python assets. Packaging must account for WebView2 runtime availability and provide an offline installation path for required dependencies; it must not assume a first-run internet download.
- The web-message bridge is an application boundary requiring schema validation, error reporting, revision checks, and compatibility tests. Large workbooks must use viewport-sized payloads rather than full-workbook transfers.
- Workbook work must run away from the WPF UI thread, with mutations serialized per session. UI dispatch and cancellation add coordination that must remain outside domain rules.
- OCR requires child-process supervision, timeouts, structured output validation, and cleanup of local job files. Process separation alone is not a security sandbox.
- Embedded web content requires controlled navigation and local origins. Extracted document text must be treated as untrusted text, not executable interface content.

### Product and Integrity Obligations

The framework does not guarantee Excel compatibility or correct OCR. Workbook-library selection remains subject to fixture-based preservation tests. Unsupported operations must be rejected rather than silently degrading workbook content.

Keep originals unchanged during opening, editing, extraction, and integration. Apply structural changes, including repeated template-driven row insertion, to isolated candidate workbooks and validate them before adoption. Save only through the explicit application save path, with candidate serialization, validation, external-change checks, and a safe file commit.

Templates must remain generic and reusable across worksheets. Region boundaries, summary positions, formula propagation, and row insertion rules belong to template configuration and workbook processing, not WPF event handlers or hardcoded worksheet names.

### Validation Required During Implementation

Test domain and application behavior independently of the desktop host. Use real workbook fixtures for working-copy isolation, merge, replacement, new sheets, formula and formatting preservation, repeated row insertion, and explicit saving. Add focused bridge and OCR contract tests, plus a small desktop integration suite. Report failed or unexecuted tests explicitly.

Runtime distribution, workbook-library compatibility, formula recalculation, OCR tooling and licensing, and installer behavior remain implementation decisions. This accepted ADR does not imply those checks have passed.

## Keeping Business Logic Independent of WPF

1. Keep domain types in plain .NET code with no references to WPF, WebView2, React, Python runtime objects, or workbook-library objects. Domain types describe identities, revisions, verification state, changes, and validation outcomes.
2. Put use cases in the application layer: open sessions, edit cells, verify fields, map templates, preview and apply integrations, insert rows, and save. Accept plain request types and return plain results, errors, and progress information.
3. Keep workbook-library details in the workbook engine and process-launch details in the OCR adapter. Implement narrow application-owned contracts for workbook access, OCR execution, and file commit where substitution enables meaningful tests.
4. Restrict WPF code to composition and desktop concerns. The host translates native file selections into application requests and routes bridge messages; it does not implement formulas, template rules, merge policies, verification gates, or save integrity rules.
5. Keep UI dispatch in host adapters. Application and domain code must not require a WPF dispatcher, window, dialog, or WebView2 control. Pass cancellation and progress through ordinary .NET mechanisms.
6. Keep React responsible for presentation state and user intent. Revalidate commands in C# so correctness does not depend on interface controls being disabled or inputs having passed frontend validation.
7. Test core services directly without a desktop window. Enforce dependency direction through project references and review; avoid a service locator, generic repository layer, or plugin framework merely to isolate the host.

Replacing the shell later should require new composition, dialog, and transport adapters while retaining the tested application rules and workbook engine. That is a dependency goal, not a commitment to build another host in the MVP.
