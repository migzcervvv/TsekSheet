import { useCallback, useEffect, useRef, useState } from 'react';
import {useReviewDiscard} from './useReviewDiscard';
import {parseCell} from './worksheetNavigation';
import { BridgeError } from './bridge/client';
import PayablesProcessing from './PayablesProcessing';
import SectionReview from './SectionReview';
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
  const [confirm, setConfirm] = useState<'close' | 'open' | 'inspect' | null>(null);
  const [uncertain, setUncertain] = useState(false);
  const [merge, setMerge] = useState<{ payload: MergePayload; preview: MergePreview } | null>(null);
  const sheet = book?.worksheets.find(s => s.id === sheetId);
  const [reviewCount,setReviewCount]=useState(0),[reviewBusy,setReviewBusy]=useState(false);
  const [reviewUncertain,setReviewUncertain]=useState(false);
  const reviewStatus=useCallback((count:number,working:boolean,unknown:boolean)=>{setReviewCount(count);setReviewBusy(working);setReviewUncertain(unknown);},[]);
  const discardReview=useReviewDiscard(reviewCount,reviewUncertain);
  const [goTo,setGoTo]=useState(''),[fullRange,setFullRange]=useState(false);
  const [explicitExtent,setExplicitExtent]=useState({row:1,column:1});
  const [scan,setScan]=useState<Viewport['scan']>(null);
  const flight=useRef<Promise<unknown>|null>(null), scroll=useRef<HTMLDivElement>(null);
  const target=useRef<{row:number;column:number}|null>(null);
  const maxRow=Math.max(1,book?.compatibility ? (fullRange ? Math.max(scan?.lastRow??0,scan?.lastRowElement??0,explicitExtent.row) : scan?.meaningfulLastRow??scan?.lastRow??1048576) : sheet?.lastRow??1);
  const maxColumn=Math.max(1,book?.compatibility ? (fullRange ? Math.max(scan?.lastColumn??16384,explicitExtent.column) : scan?.meaningfulLastColumn??scan?.lastColumn??16384) : sheet?.lastColumn??1);
  const interactionBusy=busy||reviewBusy||discardReview.confirming;
  const enabled = available && bridge !== null;

  const copy = copies.find(c => c.sessionId === viewId);
  const activeId = copy?.sessionId ?? book?.sessionId;
  const activeRevision = copy?.revision ?? book?.revision;
  const pending = uncertain || copies.some(c => c.isDirty) || book?.isDirty === true;
  useEffect(()=>{setScan(null);setExplicitExtent({row:1,column:1});setFullRange(false);setReviewCount(0);setReviewBusy(false);setReviewUncertain(false);target.current=null;},[book?.sessionId,sheetId]);
  useEffect(()=>{if(scroll.current){scroll.current.scrollTop=(row-1)*28;scroll.current.scrollLeft=(column-1)*140;}},[row,column,!!view]);
  useEffect(() => { setMerge(null); }, [bridge, book?.revision, viewId, sheetId, copy?.revision]);
  useEffect(() => { if (!bridge) { setBook(null); setView(null); setSheetId(''); setLoading(false); setBusy(false); setCopies([]); setViewId('original'); setUncertain(false); } }, [bridge]);
  useEffect(() => {
    if (!bridge || !book || !sheetId || !activeId) return;
    let active = true; setLoading(true); setError('');
    // Coalesce wheel/trackpad bursts and let an admitted scan finish before the next
    // request. Cleanup cancels queued work and prevents late session responses.
    const timer=setTimeout(()=>{void (async()=>{
      await flight.current?.catch(()=>{});
      if(!active)return;
      const request=bridge.request('workbook.readViewport',{sessionId:activeId,worksheetId:sheetId,startRow:row,startColumn:column,rows:20,columns:6}).promise;
      flight.current=request;
      try{
        const result=await request;
        if(!active)return;
        if(result.sessionId!==activeId||result.worksheetId!==sheetId||result.startRow!==row||result.startColumn!==column||result.rows!==20||result.columns!==6)throw new Error('The desktop returned a mismatched worksheet window.');
        setView(result);setScan(result.scan);
        const position=target.current??selected;
        if(position){const cell=result.cells.find(c=>c.row===position.row&&c.column===position.column);if(cell){setSelected(cell);target.current=null;}}
      }catch(e){if(active)setError((e as Error).message);}
      finally{if(flight.current===request)flight.current=null;if(active)setLoading(false);}
    })();},80);
    return ()=>{active=false;clearTimeout(timer);};
  }, [bridge, book, sheetId, row, column, activeId, activeRevision]);

  async function open(discard = false, inspectOnly = false) {
    if (!bridge) return; setBusy(true); setError(''); setNotice('');
    try {
      if (discard && book) { await bridge.request('workbook.discard', { sessionId: book.sessionId }).promise; setBook(null); setCopies([]); setView(null); setViewId('original'); setUncertain(false); }
      const result = await bridge.request(inspectOnly ? 'workbook.inspect' : 'workbook.open', {}).promise;
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

  function navigate(r:number,c:number) {
    if(!Number.isInteger(r)||!Number.isInteger(c)||r<1||r>1048576||c<1||c>16384)return;
    if(r>maxRow||c>maxColumn){setExplicitExtent(e=>({row:Math.max(e.row,r),column:Math.max(e.column,c)}));setFullRange(true);}
    target.current={row:r,column:c};
    const cell=view?.cells.find(x=>x.row===r&&x.column===c);
    if(cell){setSelected(cell);target.current=null;}
    else {setRow(Math.min(r,1048557));setColumn(Math.min(c,16379));}
  }
  function keyboard(e:React.KeyboardEvent) {
    if(!book?.compatibility||interactionBusy||!['ArrowUp','ArrowDown','ArrowLeft','ArrowRight','PageUp','PageDown','Home','End'].includes(e.key))return;
    e.preventDefault();const position=target.current??selected;const r=position?.row??row,c=position?.column??column;
    if(e.key==='Home')navigate(e.ctrlKey?1:r,1);
    else if(e.key==='End')navigate(e.ctrlKey?maxRow:r,maxColumn);
    else navigate(Math.max(1,Math.min(maxRow,r+(e.key==='ArrowUp'?-1:e.key==='ArrowDown'?1:e.key==='PageUp'?-20:e.key==='PageDown'?20:0))),Math.max(1,Math.min(maxColumn,c+(e.key==='ArrowLeft'?-1:e.key==='ArrowRight'?1:0))));
  }
  return <section className="reader" aria-labelledby="workbook-title">
    <div className="reader-heading"><div><h2 id="workbook-title">Workbook viewer</h2><p>{book?.compatibility ? 'Read-only package inspection. Source unchanged; editing and export unavailable.' : copy ? 'Working copy. Review and merge supported cell edits into its original worksheet.' : 'Original workbook view. Edit an isolated working copy.'}</p></div><button className="primary" disabled={!enabled || interactionBusy || loading || !!merge} onClick={() => discardReview.guard(()=>pending ? setConfirm('open') : void open())}>{busy ? 'Working…' : 'Open workbook'}</button><button disabled={!enabled || interactionBusy || loading || !!merge} onClick={() => discardReview.guard(()=>pending ? setConfirm('inspect') : void open(false, true))}>Inspect workbook read-only</button></div>
    {discardReview.dialog}
    {!enabled && <p>Connect to the Windows desktop host to inspect a local .xlsx workbook.</p>}
    {notice && <p aria-live="polite">{notice}</p>}
    {error && <p role="alert" className="error">{error}</p>}
    {uncertain && <p>A working-copy operation has an unknown outcome. Discard the session and reopen the workbook before continuing edits.</p>}
    {confirm && <div role="alertdialog" aria-labelledby="discard-title" className="discard-warning"><strong id="discard-title">Discard pending working-copy changes?</strong><p>Unsaved changes exist only in memory. Closing discards them.</p><div className="actions"><button onClick={() => setConfirm(null)}>Keep editing</button><button onClick={() => { const action = confirm; setConfirm(null); if (action === 'close') void close(true); else void open(true, action === 'inspect'); }}>Discard changes</button></div></div>}
    {merge && <div role="alertdialog" aria-label="Review worksheet merge"><strong>Merge into {merge.preview.destinationName}</strong><p>{merge.preview.pendingCells} effective pending cell edits. The original file stays unchanged.</p>{merge.preview.conflicts.map(c => <p role="alert" key={c.code}>{c.code}: {c.message}</p>)}<button disabled={busy} onClick={() => setMerge(null)}>Cancel merge</button><button disabled={busy || !merge.preview.canApply || uncertain} onClick={() => void applyMerge()}>Confirm merge in memory</button></div>}
    {!book && <p className="reader-empty">No workbook open. Source files remain unchanged.</p>}
    {book && <>
      <div className="reader-controls"><button disabled={!enabled || busy || loading || !!copy || !book.editingAllowed || uncertain} onClick={() => void saveAs()}>Save As</button><p>Save As creates a new .xlsx file from the original session only. Pending working-copy edits are excluded. Existing files cannot be overwritten.</p></div>
      <div className="reader-file"><strong>{book.displayName}</strong><span>{uncertain ? 'Working-copy outcome unknown' : pending ? 'Pending changes in working copies' : 'No pending changes'}</span><button disabled={interactionBusy || loading} onClick={() => discardReview.guard(()=>pending ? setConfirm('close') : void close())}>Close workbook</button></div>
      <div className="reader-controls"><label>View <select aria-label="Workbook view" value={viewId} disabled={busy || loading} onChange={e => { const id = e.target.value; setView(null); setViewId(id); setSheetId(copies.find(c => c.sessionId === id)?.sourceWorksheetId ?? originalSheet); setSelected(null); setRow(1); setColumn(1); }}><option value="original">Original session</option>{copies.map((c, i) => <option key={c.sessionId} value={c.sessionId}>Working copy {i + 1}{c.isDirty ? ' · Pending changes' : ''}</option>)}</select></label><button disabled={busy || loading || uncertain || !!copy || !book.editingAllowed || copies.length >= 2} onClick={() => void createCopy()}>Create working copy</button></div>
      {!book.editingAllowed && <p>This workbook is available for inspection only because its content is outside the supported editing scope.</p>}
      <div className="reader-controls"><label>Worksheet <select aria-label="Worksheet" value={sheetId} disabled={interactionBusy || loading} onChange={e => {const id=e.target.value;discardReview.guard(()=>{setView(null);setSheetId(id);if(!copy)setOriginalSheet(id);setSelected(null);setRow(1);setColumn(1);});}}>{book.worksheets.map(s => <option key={s.id} value={s.id}>{s.name}{s.visibility !== 'Visible' ? ` (${s.visibility})` : ''}</option>)}</select></label><span>{sheet ? (book.compatibility ? 'Dimensions measured on selected-sheet scan only' : `Used extent: ${sheet.lastRow} rows × ${sheet.lastColumn} columns`) : ''}</span></div>
      {book.compatibility && <div className="reader-controls"><label>Start row <input aria-label="Inspection start row" type="number" min="1" max="1048557" value={row} disabled={loading || busy} onChange={e => { const n = Number(e.target.value); if (Number.isInteger(n) && n >= 1 && n <= 1048557) {if(n>maxRow){setExplicitExtent(e=>({...e,row:n+19}));setFullRange(true);}setRow(n);} }} /></label><label>Start column <input aria-label="Inspection start column" type="number" min="1" max="16379" value={column} disabled={loading || busy} onChange={e => { const n = Number(e.target.value); if (Number.isInteger(n) && n >= 1 && n <= 16379) {if(n>maxColumn){setExplicitExtent(e=>({...e,column:n+5}));setFullRange(true);}setColumn(n);} }} /></label></div>}
      {book.compatibility&&<div className="reader-controls"><form onSubmit={e=>{e.preventDefault();const cell=parseCell(goTo);if(cell){setError('');navigate(cell.row,cell.column);}else setError('Enter a cell from A1 to XFD1048576.');}}><label>Go To Cell <input aria-label="Go To Cell" value={goTo} onChange={e=>setGoTo(e.target.value)} placeholder="C52"/></label><button disabled={interactionBusy}>Go</button></form><label><input type="checkbox" checked={fullRange} onChange={e=>{setFullRange(e.target.checked);setExplicitExtent({row:1,column:1});setRow(1);setColumn(1);}}/> Inspect full stored range</label><span>{fullRange?'Stored range':'Useful data'}: {maxRow} rows × {maxColumn} columns</span><p>Internal blank rows remain visible. Bounds include hidden values and formulas. Empty shared-string references may extend the range; objects, merges and formula spill areas are not measured. Display bounds never limit analysis.</p></div>}
      {copy && <p className="cached-notice">Working copy {copies.findIndex(c => c.sessionId === copy.sessionId) + 1} · Base revision {copy.baseRevision} · Revision {copy.revision} · {copy.isDirty ? 'Pending changes' : 'No changes'}{copy.sourceWorksheetId !== sheetId ? ' · This context sheet is read-only' : ' · Select a cell to edit'}</p>}
      {copy && <div className="reader-controls"><p>Destination: {book.worksheets.find(s => s.id === copy.sourceWorksheetId)?.name}. Recorded edits: {copy.changeCount}. Operation: Merge. Replace and Create New are unavailable pending preservation tests.</p><button disabled={busy || loading || uncertain || !!merge} onClick={() => void previewMerge()}>Review Merge</button></div>}
      {copy && selected && copy.sourceWorksheetId === sheetId && <div className="cell-editor"><strong>Edit {columnLabel(selected.column)}{selected.row}</strong>{selected.formula && <p>This cell contains a formula. Select Formula to edit it explicitly; other edits are rejected.</p>}<label>Value type <select aria-label="Value type" value={editKind} disabled={busy || uncertain} onChange={e => setEditKind(e.target.value as typeof editKind)}><option value="text">Text</option><option value="number">Number</option><option value="date">Date (YYYY-MM-DD)</option><option value="formula">Formula</option></select></label><label>Cell value <input aria-label="Cell value" value={draft} maxLength={1024} disabled={busy || uncertain} onChange={e => setDraft(e.target.value)} /></label><button className="primary" disabled={busy || loading || uncertain} onClick={() => void applyEdit()}>Apply to working copy</button></div>}
      <p className="cached-notice">Formula results are stored cached values and may be blank or stale. Formulas are not recalculated.</p>
      {loading && <p aria-live="polite">Loading worksheet…</p>}
      {book.compatibility && selected && <div className="cell-editor" aria-label="Read-only cell details"><strong>Inspect {columnLabel(selected.column)}{selected.row}</strong><p>Raw value: {selected.interpretation?.rawValue ?? selected.value}</p><p>Display value: {selected.interpretation?.displayValue ?? selected.value}</p><p>Type: {selected.interpretation?.resolvedType ?? selected.kind} · Interpretation: {selected.interpretation?.confidence ?? 'Stored'}</p><p>Number format: {selected.format.numberFormat || 'Unspecified'}</p><p>Source format ID: {selected.format.numberFormatId} · Effective format ID: {selected.format.effectiveNumberFormatId ?? 'Unresolved'}</p>{selected.interpretation?.classification && <p>Display classification: {selected.interpretation.classification}</p>}{selected.format.formatResolution && <p>Format resolution: {selected.format.formatResolution}</p>}{selected.formula !== null && <><p>Formula text: {selected.formula || 'Unavailable'}</p><p>Stored cached value (may be stale): {selected.value || 'Unavailable'}</p></>}<p>{selected.interpretation?.formulaStatus}</p>{selected.interpretation?.warning && <p role="note">{selected.interpretation.warning}</p>}</div>}
      {view && <div className={book.compatibility ? "cell-window inspection" : "cell-window"} ref={scroll} tabIndex={0} onKeyDown={keyboard} onScroll={e=>{if(!book.compatibility||interactionBusy)return;const el=e.currentTarget;setRow(Math.min(1048557,Math.max(1,Math.min(maxRow,Math.floor(el.scrollTop/28)+1))));setColumn(Math.min(16379,Math.max(1,Math.min(maxColumn,Math.floor(el.scrollLeft/140)+1))));}} aria-label="Worksheet viewport" style={book.compatibility?{height:620}:undefined}><div className={book.compatibility?'inspection-space':undefined} style={book.compatibility?{height:maxRow*28+620,width:Math.max(900,maxColumn*140+900)}:undefined}><table><caption>{sheet?.name} · Rows {view.startRow}–{book.compatibility?Math.min(maxRow,view.startRow + 19):view.startRow+19}, columns {columnLabel(view.startColumn)}–{columnLabel(Math.min(maxColumn,view.startColumn + 5))}</caption><thead><tr><th scope="col">Row</th>{Array.from({ length: book.compatibility?Math.min(6,maxColumn-view.startColumn+1):6 }, (_, i) => <th scope="col" key={i}>{columnLabel(view.startColumn + i)}</th>)}</tr></thead><tbody>{Array.from({ length: book.compatibility?Math.min(20,maxRow-view.startRow+1):20 }, (_, r) => <tr key={r}><th scope="row">{view.startRow + r}</th>{view.cells.slice(r * 6, r * 6 + 6).filter(cell=>!book.compatibility||cell.column<=maxColumn).map(cell => <td key={cell.column} className={cell.formula ? 'formula-cell' : ''} title={cell.formula ? `=${cell.formula}${cell.truncated ? ' (truncated)' : ''}` : `${cell.value}${cell.truncated ? ' (truncated)' : ''}`} style={{ fontWeight: cell.format.bold ? 600 : undefined, fontStyle: cell.format.italic ? 'italic' : undefined }}>{book.compatibility ? <button className="cell-select" disabled={busy || loading} aria-label={`Inspect ${columnLabel(cell.column)}${cell.row}`} aria-pressed={selected?.row===cell.row&&selected.column===cell.column} onClick={() => setSelected(cell)}>{cell.interpretation?.displayValue ?? (cell.value || (cell.formula ? `=${cell.formula}` : cell.interpretation?.formulaStatus.startsWith('Unsupported') ? 'Unsupported formula' : ''))}{cell.truncated ? '…' : ''}</button> : copy && copy.sourceWorksheetId === sheetId ? <button className="cell-select" disabled={busy || loading || uncertain} aria-label={`Select ${columnLabel(cell.column)}${cell.row}`} aria-pressed={selected?.row === cell.row && selected.column === cell.column} onClick={() => chooseCell(cell)}>{cell.formula ? <><span className="formula-marker">ƒ</span>{cell.value || <span className="formula-text">={cell.formula}</span>}</> : cell.value}{cell.truncated ? '…' : ''}</button> : <>{cell.formula ? <><span className="formula-marker">ƒ</span>{cell.value || <span className="formula-text">={cell.formula}</span>}</> : cell.value}{cell.truncated ? '…' : ''}</>}</td>)}</tr>)}</tbody></table></div></div>}
      <div className="reader-paging"><button disabled={busy || loading || row <= 1} onClick={() => { setRow(r => Math.max(1, r - 20)); }}>Previous rows</button><button disabled={busy || loading || row + 20 > maxRow || row >= 1048557} onClick={() => { setRow(r => Math.min(r + 20, 1048557)); }}>Next rows</button><button disabled={busy || loading || column <= 1} onClick={() => { setColumn(c => Math.max(1, c - 6)); }}>Previous columns</button><button disabled={busy || loading || column + 6 > maxColumn || column >= 16379} onClick={() => { setColumn(c => Math.min(c + 6, 16379)); }}>Next columns</button></div>
      {book.compatibility && <details className="compatibility" open><summary>Compatibility Report — {book.displayName}</summary><p>{book.worksheets.length} worksheets/tabs · {book.compatibility.fileBytes.toLocaleString()} file bytes · {book.compatibility.compressedPartBytes.toLocaleString()} compressed part bytes · {book.compatibility.expandedPartBytes.toLocaleString()} declared expanded bytes · {book.compatibility.partCount} parts</p><p>{book.compatibility.countStatus}</p><p>{book.compatibility.definedNames} defined names · {book.compatibility.relationships} relationships · External links: {book.compatibility.externalLinks ? 'Present; never resolved' : 'Not observed in relationships'}</p><ul>{book.compatibility.categories.map(c => <li key={c.category}>{c.category}: {c.count}</li>)}</ul><ul>{book.compatibility.capabilities.map(c => <li key={c.operation}>{c.operation}: {c.allowed ? 'Available within inspection budgets' : 'Unavailable'}. {c.reason}</li>)}</ul><p>Next steps: inspect a bounded range. To assess editing, close inspection and use Open workbook; full validation is required. Repair or simplify only a separate copy in Excel if necessary.</p></details>}
      {view?.scan && <div aria-label="Selected worksheet diagnostics"><p>Complete selected-sheet scan: {view.scan.storedCells.toLocaleString()} stored cell elements · {view.scan.nonemptyValues.toLocaleString()} nonempty stored values · {view.scan.formulas.toLocaleString()} formulas · {view.scan.styledCells.toLocaleString()} cells with explicit style attributes. Stored extent: {view.scan.lastRow} × {view.scan.lastColumn}.</p><p>Declared dimension: {view.scan.declaredDimension}. {view.scan.dimensionReliability}. Potential inflated range: {view.scan.potentialInflation ? 'Yes (heuristic)' : 'Not observed'}.</p><p>Select a cell to compare raw values, supported display interpretation and formula-cache warnings.</p>{view.scan.warnings.map(w => <p key={w}>{w}</p>)}</div>}
      <details className="compatibility"><summary>Compatibility findings ({book.findings.length})</summary><ul>{book.findings.map(f => <li key={f.code}>{f.message}</li>)}</ul><p>Creator: {book.creator || 'Not specified'}. Title: {book.title || 'Not specified'}.</p></details>
      {book.compatibility && bridge && <SectionReview key={`${book.sessionId}:${sheetId}`} bridge={bridge} book={book} sheetId={sheetId} disabled={interactionBusy || loading} onStatus={reviewStatus} onNavigate={navigate}/>}
      {!book.compatibility && bridge && activeId && activeRevision !== undefined && <PayablesProcessing key={`${activeId}:${sheetId}:${activeRevision}`} bridge={bridge} sessionId={activeId} worksheetId={sheetId}
          revision={activeRevision} sourceSha256={book.sourceSha256} workbookName={book.displayName} worksheetName={sheet?.name??''} disabled={busy||loading||uncertain||!!merge}
          destinationSessionId={book.sessionId} destinationRevision={book.revision} isWorkingCopy={!!copy}/>}
    </>}
  </section>;
}
