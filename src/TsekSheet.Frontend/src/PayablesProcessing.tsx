import { useEffect, useRef, useState } from 'react';
import type { BridgeClient } from './bridge/client';
import type { WorkflowProfile, WorkflowPlan, WorkflowRequest, CandidateReview } from './bridge/workflows';

export default function PayablesProcessing({ bridge, sessionId, worksheetId, revision, sourceSha256, workbookName, worksheetName, disabled, destinationSessionId, destinationRevision, isWorkingCopy = false }:
  { bridge: BridgeClient; sessionId: string; worksheetId: string; revision: number; sourceSha256: string; workbookName: string; worksheetName: string; disabled: boolean; destinationSessionId?:string; destinationRevision?:number; isWorkingCopy?:boolean }) {
  const [profiles,setProfiles]=useState<WorkflowProfile[]>([]); const [profileId,setProfileId]=useState('');
  const [values,setValues]=useState<Record<string,string>>({}); const [plan,setPlan]=useState<WorkflowPlan|null>(null);
  const [busy,setBusy]=useState(false); const [error,setError]=useState('');
  const [outdated,setOutdated]=useState(false);
  const [plannedRequest,setPlannedRequest]=useState<WorkflowRequest|null>(null);
  const [candidate,setCandidate]=useState<CandidateReview|null>(null); const candidateRef=useRef<CandidateReview|null>(null);
  const cancellation=useRef<Promise<void>>(Promise.resolve());
  const [acknowledged,setAcknowledged]=useState(false); const [duplicatesReviewed,setDuplicatesReviewed]=useState(false);
  function discardCandidate():Promise<void> {
    const old=candidateRef.current; candidateRef.current=null; setCandidate(null); setAcknowledged(false); setDuplicatesReviewed(false);
    if(old)cancellation.current=cancellation.current.then(()=>bridge.request('workbook.cancelCandidate',{sessionId:old.proposal.sessionId,candidateId:old.candidateId,expectedRevision:old.proposal.revision}).promise).then(()=>{},()=>{});
    return cancellation.current;
  }
  const [section,setSection]=useState<string|null>(null); const [vendor,setVendor]=useState<string|null>(null); const [newVendor,setNewVendor]=useState(false);
  const generation=useRef(0); const profile=profiles.find(p=>p.id===profileId); const record=plan?.records[0];
  useEffect(()=>{
    let live=true;
    void bridge.request('workflow.profiles',{}).promise.then(r=>{if(live)setProfiles(r.profiles);}).catch(e=>{if(live)setError((e as Error).message);});
    return ()=>{live=false;generation.current++;const old=candidateRef.current;if(old)void bridge.request('workbook.cancelCandidate',{sessionId:old.proposal.sessionId,candidateId:old.candidateId,expectedRevision:old.proposal.revision}).promise.catch(()=>{});};
  },[bridge]);
  useEffect(()=>{generation.current++;setPlan(null);setPlannedRequest(null);setSection(null);setVendor(null);setNewVendor(false);setBusy(false);discardCandidate();},[sessionId,worksheetId,revision,profileId,values,destinationRevision]);
  async function preview() {
    if(!profile)return; const current=++generation.current; setBusy(true);setError('');
    try {
      await discardCandidate();if(current!==generation.current)return;
      const request:WorkflowRequest={sessionId,worksheetId,expectedRevision:revision,profileId:profile.id,profileHash:profile.profileHash,
        records:[{id:'manual-1',values,selectedSectionId:section,selectedVendor:vendor,confirmNewVendor:newVendor}]};
      const result=await bridge.request('workbook.planRecords',request).promise;
      if(current!==generation.current)return;
      if(result.sessionId!==sessionId || result.worksheetId!==worksheetId || result.revision!==revision || result.sourceSha256!==sourceSha256
        || result.profileId!==profile.id || result.profileHash!==profile.profileHash || result.records.length!==1 || result.records[0].recordId!=='manual-1')
        throw new Error('The desktop returned a mismatched proposal. Refresh before reviewing it.');
      setPlan(result);setPlannedRequest(request);setOutdated(false);
    }catch(e){if(current===generation.current){setPlan(null);setError((e as Error).message);}}
    finally{if(current===generation.current)setBusy(false);}
  }
  function checkCandidate(result:CandidateReview) {
    if(result.proposal.sessionId!==sessionId || result.proposal.worksheetId!==worksheetId || result.proposal.revision!==revision
      || result.proposal.sourceSha256!==sourceSha256 || result.proposal.profileHash!==profile?.profileHash || result.proposal.profileId!==profile?.id
      || result.destinationSessionId!==destinationSessionId || result.expectedDestinationRevision!==destinationRevision
      || JSON.stringify(result.proposal.records)!==JSON.stringify(plan?.records))
      throw new Error('The candidate identity or revision differs from this review. Generate a new candidate.');
  }
  async function generateCandidate() {
    if(!plannedRequest||!destinationSessionId||destinationRevision===undefined||!isWorkingCopy)return;
    const current=++generation.current;setBusy(true);setError('');
    try {
      await discardCandidate();if(current!==generation.current)return;
      const result=await bridge.request('workbook.generateCandidate',{...plannedRequest,destinationSessionId,expectedDestinationRevision:destinationRevision}).promise;
      if(current!==generation.current){void bridge.request('workbook.cancelCandidate',{sessionId:result.proposal.sessionId,candidateId:result.candidateId,expectedRevision:result.proposal.revision}).promise.catch(()=>{});return;}
      checkCandidate(result); candidateRef.current=result;setCandidate(result);
    }catch(e){if(current===generation.current)setError((e as Error).message);}
    finally{if(current===generation.current)setBusy(false);}
  }
  async function approveCandidate() {
    if(!candidate||!acknowledged||candidate.rejectionReasons.length)return;
    const reviewed=candidate;const current=++generation.current;setBusy(true);setError('');
    try {
      const result=await bridge.request('workbook.approveCandidate',{sessionId,worksheetId,destinationSessionId:reviewed.destinationSessionId,
        candidateId:reviewed.candidateId,reviewSha256:reviewed.reviewSha256,candidateSha256:reviewed.candidateSha256,manifestSha256:reviewed.manifestSha256,
        sourceSha256,expectedCopyRevision:revision,expectedDestinationRevision:reviewed.expectedDestinationRevision,profileId:reviewed.proposal.profileId,profileHash:reviewed.proposal.profileHash,duplicatesReviewed}).promise;
      if(current!==generation.current){void bridge.request('workbook.cancelCandidate',{sessionId:result.proposal.sessionId,candidateId:result.candidateId,expectedRevision:result.proposal.revision}).promise.catch(()=>{});return;}
      checkCandidate(result);
      if(result.candidateId!==reviewed.candidateId||result.reviewSha256!==reviewed.reviewSha256||result.candidateSha256!==reviewed.candidateSha256||result.manifestSha256!==reviewed.manifestSha256||result.lifecycle.approval!=='approved')throw new Error('Approval returned a different candidate. Review invalidated.');
      candidateRef.current=result;setCandidate(result);
    }catch(e){if(current===generation.current){discardCandidate();setError((e as Error).message);}}
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
        onChange={e=>{generation.current++;discardCandidate();setOutdated(true);setVendor(e.target.value==='__new__'?null:e.target.value||null);setNewVendor(e.target.value==='__new__');setSection(null);}}>
        <option value="">Choose explicitly, then preview again</option>{record.suggestedVendors.map(name=><option key={name} value={name}>{name}</option>)}<option value="__new__">Retain the submitted name as a different entity</option></select></label>}
      {record.matches.length>1 && <label>Destination section <select aria-label="Destination section" disabled={disabled||busy} value={section??''} onChange={e=>{generation.current++;discardCandidate();setOutdated(true);setSection(e.target.value||null);}}>
        <option value="">Choose explicitly, then preview again</option>{record.matches.map(m=><option key={m.sectionId} value={m.sectionId}>{m.label} · after row {m.lastRow}</option>)}</select></label>}
      <p>{record.sectionLabel?`Proposed destination: ${record.sectionLabel}; insert at base row ${record.baseInsertionRow}, proposed final row ${record.proposedRow}. Existing rows through ${record.affectedThroughRow} would shift.`:'Resolve the entity and section choices before a destination can be proposed.'}</p>
      <table><caption>Proposed field mappings and values</caption><thead><tr><th>Field</th><th>Column</th><th>Value</th></tr></thead><tbody>{record.fields.map(f=><tr key={f.key}><td>{f.header}</td><td>{f.column}</td><td>{f.value}</td></tr>)}</tbody></table>
      {record.duplicates.length>0 && <div role="alert"><strong>Potential duplicates — no record was discarded</strong><ul>{record.duplicates.map((d,i)=><li key={i}>{d.existingRow?`Existing row ${d.existingRow}`:`Batch record ${d.batchRecordId}`}: {d.reason}</li>)}</ul></div>}
      <p>Required review:</p><ul>{plan.requiredConfirmations.map(c=><li key={c}>{c}</li>)}</ul>
      <p>Integrity concerns:</p><ul>{plan.integrityConcerns.map(c=><li key={c}>{c}</li>)}</ul>
      <p aria-label="Structural insertion status">{plan.structuralStatus === 'experimental-envelope-only' ? 'Experimental package preflight passed; candidate validation pending.'
        : plan.structuralStatus === 'unsupported-structure' ? 'Unsupported workbook structure.'
        : plan.structuralStatus === 'ambiguous-destination' ? 'Ambiguous destination; explicit choices required.'
        : plan.structuralStatus === 'duplicate-review-required' ? 'Duplicate warning requires review.'
        : plan.structuralStatus === 'stale-proposal' ? 'Stale proposal.' : 'Production insertion is disabled.'}</p>
      {!candidate && <div aria-label="Candidate lifecycle review">
        <p>Planning {plan.review.planner === 'resolved' ? 'resolved' : 'requires choices'}; preflight: {plan.structuralStatus === 'experimental-envelope-only' ? 'eligible for experimental candidate construction' : 'blocked or pending review'}.</p>
        <p>Candidate: not generated. Preservation oracle: not run. User approval: not requested. Working-copy adoption: not performed.</p>
        <p>The proposal is bound to revision {plan.revision}. A generated candidate must identify its exact bytes and intended resulting revision before approval.</p>
        <p>Formula and subtotal impacts: pending candidate comparison. Styling requires a uniform reviewed template with unrelated invoice, RFP and payment values cleared.</p>
        <p>Structural integration: production disabled. Export excludes unmerged records. Apply remains blocked until the approved lifecycle and export are independently verified.</p>
      </div>}
      <button disabled={disabled||busy||outdated||!plan.readyForReview||!isWorkingCopy||!destinationSessionId} onClick={()=>void generateCandidate()}>Generate candidate for review</button>
      {!isWorkingCopy&&<p>Create and select an isolated working copy to generate a candidate.</p>}
      {candidate&&<div aria-label="Generated candidate review">
        <strong>Candidate generated · preservation oracle accepted · {candidate.lifecycle.approval==='approved'?'user approved review':'awaiting explicit review approval'}</strong>
        <p>Approval records this review. Workbook changes remain disabled.</p>
        <p>{candidate.workbookName} · {candidate.worksheetName} · profile {candidate.proposal.profileId}. Candidate {candidate.candidateId}</p>
        <p>Insert {candidate.operation.rowCount} row(s) at {candidate.operation.insertionBoundary}; existing rows through {record.affectedThroughRow} shift. Template row {candidate.operation.templateRow}; subtotal row {candidate.operation.subtotalRow}.</p>
        <p>Current copy revision {candidate.proposal.revision}, intended {candidate.intendedWorkingCopyRevision}; current destination revision {candidate.expectedDestinationRevision}, intended {candidate.intendedDestinationRevision}.</p>
        <table><caption>Exact candidate field values</caption><thead><tr><th>Record</th><th>Section</th><th>Field / column</th><th>Value</th></tr></thead><tbody>{candidate.proposal.records.flatMap(r=>r.fields.map(f=><tr key={`${r.recordId}:${f.key}`}><td>{r.vendor}</td><td>{r.sectionLabel}</td><td>{f.header} / {f.column}</td><td>{f.value}</td></tr>))}</tbody></table>
        <p>Formula and subtotal impacts:</p><ul>{candidate.formulaImpacts.map((impact,i)=><li key={i}>{impact}</li>)}</ul>
        <p>Style/template assumptions:</p><ul>{candidate.styleAssumptions.map((assumption,i)=><li key={i}>{assumption}</li>)}</ul>
        <p>Oracle findings: {candidate.oracleReport.accepted?'accepted within the bounded OOXML envelope':'rejected'}. Native formatting approval is checked separately.</p>
        {candidate.rejectionReasons.map((reason,i)=><p role="alert" key={i}>{reason}</p>)}
        <p>Review fingerprint: <span style={{overflowWrap:'anywhere'}}>{candidate.reviewSha256}</span></p>
        <p>Source fingerprint: {candidate.proposal.sourceSha256}. Candidate fingerprint: {candidate.candidateSha256}. Manifest fingerprint: {candidate.manifestSha256}.</p>
        {candidate.lifecycle.approval!=='approved'&&<>
          <label><input type="checkbox" checked={acknowledged} disabled={busy||candidate.rejectionReasons.length>0} onChange={e=>setAcknowledged(e.target.checked)}/>I reviewed these exact values, destination, formulas and styling assumptions.</label>
          {candidate.proposal.records.some(r=>r.duplicates.length>0)&&<label><input type="checkbox" checked={duplicatesReviewed} disabled={busy} onChange={e=>setDuplicatesReviewed(e.target.checked)}/>I explicitly reviewed every duplicate RFP/invoice warning for this candidate.</label>}
          <button disabled={disabled||busy||!acknowledged||candidate.rejectionReasons.length>0||(candidate.proposal.records.some(r=>r.duplicates.length>0)&&!duplicatesReviewed)} onClick={()=>void approveCandidate()}>Approve reviewed candidate</button>
        </>}
        <p>Review only: approval does not apply rows. Working-copy adoption, structural integration and export have not occurred. Production Apply remains disabled pending complete production lifecycle qualification.</p>
      </div>}
      {(candidate||busy)&&<button onClick={()=>{generation.current++;discardCandidate();setBusy(false);}}>Cancel candidate review</button>}
      <button disabled>Apply row insertion — unavailable</button><p>{plan.insertionBlockedReason}</p>
    </div>}
  </section>;
}
