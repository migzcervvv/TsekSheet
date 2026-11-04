import SourcePreview from './SourcePreview';
import ExcelInsertion from './ExcelInsertion';
import { EmptyState, StatusBadge, Notice } from './ui/Workspace';
import type { Activity } from './ui/Workspace';
import { useEffect, useRef, useState } from 'react';
import type { BridgeClient } from './bridge/client';
import type { WorkflowProfile } from './bridge/workflows';
import type { ReceiptState, ReceiptField, ReceiptDestination, ReceiptPreview } from './bridge/receipts';

export default function ReceiptReview({ bridge, destination, disabled, renderingAvailable = false, view = 'all', onNavigate, onActiveChange, onStepChange }: { bridge: BridgeClient; destination: Omit<ReceiptDestination, 'profileId' | 'profileHash'> | null; disabled: boolean; renderingAvailable?:boolean; view?: 'all' | 'receipt' | 'placement' | 'workflow'; onActiveChange?:(active:boolean)=>void; onStepChange?:(step:number)=>void; onNavigate?: (activity: Activity) => void }) {
  const [step, setStep] = useState(0);
  const guided = view==='workflow';
  useEffect(()=>{onStepChange?.(step);},[step,onStepChange]);
  const [state, setState] = useState<ReceiptState | null>(null);
  const [profiles, setProfiles] = useState<WorkflowProfile[]>([]);
  const [profileId, setProfileId] = useState('');
  const [page, setPage] = useState(1);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false), [error, setError] = useState(''), [uncertain, setUncertain] = useState(false);
  const [preview, setPreview] = useState<ReceiptPreview | null>(null);
  const [sections, setSections] = useState<ReceiptPreview['mapping']['sections']>([]);
  const [section, setSection] = useState(''), [newEntity, setNewEntity] = useState(false);
  const lock = useRef(false);
  const profile = profiles.find(p => p.id === (state?.version?.profileId ?? profileId));
  useEffect(()=>{onActiveChange?.(!!state?.source);},[!!state?.source,onActiveChange]);
  const destinationKey = JSON.stringify(destination);
  useEffect(() => { setPreview(null); setSections([]); setSection(''); setNewEntity(false); }, [destinationKey]);
  useEffect(() => {
    let active = true;
    void Promise.all([bridge.request('workflow.profiles', {}).promise, bridge.request('receipt.state', {}).promise]).then(([catalog, current]) => {
      if (!active) return;
      setProfiles(catalog.profiles); setProfileId(catalog.profiles[0]?.id ?? ''); adopt(current);
    }).catch(e => { if (active) { setError((e as Error).message); setUncertain(true); } });
    return () => { active = false; };
  }, [bridge]);
  function adopt(current: ReceiptState, preserveDrafts = false) {
    if (!state?.version && current.version) setStep(1);
    if (!current.source) setStep(0);
    setState(current); setPage(current.pageNumber);
    setDrafts(d => Object.fromEntries(current.fields.map(f => {
      const previous = state?.fields.find(old => old.key === f.key);
      return [f.key, preserveDrafts && previous && previous.value === f.value && d[f.key] !== previous.value ? d[f.key] : f.value];
    })));
  }
  async function run(action: () => Promise<ReceiptState | ReceiptPreview>, preserveDrafts = false) {
    if (lock.current) return;
    lock.current = true; setBusy(true); setError(''); setPreview(null);
    try {
      const result = await action();
      if ('proposal' in result) {
        const d = result.mapping.destination;
        if (!destination || d.sessionId !== destination.sessionId || d.worksheetId !== destination.worksheetId || d.revision !== destination.revision
          || d.sourceSha256 !== destination.sourceSha256 || result.mapping.review.reviewId !== state?.version?.reviewId || result.mapping.review.revision !== state?.version?.revision)
          throw new Error('The receipt preview does not match this review and destination.');
        setPreview(result); setSections(result.mapping.sections);
      } else { adopt(result, preserveDrafts); setUncertain(false); if (!result.source) { setSections([]); setSection(''); setNewEntity(false); } }
    }
    catch (e) { setError((e as Error).message); setUncertain(true); }
    finally { lock.current = false; setBusy(false); }
  }
  function fieldAction(action: 'correct' | 'verify' | 'retract', field: ReceiptField) {
    if (!state?.version) return;
    const payload = { expected: state.version, key: field.key, valueRevision: field.valueRevision };
    void run(() => action === 'correct' ? bridge.request('receipt.correct', { ...payload, value: drafts[field.key] ?? '' }).promise : bridge.request(`receipt.${action}`, payload).promise, true);
  }
  const blocked = busy || disabled || uncertain || state?.status === 'Invalidated';
  const dirty = state?.fields.some(f => drafts[f.key] !== f.value) ?? false;
  const boundPreview = preview && destination && preview.mapping.destination.sessionId === destination.sessionId && preview.mapping.destination.worksheetId === destination.worksheetId && preview.mapping.destination.revision === destination.revision ? preview : null;
  return <section className="receipt-review" data-view={view} aria-busy={busy} aria-label="Receipt review">
    <div className="reader-heading"><div><h2>{view==='placement'?'Receipt placement':'Receipt review'}</h2><p>Manual entry · explicit human verification</p></div>
      {!state?.source && <button disabled={busy || disabled || uncertain || !state} onClick={() => void run(() => bridge.request('receipt.select', {}).promise)}>Select receipt PDF or image</button>}
      {state?.source && <button disabled={busy || disabled} onClick={() => void run(() => bridge.request('receipt.close', {}).promise)}>Close receipt and discard review</button>}
      <button disabled={busy || disabled} onClick={() => void run(() => bridge.request('receipt.state', {}).promise)}>Refresh receipt state</button>
    </div>
    {guided && <nav className="receipt-steps" aria-label="Receipt workflow">{['Source','Fields','Destination','Review'].map((label,index)=><button aria-label={label} key={label} data-step={index} aria-current={step===index?'step':undefined} disabled={busy || (index>0 && !state?.version)} onClick={()=>setStep(index)}><span>{index+1}</span>{label}</button>)}</nav>}
    <Notice kind="unavailable" title={renderingAvailable?"Offline OCR is unavailable":"Offline OCR and source rendering are unavailable"}><details><summary>Manual review only</summary>Inspect the original in a local viewer. Values are manually entered, never OCR-verified. Closing discards this in-memory review.</details></Notice>
    {error && <Notice kind="error" title="Review action failed">{error} Refresh receipt state before continuing.</Notice>}
    {state?.status === 'Invalidated' && <Notice kind="error" title="Source changed or became unavailable">Close the receipt and select it again.</Notice>}
    {!state?.source && <EmptyState title={view==='placement'?'No receipt ready for mapping':'Start with a receipt'}>{view==='placement'?'Register a source and verify its manually entered fields in Receipts.':'Select a local PDF, PNG or JPEG. No values are extracted automatically.'}</EmptyState>}
    {view==='placement' && onNavigate && <div className="actions"><button onClick={()=>onNavigate('receipts')}>Review receipt fields</button><button onClick={()=>onNavigate('worksheets')}>Choose destination workbook</button></div>}
    {state?.source && <>
<div className="receipt-context">Source: <strong>{state.source.displayName}</strong><StatusBadge tone="neutral">{state.status==='Invalidated'?'Invalidated':'Registered source'}</StatusBadge></div>
      <div className="receipt-layout"><aside className="source-inspector" hidden={view==='placement' || (guided && step>1)}><h3>Source document</h3><p className="source-name">{state.source.displayName}</p>{renderingAvailable?<SourcePreview bridge={bridge} source={state.source} association={state.pageNumber}/>:<StatusBadge tone="unavailable">Rendering unavailable</StatusBadge>}<details className="source-details"><summary>Source details and fingerprint</summary><dl className="receipt-source"><dt>Source</dt><dd>{state.source.displayName}</dd><dt>Format / size</dt><dd>{['PDF', 'PNG', 'JPEG'][state.source.document.kind]} / {state.source.document.byteLength.toLocaleString()} bytes</dd><dt>SHA-256</dt><dd>{state.source.document.version.sha256}</dd></dl></details></aside><div className="receipt-editor">
      {!state.version ? <div className="actions">
        <label>Workflow profile<select aria-label="Receipt profile" disabled={blocked} value={profileId} onChange={e => setProfileId(e.target.value)}>{profiles.map(p => <option key={p.id} value={p.id}>{p.displayName}</option>)}</select></label>
        <label>Page association<input aria-label="Receipt page" type="number" min="1" max={state.source.document.kind === 0 ? 10000 : 1} disabled={blocked || state.source.document.kind !== 0} value={page} onChange={e => setPage(Number(e.target.value))}/></label>
        <button disabled={blocked || !profileId || !Number.isInteger(page) || page < 1 || page > 10000} onClick={() => void run(() => bridge.request('receipt.start', { profileId, pageNumber: page }).promise)}>Start manual receipt review</button>
      </div> : <>
        <div className="review-summary"><StatusBadge tone={dirty?'warning':'neutral'}>{dirty?'Unsaved field edits':state.fields.filter(f=>f.status==='Verified').length+' of '+state.fields.length+' fields human-verified'}</StatusBadge>{busy && <span>Updating review…</span>}</div>
        <details className="review-binding"><summary>Review configuration and page association</summary>{profile?.displayName} · Page association {state.pageNumber} · Revision {state.version.revision}. {renderingAvailable?'Page association is a review reference; viewing does not verify field values.':'PDF page count and contents are not validated.'}</details>
        <div className="receipt-fields" hidden={view==='placement' || (guided && step!==1)}>{state.fields.map(f => { const config = profile?.fields.find(p => p.key === f.key); const changed = drafts[f.key] !== f.value;
          return <div className="receipt-field" data-status={changed?'Unsaved':f.status} key={f.key}>
            <label htmlFor={`receipt-${f.key}`}>{config?.header ?? f.key}{config?.required ? ' (required)' : ''}<span>{config?.kind === 'date' ? 'YYYY-MM-DD' : config?.kind === 'decimal' || config?.kind === 'money' ? 'Amount as entered' : config?.kind === 'accountChoice' ? 'Account code and destination' : 'Literal text'}</span></label>
            {config?.kind === 'accountChoice' ? <select id={`receipt-${f.key}`} aria-label={`Receipt ${f.key}`} disabled={blocked} value={drafts[f.key] ?? ''} onChange={e => { setDrafts(d => ({ ...d, [f.key]: e.target.value })); setPreview(null); }}><option value="">Choose account</option>{profile?.accounts?.map(a=><option key={a.column} value={a.column}>{a.code} · {a.label} · {a.column}</option>)}</select> : <input id={`receipt-${f.key}`} aria-label={`Receipt ${f.key}`} maxLength={config?.maxLength ?? 256} disabled={blocked} value={drafts[f.key] ?? ''} onChange={e => { setDrafts(d => ({ ...d, [f.key]: e.target.value })); setPreview(null); }}/>}
            <span className="receipt-status">{changed ? 'Unsaved edit — not verified' : f.status === 'Verified' ? 'Human-verified manual entry' : `${f.status} — not verified`}</span>
            <div className="actions"><button disabled={blocked || !changed} onClick={() => fieldAction('correct', f)}>Save {config?.header ?? f.key}</button>
              <button disabled={blocked || changed || f.status === 'Verified' || (!f.value.trim() && (config?.required || f.status === 'Draft'))} onClick={() => fieldAction('verify', f)}>Verify {config?.header ?? f.key}</button>
              {f.status === 'Verified' && <button disabled={blocked || changed} onClick={() => fieldAction('retract', f)}>Retract {config?.header ?? f.key}</button>}</div>
          </div>;
        })}</div>
        {view==='receipt' && onNavigate && <div className="actions workflow-next"><button onClick={()=>onNavigate('worksheets')}>Choose destination workbook</button><button className="primary" onClick={()=>setStep(3)}>Review placement</button></div>}
        {guided && step===2 && <Notice kind="information" title="Choose a workbook in Excel">Open a workbook below and choose its profile section. The preview shows the verified row before insertion.</Notice>}
        {guided && step<3 && state.version && <div className="workflow-next actions"><button disabled={step===0} onClick={()=>setStep(n=>Math.max(0,n-1))}>Previous step</button><button className="primary" onClick={()=>setStep(n=>Math.min(3,n+1))}>Continue to {['Source','Fields','Destination','Review'][step+1]}</button></div>}
        {guided && step===3 && dirty && <Notice kind="warning" title="Unsaved field edits">Save changes and explicitly verify corrected values before requesting a new proposal.</Notice>}
        <div className="receipt-placement" hidden={view==='receipt' || guided}><h3>Worksheet mapping and placement</h3>
          <p>{destination ? 'Destination: selected worksheet and active original or working copy in the workbook viewer.' : 'Open a validated workbook and select a worksheet to preview mapping. Read-only package inspection cannot plan placement.'}</p>
          {sections.length > 0 && <label>Destination section<select aria-label="Receipt destination section" disabled={blocked} value={section} onChange={e => { setSection(e.target.value); setPreview(null); }}><option value="">Resolve from verified entity</option>{sections.map(s => <option key={s.id} value={s.id}>{s.label} · rows {s.firstDataRow}–{s.subtotalRow - 1}</option>)}</select></label>}
          <label><input type="checkbox" checked={newEntity} disabled={blocked} onChange={e => { setNewEntity(e.target.checked); setPreview(null); }}/> Confirm a new entity when no exact match exists</label>
          <button disabled={blocked || !destination || !state.version || dirty} onClick={() => { if (destination && state.version) void run(() => bridge.request('receipt.preview', { expected: state.version!, destination: { ...destination, profileId: state.version!.profileId, profileHash: state.version!.profileHash }, sectionId: section || null, confirmNewEntity: newEntity }).promise); }}>Preview verified receipt placement</button>
          {boundPreview && <div aria-label="Receipt placement preview"><h4>{boundPreview.mapping.worksheetName}</h4>
            <p>Read-only proposal. Workbook cells are unchanged. Insertion is unavailable.</p>
            <div className="mapped-values"><table><caption>Verified values · proposed destination columns</caption><thead><tr><th>Field</th><th>Column</th><th>Value</th></tr></thead><tbody>{boundPreview.mapping.fields.map(f=><tr key={f.value.key}><th scope="row">{f.header}</th><td>{f.column}</td><td>{f.value.literalText||'(absent)'}</td></tr>)}</tbody></table></div>
            {boundPreview.proposal.records.map(r => <div key={r.recordId}><p>{r.vendor} · {r.matchReason}</p><p>Section: {r.sectionLabel ?? 'Choose a destination section'} · Proposed row: {r.proposedRow ?? 'unresolved'}</p>{r.suggestedVendors.length > 0 && <p>Similar entities: {r.suggestedVendors.join(', ')}. Correct the entity field and verify it again to change identity.</p>}{r.duplicates.map((d, i) => <Notice kind="warning" key={i} title={'Potential duplicate at row '+(d.existingRow??'unknown')}>{d.reason}</Notice>)}</div>)}
            {boundPreview.proposal.requiredConfirmations.map(c => <Notice kind="warning" key={c} title="Review required">{c}</Notice>)}
          </div>}
        </div>
      </>}
    </div></div></>}
    {(!guided||step>=2)&&<ExcelInsertion bridge={bridge} review={state} dirty={dirty} disabled={blocked}/>}
  </section>;
}
