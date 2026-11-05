import { EmptyState, Notice, StatusBadge } from './ui/Workspace';
import type { Activity } from './ui/Workspace';
import ReceiptReview from './ReceiptReview';
import { useCallback, useEffect, useRef, useState } from 'react';
import {useReviewDiscard} from './useReviewDiscard';
import {parseCell} from './worksheetNavigation';
import { BridgeError } from './bridge/client';
import SectionReview from './SectionReview';
import type { BridgeClient, Workbook, Viewport, Cell, CompatibilityFinding } from './bridge/client';

const columnLabel = (column: number): string => { let label = ''; for (let n = column; n > 0; n = Math.floor((n - 1) / 26)) label = String.fromCharCode(65 + (n - 1) % 26) + label; return label; };

export default function WorkbookViewer({ bridge, available, receiptsAvailable = false, renderingAvailable = false, activity, onActivityChange, openRequest = 0, onContextChange }: { bridge: BridgeClient | null; available: boolean; receiptsAvailable?: boolean; renderingAvailable?: boolean; activity?: Activity; onActivityChange?: (activity: Activity) => void; openRequest?: number; onContextChange?: (context:{name:string;sheet:string;copy:string;pending:boolean;inspection:boolean}|null)=>void }) {
  const [receiptActive, setReceiptActive] = useState(false);
  const [receiptStep, setReceiptStep] = useState(0);
  const seenOpenRequest = useRef(0);
  const [book, setBook] = useState<Workbook | null>(null);
  const [sheetId, setSheetId] = useState('');
  const [row, setRow] = useState(1); const [column, setColumn] = useState(1);
  const [view, setView] = useState<Viewport | null>(null);
  const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(false);
  const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  const [openingDetails, setOpeningDetails] = useState<CompatibilityFinding[]>([]);
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

  const [selected, setSelected] = useState<Cell | null>(null);
  const activeId = book?.sessionId;
  const activeRevision = book?.revision;
  const pending = false;
  useEffect(()=>{setScan(null);setExplicitExtent({row:1,column:1});setFullRange(false);setReviewCount(0);setReviewBusy(false);setReviewUncertain(false);target.current=null;},[book?.sessionId,sheetId]);
  useEffect(()=>{if(scroll.current){scroll.current.scrollTop=(row-1)*28;scroll.current.scrollLeft=(column-1)*140;}},[row,column,!!view]);
  useEffect(() => { if (!bridge) { setBook(null); setView(null); setSheetId(''); setLoading(false); setBusy(false); } }, [bridge]);
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

  useEffect(()=>{onContextChange?.(book?{name:book.displayName,sheet:sheet?.name??'',copy:'Read-only inspection',pending,inspection:!book.editingAllowed}:null);},[onContextChange,book?.sessionId,book?.displayName,sheet?.name,pending,book?.editingAllowed]);
  useEffect(()=>{
    if(openRequest===seenOpenRequest.current)return;
    seenOpenRequest.current=openRequest;
    if(!enabled || interactionBusy || loading)return;
    discardReview.guard(()=>void open());
  },[openRequest]);

  async function open(inspectOnly = false) {
    if (book && receiptActive) { setError('Close the current receipt before switching workbooks. Its review and unsaved fields have been retained.'); return; }
    if (!bridge) return; setBusy(true); setError(''); setNotice(''); setOpeningDetails([]);
    try {
      const result = await bridge.request(inspectOnly ? 'workbook.inspect' : 'workbook.open', {}).promise;
      if (result.status === 'opened' && result.workbook) { setView(null); setBook(result.workbook); setSheetId(result.workbook.worksheets[0].id); setSelected(null); setRow(1); setColumn(1); }
      else setNotice('Workbook selection cancelled.');
    } catch (e) { if (e instanceof BridgeError) { setOpeningDetails([{code:e.code,message:e.message}, ...e.details]); } setError((e as Error).message); }
    finally { setBusy(false); }
  }
  async function close() {
    if (receiptActive) { setError('Close the current receipt before closing its workbook. Its review has been retained.'); return; }
    if (!bridge || !book) return; setBusy(true); setError(''); setOpeningDetails([]);
    try { await bridge.request('workbook.close', { sessionId: book.sessionId }).promise; setBook(null); setSelected(null); setView(null); setNotice('Workbook closed. Original file unchanged.'); }
    catch (e) { setError((e as Error).message); }
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
  return <div className="workspace-content" data-activity={activity}>
    {activity==='overview' && <section className="workbook-overview">{book?<><h2>{book.displayName}</h2><Notice kind={book.compatibility?'unavailable':pending?'warning':'information'} title={book.compatibility?'Inspection-only workbook':pending?'Unsaved changes in this workspace':'Workbook ready for review'}>Original file remains unchanged. {book.worksheets.length} worksheets.</Notice><div className="actions"><button onClick={()=>onActivityChange?.('receipts')}>Review receipts</button><button onClick={()=>onActivityChange?.('worksheets')}>Explore worksheets</button></div></>:<EmptyState title="Start with an Excel workbook">Each selected workbook is a project. Choose a local .xlsx file to begin. No recent paths or personal data are stored.</EmptyState>}</section>}

    {error && activity==='receipts' && <Notice kind="error" title="Workbook action blocked">{error}</Notice>}
    <div className="receipt-task" hidden={!!activity && activity!=='receipts'}>
    {bridge && receiptsAvailable && <ReceiptReview bridge={bridge} renderingAvailable={renderingAvailable} view={activity?'workflow':'all'} onActiveChange={setReceiptActive} onStepChange={setReceiptStep} onNavigate={onActivityChange} disabled={false} destination={book && !book.compatibility && activeId && activeRevision !== undefined ? {sessionId:activeId,worksheetId:sheetId,revision:activeRevision,sourceSha256:book.sourceSha256} : null}/>}

    {(!bridge || !receiptsAvailable) && <EmptyState title="Receipt review unavailable">Connect to the Windows desktop host to register a local PDF or image. OCR and rendering are unavailable.</EmptyState>}
    </div>
    <section hidden={!!activity && activity!=='worksheets' && activity!=='overview' && !(activity==='receipts' && receiptStep===2)} className="reader" aria-labelledby="workbook-title">
    <div className="reader-heading"><div><h2 id="workbook-title">Workbook viewer</h2><p className="workbook-guidance">{book?.compatibility ? 'Read-only package inspection. Source unchanged; editing and export unavailable.' : 'Read-only workbook view. Use Insert into Excel to add verified receipt rows.'}</p></div><button className="primary" disabled={!enabled || interactionBusy || loading} onClick={() => discardReview.guard(()=>void open())}>{busy ? 'Working…' : 'Open workbook'}</button><button disabled={!enabled || interactionBusy || loading} onClick={() => discardReview.guard(()=>void open(true))}>Inspect workbook read-only</button></div>
    {discardReview.dialog}
    {!enabled && <p>Connect to the Windows desktop host to inspect a local .xlsx workbook.</p>}
    {notice && <p aria-live="polite">{notice}</p>}
    {error && <p role="alert" className="error">{error}</p>}
    {openingDetails.length > 0 && <details className="workbook-report"><summary>Opening failure details</summary><ul>{openingDetails.map((f,i)=><li key={i}><strong>{f.code}</strong>: {f.message}</li>)}</ul></details>}
    {book && !book.editingAllowed && <Notice kind="warning" title="Workbook opened in inspection mode">
      <p>{book.compatibility ? book.findings.some(f=>f.code.startsWith('EDITABLE_ADMISSION_')) ? 'Editable admission was rejected. The workbook is available for safe inspection.' : 'Opened for read-only inspection. Editing eligibility was not evaluated.' : 'This workbook view is read-only; insertion uses the Excel workflow below.'} Open the workbook under Insert into Excel to use profile sections.</p>
      <details className="workbook-report"><summary>Details</summary><ul>{book.findings.map((f,i)=><li key={i}><strong>{f.code}</strong>: {f.message}</li>)}</ul>
        {book.compatibility && <><p>Metadata inspection succeeded. Each worksheet is checked separately when viewed; some tabs or formats may be unavailable. No external links are followed. Editable admission stops at its first rejection; package categories are not preservation certification.</p><ul>{book.compatibility.categories.map(c=><li key={c.category}>{c.category}: {c.count}</li>)}</ul></>}
      </details>
    </Notice>}
    {!book && activity!=='overview' && <EmptyState title="No workbook open">Open a local .xlsx file or inspect its package read-only. Source files remain unchanged.</EmptyState>}
    {book && <>
      <div className="reader-file"><strong>{book.displayName}</strong><span>Read-only inspection</span><button disabled={interactionBusy || loading} onClick={() => discardReview.guard(()=>void close())}>Close workbook</button></div>
      <div className="workbook-selectors">{!book.editingAllowed && <StatusBadge tone="unavailable">Inspection only · editing and export unavailable</StatusBadge>}
      <div className="reader-controls"><label>Worksheet <select aria-label="Worksheet" value={sheetId} disabled={interactionBusy || loading} onChange={e => {const id=e.target.value;discardReview.guard(()=>{setView(null);setSheetId(id);setSelected(null);setRow(1);setColumn(1);});}}>{book.worksheets.map(s => <option key={s.id} value={s.id}>{s.name}{s.visibility !== 'Visible' ? ` (${s.visibility})` : ''}</option>)}</select></label><span>{sheet ? (book.compatibility ? 'Dimensions measured on selected-sheet scan only' : `Used extent: ${sheet.lastRow} rows × ${sheet.lastColumn} columns`) : ''}</span></div>
      </div><div className="worksheet-body" hidden={!!activity && activity!=='worksheets'}>
      {book.compatibility && <div className="reader-controls"><label>Start row <input aria-label="Inspection start row" type="number" min="1" max="1048557" value={row} disabled={loading || busy} onChange={e => { const n = Number(e.target.value); if (Number.isInteger(n) && n >= 1 && n <= 1048557) {if(n>maxRow){setExplicitExtent(e=>({...e,row:n+19}));setFullRange(true);}setRow(n);} }} /></label><label>Start column <input aria-label="Inspection start column" type="number" min="1" max="16379" value={column} disabled={loading || busy} onChange={e => { const n = Number(e.target.value); if (Number.isInteger(n) && n >= 1 && n <= 16379) {if(n>maxColumn){setExplicitExtent(e=>({...e,column:n+5}));setFullRange(true);}setColumn(n);} }} /></label></div>}
      {book.compatibility&&<div className="reader-controls"><form onSubmit={e=>{e.preventDefault();const cell=parseCell(goTo);if(cell){setError('');navigate(cell.row,cell.column);}else setError('Enter a cell from A1 to XFD1048576.');}}><label>Go To Cell <input aria-label="Go To Cell" value={goTo} onChange={e=>setGoTo(e.target.value)} placeholder="C52"/></label><button disabled={interactionBusy}>Go</button></form><label><input type="checkbox" checked={fullRange} onChange={e=>{setFullRange(e.target.checked);setExplicitExtent({row:1,column:1});setRow(1);setColumn(1);}}/> Inspect full stored range</label><span>{fullRange?'Stored range':'Useful data'}: {maxRow} rows × {maxColumn} columns</span><p>Internal blank rows remain visible. Bounds include hidden values and formulas. Empty shared-string references may extend the range; objects, merges and formula spill areas are not measured. Display bounds never limit analysis.</p></div>}
      <p className="cached-notice">Stored formula results · may be stale · no recalculation</p>
      {loading && <p aria-live="polite">Loading worksheet…</p>}
      {book.compatibility && selected && <div className="cell-editor" aria-label="Read-only cell details"><strong>Inspect {columnLabel(selected.column)}{selected.row}</strong><p>Raw value: {selected.interpretation?.rawValue ?? selected.value}</p><p>Display value: {selected.interpretation?.displayValue ?? selected.value}</p><p>Type: {selected.interpretation?.resolvedType ?? selected.kind} · Interpretation: {selected.interpretation?.confidence ?? 'Stored'}</p><p>Number format: {selected.format.numberFormat || 'Unspecified'}</p><p>Source format ID: {selected.format.numberFormatId} · Effective format ID: {selected.format.effectiveNumberFormatId ?? 'Unresolved'}</p>{selected.interpretation?.classification && <p>Display classification: {selected.interpretation.classification}</p>}{selected.format.formatResolution && <p>Format resolution: {selected.format.formatResolution}</p>}{selected.formula !== null && <><p>Formula text: {selected.formula || 'Unavailable'}</p><p>Stored cached value (may be stale): {selected.value || 'Unavailable'}</p></>}<p>{selected.interpretation?.formulaStatus}</p>{selected.interpretation?.warning && <p role="note">{selected.interpretation.warning}</p>}</div>}
      {view && <div className={book.compatibility ? "cell-window inspection" : "cell-window"} ref={scroll} tabIndex={0} onKeyDown={keyboard} onScroll={e=>{if(!book.compatibility||interactionBusy)return;const el=e.currentTarget;setRow(Math.min(1048557,Math.max(1,Math.min(maxRow,Math.floor(el.scrollTop/28)+1))));setColumn(Math.min(16379,Math.max(1,Math.min(maxColumn,Math.floor(el.scrollLeft/140)+1))));}} aria-label="Worksheet viewport" style={book.compatibility?{height:620}:undefined}><div className={book.compatibility?'inspection-space':undefined} style={book.compatibility?{height:maxRow*28+620,width:Math.max(900,maxColumn*140+900)}:undefined}><table><caption>{sheet?.name} · Rows {view.startRow}–{book.compatibility?Math.min(maxRow,view.startRow + 19):view.startRow+19}, columns {columnLabel(view.startColumn)}–{columnLabel(Math.min(maxColumn,view.startColumn + 5))}</caption><thead><tr><th scope="col">Row</th>{Array.from({ length: book.compatibility?Math.min(6,maxColumn-view.startColumn+1):6 }, (_, i) => <th scope="col" key={i}>{columnLabel(view.startColumn + i)}</th>)}</tr></thead><tbody>{Array.from({ length: book.compatibility?Math.min(20,maxRow-view.startRow+1):20 }, (_, r) => <tr key={r}><th scope="row">{view.startRow + r}</th>{view.cells.slice(r * 6, r * 6 + 6).filter(cell=>!book.compatibility||cell.column<=maxColumn).map(cell => <td key={cell.column} className={cell.formula ? 'formula-cell' : ''} title={cell.formula ? `=${cell.formula}${cell.truncated ? ' (truncated)' : ''}` : `${cell.value}${cell.truncated ? ' (truncated)' : ''}`} style={{ fontWeight: cell.format.bold ? 600 : undefined, fontStyle: cell.format.italic ? 'italic' : undefined }}>{book.compatibility ? <button className="cell-select" disabled={busy || loading} aria-label={`Inspect ${columnLabel(cell.column)}${cell.row}`} aria-pressed={selected?.row===cell.row&&selected.column===cell.column} onClick={() => setSelected(cell)}>{cell.interpretation?.displayValue ?? (cell.value || (cell.formula ? `=${cell.formula}` : cell.interpretation?.formulaStatus.startsWith('Unsupported') ? 'Unsupported formula' : ''))}{cell.truncated ? '…' : ''}</button> : <>{cell.formula ? <><span className="formula-marker">ƒ</span>{cell.value || <span className="formula-text">={cell.formula}</span>}</> : cell.value}{cell.truncated ? '…' : ''}</>}</td>)}</tr>)}</tbody></table></div></div>}
      <div className="reader-paging"><button disabled={busy || loading || row <= 1} onClick={() => { setRow(r => Math.max(1, r - 20)); }}>Previous rows</button><button disabled={busy || loading || row + 20 > maxRow || row >= 1048557} onClick={() => { setRow(r => Math.min(r + 20, 1048557)); }}>Next rows</button><button disabled={busy || loading || column <= 1} onClick={() => { setColumn(c => Math.max(1, c - 6)); }}>Previous columns</button><button disabled={busy || loading || column + 6 > maxColumn || column >= 16379} onClick={() => { setColumn(c => Math.min(c + 6, 16379)); }}>Next columns</button></div>
      {book.compatibility && <details className="compatibility"><summary>Compatibility Report — {book.displayName}</summary><p>{book.worksheets.length} worksheets/tabs · {book.compatibility.fileBytes.toLocaleString()} file bytes · {book.compatibility.compressedPartBytes.toLocaleString()} compressed part bytes · {book.compatibility.expandedPartBytes.toLocaleString()} declared expanded bytes · {book.compatibility.partCount} parts</p><p>{book.compatibility.countStatus}</p><p>{book.compatibility.definedNames} defined names · {book.compatibility.relationships} relationships · External links: {book.compatibility.externalLinks ? 'Present; never resolved' : 'Not observed in relationships'}</p><ul>{book.compatibility.categories.map(c => <li key={c.category}>{c.category}: {c.count}</li>)}</ul><ul>{book.compatibility.capabilities.map(c => <li key={c.operation}>{c.operation}: {c.allowed ? 'Available within inspection budgets' : 'Unavailable'}. {c.reason}</li>)}</ul><p>This inspection project has no write authority. Existing package content is never repaired, removed or simplified by inspection.</p></details>}
      {view?.scan && <details className="technical-details" aria-label="Selected worksheet diagnostics"><summary>Advanced worksheet diagnostics</summary><p>Complete selected-sheet scan: {view.scan.storedCells.toLocaleString()} stored cell elements · {view.scan.nonemptyValues.toLocaleString()} nonempty stored values · {view.scan.formulas.toLocaleString()} formulas · {view.scan.styledCells.toLocaleString()} cells with explicit style attributes. Stored extent: {view.scan.lastRow} × {view.scan.lastColumn}.</p><p>Declared dimension: {view.scan.declaredDimension}. {view.scan.dimensionReliability}. Potential inflated range: {view.scan.potentialInflation ? 'Yes (heuristic)' : 'Not observed'}.</p><p>Select a cell to compare raw values, supported display interpretation and formula-cache warnings.</p>{view.scan.warnings.map(w => <p key={w}>{w}</p>)}</details>}
      <details className="compatibility"><summary>Compatibility findings ({book.findings.length})</summary><ul>{book.findings.map(f => <li key={f.code}>{f.message}</li>)}</ul><p>Creator: {book.creator || 'Not specified'}. Title: {book.title || 'Not specified'}.</p></details>
      <details className="worksheet-tools" open={!activity}><summary>Section and planning review</summary>
      {book.compatibility && bridge && <SectionReview key={`${book.sessionId}:${sheetId}`} bridge={bridge} book={book} sheetId={sheetId} disabled={interactionBusy || loading} onStatus={reviewStatus} onNavigate={navigate}/>}
      </details>
      </div>
    </>}
  </section></div>;
}
