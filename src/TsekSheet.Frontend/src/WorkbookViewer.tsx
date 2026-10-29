import { useEffect, useState } from 'react';
import { BridgeError } from './bridge/client';
import PayablesProcessing from './PayablesProcessing';
import type { BridgeClient, Workbook, Viewport, WorkingCopy, Cell, MergePayload, MergePreview } from './bridge/client';

const columnLabel = (column: number): string => { let label = ''; for (let n = column; n > 0; n = Math.floor((n - 1) / 26)) label = String.fromCharCode(65 + (n - 1) % 26) + label; return label; };

export default function WorkbookViewer({ bridge, available }: { bridge: BridgeClient | null; available: boolean }) {
  const [book, setBook] = useState<Workbook | null>(null);
  const [sheetId, setSheetId] = useState('');
  const [row, setRow] = useState(1); const [column, setColumn] = useState(1);
  const [view, setView] = useState<Viewport | null>(null);
  const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(false);
  const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  const [copies, setCopies] = useState<WorkingCopy[]>([]); const [viewId, setViewId] = useState('original');
  const [originalSheet, setOriginalSheet] = useState(''); const [selected, setSelected] = useState<Cell | null>(null);
  const [editKind, setEditKind] = useState<'text' | 'number' | 'date' | 'formula'>('text'); const [draft, setDraft] = useState('');
  const [confirm, setConfirm] = useState<'close' | 'open' | null>(null);
  const [uncertain, setUncertain] = useState(false);
  const [merge, setMerge] = useState<{ payload: MergePayload; preview: MergePreview } | null>(null);
  const enabled = available && bridge !== null;
  const sheet = book?.worksheets.find(s => s.id === sheetId);
  const copy = copies.find(c => c.sessionId === viewId);
  const activeId = copy?.sessionId ?? book?.sessionId;
  const activeRevision = copy?.revision ?? book?.revision;
  const pending = uncertain || copies.some(c => c.isDirty) || book?.isDirty === true;
  useEffect(() => { setMerge(null); }, [bridge, book?.revision, viewId, sheetId, copy?.revision]);
  useEffect(() => { if (!bridge) { setBook(null); setView(null); setSheetId(''); setLoading(false); setBusy(false); setCopies([]); setViewId('original'); setUncertain(false); } }, [bridge]);
  useEffect(() => {
    if (!bridge || !book || !sheetId || !activeId) return;
    let active = true; setLoading(true); setView(null); setError('');
    void bridge.request('workbook.readViewport', { sessionId: activeId, worksheetId: sheetId, startRow: row, startColumn: column, rows: 20, columns: 6 }).promise
      .then(result => {
        if (active && result.sessionId === activeId && result.worksheetId === sheetId && result.startRow === row && result.startColumn === column && result.rows === 20 && result.columns === 6) setView(result);
        else if (active) setError('The desktop returned a mismatched worksheet window.');
      })
      .catch(e => { if (active) setError((e as Error).message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [bridge, book, sheetId, row, column, activeId, activeRevision]);

  async function open(discard = false) {
    if (!bridge) return; setBusy(true); setError(''); setNotice('');
    try {
      if (discard && book) { await bridge.request('workbook.discard', { sessionId: book.sessionId }).promise; setBook(null); setCopies([]); setView(null); setViewId('original'); setUncertain(false); }
      const result = await bridge.request('workbook.open', {}).promise;
      if (result.status === 'opened' && result.workbook) { setView(null); setBook(result.workbook); setSheetId(result.workbook.worksheets[0].id); setOriginalSheet(result.workbook.worksheets[0].id); setCopies([]); setViewId('original'); setUncertain(false); setSelected(null); setRow(1); setColumn(1); }
      else setNotice('Workbook selection cancelled.');
    } catch (e) { if (e instanceof BridgeError && e.code === 'UNSAVED_CHANGES') setUncertain(true); setError((e as Error).message); }
    finally { setBusy(false); }
  }
  async function close(discard = false) {
    if (!bridge || !book) return; setBusy(true); setError('');
    try { await bridge.request(discard ? 'workbook.discard' : 'workbook.close', { sessionId: book.sessionId }).promise; setBook(null); setCopies([]); setViewId('original'); setUncertain(false); setSelected(null); setView(null); setNotice('Workbook closed. Original file unchanged.'); }
    catch (e) { if (e instanceof BridgeError && e.code === 'UNSAVED_CHANGES') setUncertain(true); setError((e as Error).message); }
    finally { setBusy(false); }
  }
  async function createCopy() {
    if (!bridge || !book) return; setBusy(true); setError(''); setNotice('');
    try {
      const result = await bridge.request('workbook.createCopy', { sessionId: book.sessionId, worksheetId: sheetId, expectedRevision: book.revision }).promise;
      if (result.sessionId === book.sessionId || result.sourceWorksheetId !== sheetId || result.baseRevision !== book.revision || result.revision !== 0 || result.isDirty || result.changeCount !== 0 || copies.some(c => c.sessionId === result.sessionId)) { setUncertain(true); throw new Error('The desktop returned an invalid working copy. Its outcome is unknown.'); }
      setView(null); setCopies(existing => [...existing, result]); setViewId(result.sessionId); setSelected(null); setNotice('Working copy created. Changes stay in memory.');
    } catch (e) { if ((e as Error).message.includes('outcome is unknown')) setUncertain(true); setError((e as Error).message); }
    finally { setBusy(false); }
  }
  async function saveAs() {
    if (!bridge || !book || copy) return; setBusy(true); setError(''); setNotice('');
    try {
      const result = await bridge.request('workbook.saveAsNew', { sessionId: book.sessionId, expectedRevision: book.revision }).promise;
      if (result.status === 'cancelled') { setNotice('Save As cancelled. No file was created.'); return; }
      const receipt = result.receipt;
      if (!receipt || receipt.sessionId !== book.sessionId || receipt.savedRevision !== book.revision || receipt.sourceStillDirty !== book.isDirty) throw new Error('Save outcome is unknown. Inspect the selected file before retrying.');
      setNotice(`Created ${receipt.displayName} from original session revision ${receipt.savedRevision}. Pending working-copy edits were excluded. The session remains bound to its original source.${receipt.cancellationAfterCommit ? ' Cancellation arrived after commit; the file was saved.' : ''}`);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  async function previewMerge() {
    if (!bridge || !book || !copy) return;
    setBusy(true); setError(''); setMerge(null);
    const payload: MergePayload = { sessionId: book.sessionId, workingCopyId: copy.sessionId, worksheetId: copy.sourceWorksheetId, expectedCopyRevision: copy.revision, expectedRevision: book.revision, expectedBaseRevision: copy.baseRevision };
    try { const preview = await bridge.request('workbook.previewMerge', payload).promise;
      if (preview.destinationName !== book.worksheets.find(s => s.id === copy.sourceWorksheetId)?.name) throw new Error('The desktop returned a mismatched destination.');
      setMerge({ payload, preview });
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  async function applyMerge() {
    if (!bridge || !book || !merge || !merge.preview.canApply) return;
    setBusy(true); setError('');
    try {
      const result = await bridge.request('workbook.merge', merge.payload).promise;
      if (result.workbook.sessionId !== book.sessionId || result.workbook.sourceSha256 !== book.sourceSha256 || result.appliedCells !== merge.preview.pendingCells
        || result.workbook.revision !== book.revision + (result.appliedCells > 0 ? 1 : 0) || (result.appliedCells > 0 && !result.workbook.isDirty)) {
        setUncertain(true); throw new Error('The desktop returned an invalid merge result. Its outcome is unknown.');
      }
      setBook(result.workbook); setView(null); setViewId('original'); setSheetId(merge.payload.worksheetId); setOriginalSheet(merge.payload.worksheetId); setSelected(null);
      setNotice(`Merged ${result.appliedCells} cells in memory. Save As is a separate action. The working copy is retained; create a fresh copy for further integration.`);
    } catch (e) { if ((e as Error).message.includes('outcome is unknown')) setUncertain(true); setError((e as Error).message); }
    finally { setMerge(null); setBusy(false); }
  }
  function chooseCell(cell: Cell) {
    setSelected(cell); setError(''); setNotice('');
    setEditKind(cell.formula ? 'formula' : cell.kind === 'Number' ? 'number' : 'text');
    setDraft(cell.formula ? `=${cell.formula}` : cell.value);
  }
  async function applyEdit() {
    if (!bridge || !copy || !selected) return; setBusy(true); setError(''); setNotice('');
    try {
      const result = await bridge.request('workbook.editCopy', { sessionId: copy.sessionId, worksheetId: sheetId, row: selected.row, column: selected.column, kind: editKind, value: draft, expectedRevision: copy.revision }).promise;
      if (result.sessionId !== copy.sessionId || result.sourceWorksheetId !== copy.sourceWorksheetId || result.baseRevision !== copy.baseRevision || result.revision !== copy.revision + 1 || !result.isDirty || result.changeCount !== copy.changeCount + 1) { setUncertain(true); throw new Error('The desktop returned an invalid edit result. Its outcome is unknown.'); }
      setView(null); setCopies(existing => existing.map(c => c.sessionId === result.sessionId ? result : c)); setSelected(null); setNotice('Change applied to the working copy in memory.');
    } catch (e) { if ((e as Error).message.includes('outcome is unknown')) setUncertain(true); setError((e as Error).message); }
    finally { setBusy(false); }
  }

  return <section className="reader" aria-labelledby="workbook-title">
    <div className="reader-heading"><div><h2 id="workbook-title">Workbook viewer</h2><p>{copy ? 'Working copy. Review and merge supported cell edits into its original worksheet.' : 'Original workbook view. Edit an isolated working copy.'}</p></div><button className="primary" disabled={!enabled || busy || loading || !!merge} onClick={() => pending ? setConfirm('open') : void open()}>{busy ? 'Working…' : 'Open workbook'}</button></div>
    {!enabled && <p>Connect to the Windows desktop host to inspect a local .xlsx workbook.</p>}
    {notice && <p aria-live="polite">{notice}</p>}
    {error && <p role="alert" className="error">{error}</p>}
    {uncertain && <p>A working-copy operation has an unknown outcome. Discard the session and reopen the workbook before continuing edits.</p>}
    {confirm && <div role="alertdialog" aria-labelledby="discard-title" className="discard-warning"><strong id="discard-title">Discard pending working-copy changes?</strong><p>Unsaved changes exist only in memory. Closing discards them.</p><div className="actions"><button onClick={() => setConfirm(null)}>Keep editing</button><button onClick={() => { const action = confirm; setConfirm(null); if (action === 'close') void close(true); else void open(true); }}>Discard changes</button></div></div>}
    {merge && <div role="alertdialog" aria-label="Review worksheet merge"><strong>Merge into {merge.preview.destinationName}</strong><p>{merge.preview.pendingCells} effective pending cell edits. The original file stays unchanged.</p>{merge.preview.conflicts.map(c => <p role="alert" key={c.code}>{c.code}: {c.message}</p>)}<button disabled={busy} onClick={() => setMerge(null)}>Cancel merge</button><button disabled={busy || !merge.preview.canApply || uncertain} onClick={() => void applyMerge()}>Confirm merge in memory</button></div>}
    {!book && <p className="reader-empty">No workbook open. Source files remain unchanged.</p>}
    {book && <>
      <div className="reader-controls"><button disabled={!enabled || busy || loading || !!copy || !book.editingAllowed || uncertain} onClick={() => void saveAs()}>Save As</button><p>Save As creates a new .xlsx file from the original session only. Pending working-copy edits are excluded. Existing files cannot be overwritten.</p></div>
      <div className="reader-file"><strong>{book.displayName}</strong><span>{uncertain ? 'Working-copy outcome unknown' : pending ? 'Pending changes in working copies' : 'No pending changes'}</span><button disabled={busy || loading} onClick={() => pending ? setConfirm('close') : void close()}>Close workbook</button></div>
      <div className="reader-controls"><label>View <select aria-label="Workbook view" value={viewId} disabled={busy || loading} onChange={e => { const id = e.target.value; setView(null); setViewId(id); setSheetId(copies.find(c => c.sessionId === id)?.sourceWorksheetId ?? originalSheet); setSelected(null); setRow(1); setColumn(1); }}><option value="original">Original session</option>{copies.map((c, i) => <option key={c.sessionId} value={c.sessionId}>Working copy {i + 1}{c.isDirty ? ' · Pending changes' : ''}</option>)}</select></label><button disabled={busy || loading || uncertain || !!copy || !book.editingAllowed || copies.length >= 2} onClick={() => void createCopy()}>Create working copy</button></div>
      {!book.editingAllowed && <p>This workbook is available for inspection only because its content is outside the supported editing scope.</p>}
      <div className="reader-controls"><label>Worksheet <select aria-label="Worksheet" value={sheetId} disabled={busy || loading} onChange={e => { setView(null); setSheetId(e.target.value); if (!copy) setOriginalSheet(e.target.value); setSelected(null); setRow(1); setColumn(1); }}>{book.worksheets.map(s => <option key={s.id} value={s.id}>{s.name}{s.visibility !== 'Visible' ? ` (${s.visibility})` : ''}</option>)}</select></label><span>{sheet ? `Used extent: ${sheet.lastRow} rows × ${sheet.lastColumn} columns` : ''}</span></div>
      {copy && <p className="cached-notice">Working copy {copies.findIndex(c => c.sessionId === copy.sessionId) + 1} · Base revision {copy.baseRevision} · Revision {copy.revision} · {copy.isDirty ? 'Pending changes' : 'No changes'}{copy.sourceWorksheetId !== sheetId ? ' · This context sheet is read-only' : ' · Select a cell to edit'}</p>}
      {copy && <div className="reader-controls"><p>Destination: {book.worksheets.find(s => s.id === copy.sourceWorksheetId)?.name}. Recorded edits: {copy.changeCount}. Operation: Merge. Replace and Create New are unavailable pending preservation tests.</p><button disabled={busy || loading || uncertain || !!merge} onClick={() => void previewMerge()}>Review Merge</button></div>}
      {copy && selected && copy.sourceWorksheetId === sheetId && <div className="cell-editor"><strong>Edit {columnLabel(selected.column)}{selected.row}</strong>{selected.formula && <p>This cell contains a formula. Select Formula to edit it explicitly; other edits are rejected.</p>}<label>Value type <select aria-label="Value type" value={editKind} disabled={busy || uncertain} onChange={e => setEditKind(e.target.value as typeof editKind)}><option value="text">Text</option><option value="number">Number</option><option value="date">Date (YYYY-MM-DD)</option><option value="formula">Formula</option></select></label><label>Cell value <input aria-label="Cell value" value={draft} maxLength={1024} disabled={busy || uncertain} onChange={e => setDraft(e.target.value)} /></label><button className="primary" disabled={busy || loading || uncertain} onClick={() => void applyEdit()}>Apply to working copy</button></div>}
      <p className="cached-notice">Formula results are stored cached values and may be blank or stale. Formulas are not recalculated.</p>
      {loading && <p aria-live="polite">Loading worksheet…</p>}
      {view && <div className="cell-window" tabIndex={0} aria-label="Worksheet viewport"><table><caption>{sheet?.name} · Rows {row}–{row + 19}, columns {columnLabel(column)}–{columnLabel(column + 5)}</caption><thead><tr><th scope="col">Row</th>{Array.from({ length: 6 }, (_, i) => <th scope="col" key={i}>{columnLabel(column + i)}</th>)}</tr></thead><tbody>{Array.from({ length: 20 }, (_, r) => <tr key={r}><th scope="row">{row + r}</th>{view.cells.slice(r * 6, r * 6 + 6).map(cell => <td key={cell.column} className={cell.formula ? 'formula-cell' : ''} title={cell.formula ? `=${cell.formula}${cell.truncated ? ' (truncated)' : ''}` : `${cell.value}${cell.truncated ? ' (truncated)' : ''}`} style={{ fontWeight: cell.format.bold ? 600 : undefined, fontStyle: cell.format.italic ? 'italic' : undefined }}>{copy && copy.sourceWorksheetId === sheetId ? <button className="cell-select" disabled={busy || loading || uncertain} aria-label={`Select ${columnLabel(cell.column)}${cell.row}`} aria-pressed={selected?.row === cell.row && selected.column === cell.column} onClick={() => chooseCell(cell)}>{cell.formula ? <><span className="formula-marker">ƒ</span>{cell.value || <span className="formula-text">={cell.formula}</span>}</> : cell.value}{cell.truncated ? '…' : ''}</button> : <>{cell.formula ? <><span className="formula-marker">ƒ</span>{cell.value || <span className="formula-text">={cell.formula}</span>}</> : cell.value}{cell.truncated ? '…' : ''}</>}</td>)}</tr>)}</tbody></table></div>}
      <div className="reader-paging"><button disabled={busy || loading || row <= 1} onClick={() => { setView(null); setSelected(null); setRow(r => Math.max(1, r - 20)); }}>Previous rows</button><button disabled={busy || loading || row + 20 > (sheet?.lastRow ?? 0) || row >= 1048557} onClick={() => { setView(null); setSelected(null); setRow(r => Math.min(r + 20, 1048557)); }}>Next rows</button><button disabled={busy || loading || column <= 1} onClick={() => { setView(null); setSelected(null); setColumn(c => Math.max(1, c - 6)); }}>Previous columns</button><button disabled={busy || loading || column + 6 > (sheet?.lastColumn ?? 0) || column >= 16379} onClick={() => { setView(null); setSelected(null); setColumn(c => Math.min(c + 6, 16379)); }}>Next columns</button></div>
      <details className="compatibility"><summary>Compatibility findings ({book.findings.length})</summary><ul>{book.findings.map(f => <li key={f.code}>{f.message}</li>)}</ul><p>Creator: {book.creator || 'Not specified'}. Title: {book.title || 'Not specified'}.</p></details>
      {bridge && activeId && activeRevision !== undefined && <PayablesProcessing key={`${activeId}:${sheetId}:${activeRevision}`} bridge={bridge} sessionId={activeId} worksheetId={sheetId}
          revision={activeRevision} sourceSha256={book.sourceSha256} workbookName={book.displayName} worksheetName={sheet?.name??''} disabled={busy||loading||uncertain||!!merge}
          destinationSessionId={book.sessionId} destinationRevision={book.revision} isWorkingCopy={!!copy}/>}
    </>}
  </section>;
}
