import {useEffect, useRef, useState} from 'react';
import {BridgeError, BridgeTransportError} from './bridge/client';
import type {BridgeClient, Workbook} from './bridge/client';
import {useReviewDiscard} from './useReviewDiscard';
import type {ReviewDecision, ReviewMetrics, ReviewSnapshot} from './bridge/sectionReview';

const kinds:Record<string,string>={header:'Confirm header variant / repeated header',heading:'Confirm heading marker',boundary:'Confirm section ending',parent:'Review parent / child relationship',intermediate:'Intermediate subtotal',summary:'Summary row',exclusion:'Excluded row',investigate:'Requires investigation',identity:'Confirm identity from source evidence',missing:'Information genuinely missing', 'non-record':'Mark as subtotal / non-record',duplicate:'Acknowledge duplicate warning'};
const metrics:([keyof ReviewMetrics,string])[]=[['observedRows','Observed rows'],['capturedRows','Captured rows'],['allRowsCaptured','All observed rows captured'],['roleColumnsCovered','Role columns covered'],['headerRows','Recognized headers'],['recordShapedRows','Record-shaped rows retained'],['ownedRecords','Records with provisional owner'],['uncertainIdentities','Uncertain identities'],['unassignedMeaningfulRows','Unassigned meaningful rows'],['intermediateSubtotals','Reviewed intermediate subtotals'],['unclassifiedSubtotals','Unclassified subtotal candidates'],['summaryRows','Classified summaries'],['boundaryConflicts','Boundary / marker conflicts'],['unresolvedDependencies','Unverified dependencies']];
export default function SectionReview({bridge,book,sheetId,disabled,onNavigate,onStatus}:{bridge:BridgeClient;book:Workbook;sheetId:string;disabled:boolean;onNavigate:(row:number,column:number)=>void;onStatus?:(count:number,busy:boolean,uncertain:boolean)=>void}) {
 const [review,setReview]=useState<ReviewSnapshot|null>(null),[busy,setBusy]=useState(false),[error,setError]=useState(''),[notice,setNotice]=useState('');
 const [kind,setKind]=useState('investigate'),[row,setRow]=useState(1),[section,setSection]=useState(''),[value,setValue]=useState(''),[evidenceRow,setEvidenceRow]=useState(1),[evidenceColumn,setEvidenceColumn]=useState(1),[reason,setReason]=useState(''),[page,setPage]=useState(0),[queueRow,setQueueRow]=useState(0),[duplicate,setDuplicate]=useState(0);
 const [category,setCategory]=useState('Overview'),[guided,setGuided]=useState(false);
 const [unknownCount,setUnknownCount]=useState(0);
 const [requiresRecovery,setRequiresRecovery]=useState(false);
 const recovery=useRef(false), operation=useRef(false);
 const confirmedReview=useRef<{bridge:BridgeClient;sessionId:string;worksheetId:string;decisionCount:number}|null>(null);
 function markUnknown(count:number){recovery.current=true;setRequiresRecovery(true);setUnknownCount(count);setNotice('');}
 function mayHaveApplied(e:unknown){return !(e instanceof BridgeError) && !(e instanceof BridgeTransportError && !e.outcomeUnknown);}
 const decisionCount=Math.max(review?.decisions.length??0,unknownCount);
 const discard=useReviewDiscard(decisionCount,requiresRecovery);
 useEffect(()=>{onStatus?.(decisionCount,busy,requiresRecovery);},[decisionCount,busy,requiresRecovery,onStatus]);
 const alive=useRef(true),generation=useRef(0);
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;generation.current++;
  // Cleanup may release only a confirmed empty review owned by this effect.
  // Read the latest accepted count without re-running cleanup on review updates.
  const confirmed=confirmedReview.current;
  if(!recovery.current&&!operation.current&&confirmed?.decisionCount===0
   &&confirmed.bridge===bridge&&confirmed.sessionId===book.sessionId&&confirmed.worksheetId===sheetId)
   void bridge.request('sectionReview.cancel',{sessionId:book.sessionId,worksheetId:sheetId}).promise.catch(()=>{});
 };},[bridge,book.sessionId,sheetId]);
 function accept(r:ReviewSnapshot,previous:ReviewSnapshot|null) {
  if(r.binding.sessionId!==book.sessionId||r.binding.worksheetId!==sheetId||r.binding.sourceSha256!==book.sourceSha256||r.binding.captureRevision!==book.revision||r.binding.worksheetOrder!==book.worksheets.find(s=>s.id===sheetId)?.order
   ||previous&&(r.binding.reviewId!==previous.binding.reviewId||r.binding.profileSha256!==previous.binding.profileSha256||r.binding.profileVersion!==previous.binding.profileVersion||r.binding.reviewRevision!==previous.binding.reviewRevision+1))throw new Error('Review response identity changed. Close and restart review.');
  confirmedReview.current={bridge,sessionId:r.binding.sessionId,worksheetId:r.binding.worksheetId,decisionCount:r.decisions.length};
  recovery.current=false;setRequiresRecovery(false);setUnknownCount(0);setReview(r);
 }
 function clearPrivateDraft(){setReason('');setValue('');setSection('');setRow(1);setEvidenceRow(1);setEvidenceColumn(1);setQueueRow(0);setDuplicate(0);setPage(0);setKind('investigate');setGuided(false);setCategory('Overview');setUnknownCount(0);}
 async function start(){if(operation.current||recovery.current)return;operation.current=true;const g=++generation.current;setBusy(true);setError('');setNotice('');confirmedReview.current=null;setReview(null);clearPrivateDraft();
  try {const r=await bridge.request('sectionReview.start',{sessionId:book.sessionId,worksheetId:sheetId,expectedRevision:book.revision}).promise;if(!alive.current||g!==generation.current)return;
   if(r.review){accept(r.review,null);setSection(r.review.report.sections[0]?.id??'');setRow(r.review.report.coverage.startRow);}else setNotice('Profile selection cancelled.');
  }catch(e){if(alive.current&&g===generation.current){setError((e as Error).message);if(mayHaveApplied(e))markUnknown(0);}}finally{operation.current=false;if(alive.current&&g===generation.current)setBusy(false);}
 }
 async function cancel(){if(operation.current)return;operation.current=true;const g=++generation.current;setError('');setNotice('');setBusy(true);
  try{await bridge.request('sectionReview.cancel',{sessionId:book.sessionId,worksheetId:sheetId}).promise;
   if(!alive.current||g!==generation.current)return;
   confirmedReview.current=null;setReview(null);clearPrivateDraft();recovery.current=false;setRequiresRecovery(false);setNotice('Review closed. In-memory assertions released. Source unchanged.');
  }catch(e){if(alive.current&&g===generation.current){setError((e as Error).message);if(mayHaveApplied(e))markUnknown(decisionCount);}}finally{operation.current=false;if(alive.current&&g===generation.current)setBusy(false);}
 }
 async function decide(){if(!review||operation.current||recovery.current)return;operation.current=true;setBusy(true);setError('');setNotice('');const g=generation.current;
  const d:ReviewDecision={id:crypto.randomUUID(),kind,row,sectionId:['heading','boundary','parent'].includes(kind)?section:null,value:kind==='duplicate'?review.duplicateKeys[duplicate]:['header','parent'].includes(kind)?value||null:null,evidenceRow:['header','heading','identity'].includes(kind)?kind==='identity'?evidenceRow:row:null,evidenceColumn:['header','heading','identity'].includes(kind)?evidenceColumn:null,reason};
  try{const r=await bridge.request('sectionReview.decide',{binding:review.binding,decision:d}).promise;if(alive.current&&g===generation.current){accept(r,review);setReason('');setNotice('Decision recorded. Source unchanged.');setGuided(kind==='boundary'&&r.originalRecordRows.includes(row));}}catch(e){if(alive.current&&g===generation.current){setError((e as Error).message);if(mayHaveApplied(e))markUnknown(review.decisions.length+1);}}finally{operation.current=false;if(alive.current&&g===generation.current)setBusy(false);}
 }
 async function retract(id:string){if(!review||operation.current||recovery.current)return;operation.current=true;setBusy(true);setError('');setNotice('');const g=generation.current;
  try{const r=await bridge.request('sectionReview.retract',{binding:review.binding,decisionId:id}).promise;if(alive.current&&g===generation.current)accept(r,review);}catch(e){if(alive.current&&g===generation.current){setError((e as Error).message);if(mayHaveApplied(e))markUnknown(review.decisions.length);}}finally{operation.current=false;if(alive.current&&g===generation.current)setBusy(false);}
 }
 function choose(r:number,k='investigate',col=1){setRow(r);setKind(k);setEvidenceColumn(col);onNavigate(r,col);}
 const q=review?.identityQueue.find(x=>x.row===queueRow)??review?.identityQueue[0], warning=review?.report.potentialDuplicates[duplicate];
 const unavailable=busy||disabled||discard.confirming;
 const off=unavailable||requiresRecovery;
 return <section className="workflow-panel section-review" aria-label="Read-only section review">
  <h3>Section Review · read only</h3><p>Worksheet: {book.worksheets.find(s=>s.id===sheetId)?.name}. Section review requires a configured profile. Select a local profile for this worksheet; it stays in memory for this review.</p>
  <button disabled={off} onClick={()=>discard.guard(()=>void start())}>Select profile and start review</button><button disabled={unavailable||(!review&&!requiresRecovery)} onClick={()=>discard.guard(()=>void cancel())}>Close section review</button>
  {discard.dialog}
  {busy&&<p aria-live="polite">Review operation in progress…</p>}{notice&&<p aria-live="polite">{notice}</p>}{error&&<p role="alert">{error}</p>}
  {requiresRecovery&&<p role="status">Review requires recovery. The last confirmed snapshot is retained; the current desktop review revision is unconfirmed. Assertions and retractions are blocked. Explicitly close this review to discard it, then select a profile to start again. Closure must be confirmed by the desktop host.</p>}
  {review&&<>
   <nav className="review-categories" aria-label="Review categories">{['Overview','Sections & Subtotals','Missing Vendors','Potential Duplicates','Unassigned Rows','Review History'].map(c=><button key={c} aria-pressed={category===c} onClick={()=>setCategory(c)}>{c}</button>)}</nav>
   <div hidden={category!=='Overview'}>
   <p>Selected profile: {review.profileId} · version {review.binding.profileVersion} · review revision {review.binding.reviewRevision}</p>
   <p>Configuration reviewed: {String(review.configurationReviewed)} · Record coverage reviewed: {String(review.recordCoverageReviewed)} · Vendor cases reviewed: {String(review.vendorIdentitiesReviewed)} · Bounded hierarchy verified: {String(review.hierarchyVerified)}</p>
   <p role="note">Formula/name/reference preservation NOT verified. Structural insertion NOT authorized. Reviewer assertions do not approve a ledger modification.</p>
   <details><summary>Technical capture details</summary><p>{metrics.map(([key,label])=>`${label}: ${review.before[key]} → ${review.after[key]}`).join(' · ')}</p><p>Capture: {review.captureMilliseconds.toFixed(1)} ms · in-memory reanalysis: {review.reanalysisMilliseconds.toFixed(1)} ms · {review.report.coverage.cellsCaptured} retained coordinates · {review.report.coverage.accountedBytes} accounted bytes · hidden rows included: {review.report.coverage.hiddenRowsIncluded}</p></details>
   <div className="review-scroll"><table aria-label="Review coverage"><thead><tr><th>Independent metric</th><th>Before</th><th>After review</th></tr></thead><tbody>{metrics.filter(([key])=>['recordShapedRows','ownedRecords','uncertainIdentities','unassignedMeaningfulRows','unclassifiedSubtotals','boundaryConflicts'].includes(key)).map(([key,label])=><tr key={key}><th>{label}</th><td>{String(review.before[key])}</td><td>{String(review.after[key])}</td></tr>)}</tbody></table></div>
   </div>
   <div hidden={category!=='Sections & Subtotals'}>
   <details><summary>Headers and mapped roles</summary><p>Header rows: {review.report.headerRows.join(', ')||'None recognized'}</p><p>{Object.entries(review.report.fieldColumns).map(([k,c])=>`${k}: column ${c}`).join(' · ')}</p>{review.report.headerRows.map(r=><button key={r} disabled={off} onClick={()=>choose(r,'header',Object.values(review.report.fieldColumns)[0]??1)}>Header row {r}</button>)}</details>
   <details open><summary>Proposed section hierarchy and boundaries</summary>{review.report.sections.map(s=><div key={s.id}><strong>{s.label}</strong><p>Parent: {s.parentId??'None'} · heading: {s.headingRow??'Ambiguous'} · end: {s.subtotalRow??'Ambiguous'} · {s.status}. Insertion unavailable.</p>{s.headingRow&&<button disabled={off} onClick={()=>{setSection(s.id);choose(s.headingRow!,'heading');}}>Review heading {s.headingRow}</button>}{s.subtotalCandidates.map(r=><button disabled={off} key={r} onClick={()=>{setSection(s.id);choose(r,'boundary');}}>Boundary {s.id} row {r}</button>)}</div>)}</details>
   </div>
   <div hidden={category!=='Missing Vendors'}>
   <details open><summary>Missing / uncertain vendor queue ({review.identityQueue.length})</summary>{q?<><label>Identity case <select aria-label="Identity case" disabled={off} value={q.row} onChange={e=>{const n=Number(e.target.value);setQueueRow(n);choose(n,'identity',review.report.fieldColumns[Object.keys(review.report.fieldColumns)[0]]??1);}}>{review.identityQueue.map(x=><option key={x.row} value={x.row}>Row {x.row} · {x.vendorStatus}</option>)}</select></label><p>Row {q.row} · {q.hidden?'Hidden source row':'Visible source row'} · vendor status: {q.vendorStatus} · provisional section: {q.provisionalSection??'Unassigned'}</p><dl>{Object.entries(q.fields).map(([k,v])=><div key={k}><dt>{k}</dt><dd>{v||'Blank source field'}</dd></div>)}</dl><button disabled={off} onClick={()=>choose(q.row,'identity')}>Review identity row {q.row}</button><p>Confirm only from an explicit captured literal vendor record. Choose evidence coordinates below. Missing information stays unresolved; no vendor is copied automatically. Reviewer-confirmed identity remains separate from the source value.</p></>:<p>No uncertain identity cases detected.</p>}</details>
   </div>
   <div hidden={category!=='Potential Duplicates'}>
   <details open><summary>Potential repeated identifiers ({review.report.potentialDuplicates.length}) and similar names</summary><p>Warnings are not confirmed duplicate transactions. Acknowledgment does not suppress records, prove uniqueness, or permit posting. Missing identities can hide additional matches.</p>{warning&&<><label>Duplicate warning <select aria-label="Duplicate warning" disabled={off} value={duplicate} onChange={e=>setDuplicate(Number(e.target.value))}>{review.report.potentialDuplicates.map((d,i)=><option value={i} key={review.duplicateKeys[i]}>{d.entity} · {d.identifier}</option>)}</select></label><p>Exact normalized vendor basis: {warning.entity} · identifier: {warning.identifier} · roles: {warning.fields.join(', ')} · rows: {warning.rows.join(', ')} · {review.decisions.some(d=>d.kind==='duplicate'&&d.value===review.duplicateKeys[duplicate])?'Warning reviewed; records retained':'Unreviewed warning'}</p>{warning.rows.map(r=><button disabled={off} key={r} onClick={()=>choose(r,'duplicate')}>Inspect duplicate row {r} · section {review.report.records.find(x=>x.row===r)?.sectionId??'Unassigned'}</button>)}</>}{review.report.similarNames.map((x,i)=><p key={i}>{x.firstEntity} / {x.secondEntity}: {x.reason}</p>)}</details>
   </div>
   <div hidden={category!=='Sections & Subtotals'}>
   <details open><summary>Subtotals, summaries and exclusions</summary><p>Summary rows: {review.report.summaryRows.join(', ')||'None'} · excluded rows: {review.report.excludedRows.join(', ')||'None'}</p><label>Subtotal candidate <select aria-label="Subtotal candidate" disabled={off} value="" onChange={e=>{const c=review.report.subtotalCandidates[Number(e.target.value)];if(c)choose(c.row,'intermediate',c.column);}}><option value="">Select a captured formula candidate</option>{review.report.subtotalCandidates.map((c,i)=><option key={i} value={i}>Row {c.row}, column {c.column} · {c.configuredBoundary?'Final boundary candidate':'Unclassified subtotal'} · {c.formulaStatus}</option>)}</select></label><p>{review.report.summaryDependencies.length} known incomplete dependencies. Formula caches are unverified; lexical references do not prove aggregation correctness.</p></details>
   </div>
   <div hidden={category!=='Unassigned Rows'}>
   <details open><summary>Unassigned rows and reasons hierarchy remains unverified</summary><label>Unassigned row <select aria-label="Unassigned row" value="" disabled={off} onChange={e=>choose(Number(e.target.value))}><option value="">Select source row</option>{review.report.unassignedRows.map(r=><option key={r} value={r}>{r}</option>)}</select></label><details><summary>Diagnostic details and all warnings</summary><ul>{review.report.diagnostics.slice(page*40,page*40+40).map((d,i)=><li key={i}>{d.code}: {d.message} {d.row&&<button disabled={off} onClick={()=>choose(d.row!)}>Inspect row {d.row}</button>}{d.sectionId&&` · section ${d.sectionId}`}</li>)}</ul><button disabled={off||page===0} onClick={()=>setPage(p=>p-1)}>Previous diagnostics</button><span> {review.report.diagnostics.length} diagnostics · page {page+1} </span><button disabled={off||(page+1)*40>=review.report.diagnostics.length} onClick={()=>setPage(p=>p+1)}>Next diagnostics</button></details></details>
   </div>
   {guided&&<div className="discard-warning"><p>Section ending confirmed for row {row}. Its record classification is a separate decision. Review the source before classifying it as a subtotal / non-record.</p><button disabled={off} onClick={()=>{setKind('non-record');setGuided(false);}}>Review subtotal / non-record classification</button></div>}
   <div hidden={category==='Overview'||category==='Review History'}><fieldset disabled={off}><legend>Explicit reviewer assertion</legend>
    <label>Decision type <select aria-label="Review decision type" value={kind} onChange={e=>setKind(e.target.value)}>{Object.entries(kinds).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>
    <label>Captured row <input aria-label="Review row" type="number" min={review.report.coverage.startRow} max={review.report.coverage.endRow} value={row} onChange={e=>setRow(Number(e.target.value))}/></label><button onClick={()=>onNavigate(row,evidenceColumn)}>Navigate to review row</button>
    {['heading','boundary','parent'].includes(kind)&&<label>Section <select aria-label="Review section" value={section} onChange={e=>setSection(e.target.value)}>{review.report.sections.map(s=><option key={s.id} value={s.id}>{s.label}</option>)}</select></label>}
    {kind==='header'&&<label>Header role <select aria-label="Header role" value={value} onChange={e=>{setValue(e.target.value);setEvidenceColumn(review.report.fieldColumns[e.target.value]);}}><option value="">Select field role</option>{Object.keys(review.report.fieldColumns).map(k=><option key={k}>{k}</option>)}</select></label>}
    {kind==='parent'&&<label>Reviewed parent <select aria-label="Reviewed parent" value={value} onChange={e=>setValue(e.target.value)}><option value="">No parent</option>{review.report.sections.filter(s=>s.id!==section).map(s=><option key={s.id} value={s.id}>{s.label}</option>)}</select></label>}
    {kind==='identity'&&<label>Literal vendor evidence row <input aria-label="Identity evidence row" type="number" value={evidenceRow} onChange={e=>setEvidenceRow(Number(e.target.value))}/></label>}
    {['header','heading','identity'].includes(kind)&&<label>Source evidence column <select aria-label="Evidence column" value={evidenceColumn} onChange={e=>setEvidenceColumn(Number(e.target.value))}>{review.report.coverage.columns.map(c=><option value={c} key={c}>{c}</option>)}</select></label>}
    {kind==='duplicate'&&<p>Selected warning: {warning?.entity} · {warning?.identifier}</p>}
    <label>Documented reason <textarea aria-label="Review reason" maxLength={512} value={reason} onChange={e=>setReason(e.target.value)}/></label><button disabled={!reason.trim()} onClick={()=>void decide()}>Confirm read-only assertion</button>
   </fieldset></div>
   <div hidden={category!=='Review History'}><details open><summary>Human decisions ({review.decisions.length}) · retractable</summary><ul>{review.decisions.map(d=><li key={d.id}>{kinds[d.kind]} · row {d.row} · {d.reason}{d.kind==='identity'&&` · reviewer evidence row ${d.evidenceRow}, column ${d.evidenceColumn}`}<button disabled={off} onClick={()=>void retract(d.id)}>Retract assertion row {d.row} {d.kind}</button></li>)}</ul></details>
   </div>
   <details><summary>Display limitations</summary><p>Display limits: unsupported effective formats or style inheritance, General/raw values and over-precision numbers retain raw evidence. Formula caches remain unverified. Inspect a source cell for its exact resolution reason.</p></details>
  </>}
 </section>;
}
