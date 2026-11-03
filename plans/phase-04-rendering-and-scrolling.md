# Scroll accessibility and local rendering

Preserve the accepted workbook/project layout. First remove overflow clipping:
main remains the task scroll owner; the bounded worksheet grid keeps its independent
scrolling. Expanded reports/editors participate in canvas flow. Headers/footer stay
in normal shell flow. Use CSS and native keyboard scrolling, not wheel handlers.
Verify last actions at all requested sizes and 1280×600/125% content zoom in WPF.

Then implement a bounded renderer behind the registered-source boundary. Evaluate
Windows.Data.Pdf and Windows imaging APIs, supplied/serviced by Windows, in a separate
local worker process. No third-party PDF runtime or paid service is needed if the
installed SDK/runtime works. Application contracts and process supervision stay
independent of WPF. The worker receives only freshly checked bytes over pipes,
never an original path. Return a capped PNG page and parsed page count. Enforce
file/page/dimension/output bounds, timeout, cancellation and stale-source checks
before and after decode; terminate failed/cancelled workers. No original writes,
temporary document files or browser filesystem access. Display bytes through a
bounded path-free bridge result and source inspector; require fresh render requests
for pages, without changing field verification or registered source identity.

Test genuine generated PDF/PNG/JPEG fixtures, malformed/encrypted/oversized inputs,
page navigation, cancellation/timeouts/source changes and unchanged hashes. Audit
Windows SDK/runtime redistribution and offline packaging. Gate genuine OCR behind
the verified renderer and a separate supervised extraction/verification plan.
No receipt insertion/export, workbook mutation, commits or deployment.
