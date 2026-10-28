import { useEffect, useRef, useState } from 'react';
import type { BridgeClient } from './bridge/client';
import type { WorkflowProfile, WorkflowPlan } from './bridge/workflows';

export default function PayablesProcessing({ bridge, sessionId, worksheetId, revision, sourceSha256, workbookName, worksheetName, disabled }:
  { bridge: BridgeClient; sessionId: string; worksheetId: string; revision: number; sourceSha256: string; workbookName: string; worksheetName: string; disabled: boolean }) {
  const [profiles,setProfiles]=useState<WorkflowProfile[]>([]); const [profileId,setProfileId]=useState('');
  const [values,setValues]=useState<Record<string,string>>({}); const [plan,setPlan]=useState<WorkflowPlan|null>(null);
  const [busy,setBusy]=useState(false); const [error,setError]=useState('');
  const [outdated,setOutdated]=useState(false);
  const [section,setSection]=useState<string|null>(null); const [vendor,setVendor]=useState<string|null>(null); const [newVendor,setNewVendor]=useState(false);
  const generation=useRef(0); const profile=profiles.find(p=>p.id===profileId); const record=plan?.records[0];
  useEffect(()=>{
    let live=true;
    void bridge.request('workflow.profiles',{}).promise.then(r=>{if(live)setProfiles(r.profiles);}).catch(e=>{if(live)setError((e as Error).message);});
    return ()=>{live=false;generation.current++;};
  },[bridge]);
  useEffect(()=>{generation.current++;setPlan(null);setSection(null);setVendor(null);setNewVendor(false);setBusy(false);},[sessionId,worksheetId,revision,profileId,values]);
  async function preview() {
    if(!profile)return; const current=++generation.current; setBusy(true);setError('');
    try {
      const result=await bridge.request('workbook.planRecords',{sessionId,worksheetId,expectedRevision:revision,profileId:profile.id,profileHash:profile.profileHash,
        records:[{id:'manual-1',values,selectedSectionId:section,selectedVendor:vendor,confirmNewVendor:newVendor}]}).promise;
      if(current!==generation.current)return;
      if(result.sessionId!==sessionId || result.worksheetId!==worksheetId || result.revision!==revision || result.sourceSha256!==sourceSha256
        || result.profileId!==profile.id || result.profileHash!==profile.profileHash || result.records.length!==1 || result.records[0].recordId!=='manual-1')
        throw new Error('The desktop returned a mismatched proposal. Refresh before reviewing it.');
      setPlan(result);setOutdated(false);
    }catch(e){if(current===generation.current){setPlan(null);setError((e as Error).message);}}
    finally{if(current===generation.current)setBusy(false);}
  }
  return <section className="workflow-panel" aria-labelledby="payables-title">
    <h3 id="payables-title">Payables Processing</h3>
    <p>{workbookName} · {worksheetName} · revision {revision}. Manual planning only; no PDF OCR.</p>
    <label>Workflow profile <select aria-label="Workflow profile" disabled={disabled||busy} value={profileId} onChange={e=>{setProfileId(e.target.value);setValues({});setError('');}}>
      <option value="">Choose an approved profile</option>{profiles.map(p=><option key={p.id} value={p.id}>{p.displayName} · v{p.revision}</option>)}</select></label>
    {profile && <div className="workflow-fields">{profile.fields.map(f=><label key={f.key}>{f.header}{f.required?' (required)':''}
      <input aria-label={`Payable ${f.header}`} value={values[f.key]??''} disabled={disabled||busy} maxLength={f.maxLength} inputMode={f.kind==='decimal'?'decimal':'text'}
        placeholder={f.kind==='date'?'YYYY-MM-DD':undefined} onChange={e=>setValues(previous=>({...previous,[f.key]:e.target.value}))}/></label>)}</div>}
    <button disabled={disabled||busy||!profile} onClick={()=>void preview()}>{busy?'Analyzing worksheet…':'Preview payable destination'}</button>
    {error && <p role="alert">{error}</p>}
    {plan && record && <div aria-label="Payable insertion proposal" className="workflow-proposal">
      {outdated && <p role="alert">Choices changed. Preview again to validate the destination below.</p>}
      <strong>{record.vendor} · {record.matchReason}</strong>
      <p>Lookup covers every recognized section. Exact/alias matches: {record.matches.length} section(s).</p>
      <ul>{record.matches.map(m=><li key={m.sectionId}>{m.label}: {m.entries} existing entries, last at row {m.lastRow}</li>)}</ul>
      {record.suggestedVendors.length>0 && <label>Vendor identity <select aria-label="Vendor identity" disabled={disabled||busy} value={newVendor?'__new__':vendor??''}
        onChange={e=>{generation.current++;setOutdated(true);setVendor(e.target.value==='__new__'?null:e.target.value||null);setNewVendor(e.target.value==='__new__');setSection(null);}}>
        <option value="">Choose explicitly, then preview again</option>{record.suggestedVendors.map(name=><option key={name} value={name}>{name}</option>)}<option value="__new__">Retain the submitted name as a different entity</option></select></label>}
      {record.matches.length>1 && <label>Destination section <select aria-label="Destination section" disabled={disabled||busy} value={section??''} onChange={e=>{generation.current++;setOutdated(true);setSection(e.target.value||null);}}>
        <option value="">Choose explicitly, then preview again</option>{record.matches.map(m=><option key={m.sectionId} value={m.sectionId}>{m.label} · after row {m.lastRow}</option>)}</select></label>}
      <p>{record.sectionLabel?`Proposed destination: ${record.sectionLabel}; insert at base row ${record.baseInsertionRow}, proposed final row ${record.proposedRow}. Existing rows through ${record.affectedThroughRow} would shift.`:'Resolve the entity and section choices before a destination can be proposed.'}</p>
      <table><caption>Proposed field mappings and values</caption><thead><tr><th>Field</th><th>Column</th><th>Value</th></tr></thead><tbody>{record.fields.map(f=><tr key={f.key}><td>{f.header}</td><td>{f.column}</td><td>{f.value}</td></tr>)}</tbody></table>
      {record.duplicates.length>0 && <div role="alert"><strong>Potential duplicates — no record was discarded</strong><ul>{record.duplicates.map((d,i)=><li key={i}>{d.existingRow?`Existing row ${d.existingRow}`:`Batch record ${d.batchRecordId}`}: {d.reason}</li>)}</ul></div>}
      <p>Required review:</p><ul>{plan.requiredConfirmations.map(c=><li key={c}>{c}</li>)}</ul>
      <p>Integrity concerns:</p><ul>{plan.integrityConcerns.map(c=><li key={c}>{c}</li>)}</ul>
      <button disabled>Apply row insertion — unavailable</button><p>{plan.insertionBlockedReason}</p>
    </div>}
  </section>;
}
