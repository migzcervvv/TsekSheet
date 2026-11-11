import { useCallback, useEffect, useRef, useState } from 'react';
import { BridgeError, BridgeTransportError } from './bridge/client';
import type { BridgeClient } from './bridge/client';
import type { ReceiptQueueState } from './bridge/receipts';
import type { ExcelCatalog, ExcelChange, ExcelPreview, ExcelWorkbook } from './bridge/excel';
import type { WorkflowProfile } from './bridge/workflows';
import SourcePreview from './SourcePreview';
import { Notice } from './ui/Workspace';

type Destination = { workbookId:string; sheet:string; sectionId:string };
const emptyDestination:Destination = {workbookId:'',sheet:'',sectionId:''};
function column(n:number){let s='';while(n){n--;s=String.fromCharCode(65+n%26)+s;n=Math.floor(n/26);}return s;}

export default function FastReceiptLoop({bridge,profiles,disabled,renderingAvailable,onActiveChange,activeWorkbook,onProtectedChange}:{
  bridge:BridgeClient; profiles:WorkflowProfile[]; disabled:boolean; renderingAvailable:boolean;
  activeWorkbook?:ExcelWorkbook; onProtectedChange?:(protectedWork:boolean)=>void; onActiveChange?:(active:boolean)=>void;
}) {
  const [queue,setQueue] = useState<ReceiptQueueState|null>(null);
  const [profileId,setProfileId] = useState(profiles.find(p=>p.id==='exti-liquidation')?.id??profiles[0]?.id??'');
  const [profileChoice,setProfileChoice] = useState(profileId);
  const [account,setAccount] = useState('');
  const [focusedField,setFocusedField] = useState('');
  const [drafts,setDrafts] = useState<Record<string,string>>({});
  const cache = useRef<Record<string,Record<string,string>>>({});
  const bindings = useRef<Record<string,Destination>>({});
  const unknownSubmissions = useRef(new Set<string>());
  const [catalog,setCatalog] = useState<ExcelCatalog>({workbooks:[],sections:[],changes:[]});
  const initialDestination = {...emptyDestination,workbookId:activeWorkbook?.id??''};
  const [choice,setChoice] = useState<Destination>(initialDestination), [context,setContext] = useState<Destination>(initialDestination);
  const [preview,setPreview] = useState<ExcelPreview|null>(null);
  const [busy,setBusy] = useState(true);
  const proposalEpoch = useRef(0);
  const pendingChecks = useRef<{remaining:number}|null>(null);
  const [error,setError] = useState(''), [errorDetails,setErrorDetails] = useState(''), [notice,setNotice] = useState('');
  const lock = useRef(true), editRevision = useRef(0);
  const invalidate = useCallback(()=>{editRevision.current++;setPreview(null);},[]);
  const receipt = queue?.receipt, active = queue?.items.find(i=>i.id===queue.activeId);
  const profile = profiles.find(p=>p.id===(receipt?.version?.profileId??profileId));
  const destination = queue?.activeId ? bindings.current[queue.activeId]??context : context;
  const book = catalog.workbooks.find(w=>w.id===choice.workbookId);
  const selectedBook = catalog.workbooks.find(w=>w.id===destination.workbookId);
  const blocked = busy||disabled, inserted = active?.status==='Inserted';
  const changedDestination = JSON.stringify(choice)!==JSON.stringify(destination);
  const missingFields = profile?.fields.filter(f=>f.required&&!drafts[f.key]?.trim())??[];
  const uncertain = !!queue?.activeId&&unknownSubmissions.current.has(queue.activeId);
  const reason = inserted ? 'This receipt is already inserted. Review its highlighted row in Excel.'
    : uncertain ? 'The insertion result is unknown. Check Excel and refresh the queue. Do not submit this receipt again.'
    : busy && pendingChecks.current===null ? 'Finishing the current receipt action…' : disabled ? 'Receipt processing is temporarily unavailable.'
    : !profile ? 'Choose a workflow profile.' : profileChoice!==receipt?.version?.profileId ? 'Apply the profile change before checking the row.'
    : !destination.workbookId||!destination.sheet||!destination.sectionId||changedDestination ? 'Choose a sheet and section, then use that destination for this receipt.'
    : missingFields.length ? 'Complete: '+missingFields.map(f=>f.header).join(', ')+'.' : '';

  function showError(e:unknown, insertion=false){
    setErrorDetails(e instanceof BridgeError?[e.code,...e.details.map(d=>d.code+': '+d.message)].join('\n'):e instanceof Error?e.message:String(e));
    setError(e instanceof BridgeError?e.message:insertion?'The row was not confirmed in the app. Check Excel before continuing.':
      'This action could not finish. Your drafts are retained. Review the destination or refresh the queue.');
  }
  async function adopt(next:ReceiptQueueState, start=true){
    // Adopt the queue even if review startup fails, so imported receipts remain recoverable.
    setQueue(next);invalidate();
    if(next.activeId&&start&&!next.receipt.version){
      next={...next,receipt:await bridge.request('receipt.start',{profileId,pageNumber:1}).promise};
    }
    setQueue(next);setProfileChoice(next.receipt.version?.profileId??profileId);
    if(next.activeId){
      const previous=cache.current[next.activeId];
      const values={...Object.fromEntries(next.receipt.fields.map(f=>[f.key,f.value||(!previous&&profile?.fields.find(p=>p.key===f.key)?.kind==='accountChoice'?account:'')])),...previous};
      if(next.receipt.version)cache.current[next.activeId]=values;
      setDrafts(values);
      const target=bindings.current[next.activeId]??context;setChoice(target);
      if(target.workbookId&&!bindings.current[next.activeId])bindings.current[next.activeId]={...target};
    }
  }
  useEffect(()=>{
    let current=true;
    void bridge.request('receipt.queue',{}).promise.then(q=>{if(current)return adopt(q);}).catch(e=>{if(current)showError(e);}).finally(()=>{if(current){lock.current=false;setBusy(false);}});
    return ()=>{current=false;};
  },[bridge]);
  useEffect(()=>{onActiveChange?.(!!receipt?.source);},[!!receipt?.source,onActiveChange]);
  useEffect(()=>{onProtectedChange?.(busy||!!queue?.items.length);},[busy,queue?.items.length,onProtectedChange]);
  useEffect(()=>{
    if(!activeWorkbook||!receipt?.version)return;
    let current=true;
    void bridge.request('excel.listWorkbooks',{open:false}).promise.then(c=>{if(current)setCatalog(c);}).catch(e=>{if(current)showError(e);});
    return ()=>{current=false;};
  },[bridge,activeWorkbook?.id,receipt?.version?.profileId]);
  const sourceBinding=JSON.stringify([queue?.activeId,receipt?.source?.document.version,receipt?.pageNumber]);
  useEffect(()=>{invalidate();},[sourceBinding,invalidate]);
  async function run(action:()=>Promise<void>){
    if(lock.current)return;lock.current=true;setBusy(true);setError('');setErrorDetails('');invalidate();
    try{await action();}catch(e){showError(e);}finally{lock.current=false;setBusy(false);}
  }
  function edit(key:string,value:string){
    const next={...drafts,[key]:value};setDrafts(next);
    if(queue?.activeId)cache.current[queue.activeId]=next;invalidate();
  }
  function move(targetId:string,skip=false){if(!queue?.activeId)return;void run(async()=>adopt(await bridge.request('receipt.navigate',{expectedId:queue.activeId!,targetId,skip}).promise));}
  async function check(){
    if(reason||!receipt?.version||!queue)return;
    if(pendingChecks.current!==null){
      pendingChecks.current.remaining++;
      return;
    }
    if(lock.current)return;
    proposalEpoch.current++;
    const checks={remaining:1};
    pendingChecks.current=checks;
    await run(async()=>{
      const generation=editRevision.current;
      let expected=receipt.version!;
      try {
        while(checks.remaining>0 && generation===editRevision.current){
          checks.remaining--;
          setError('');setErrorDetails('');
          try {
            const state=await bridge.request('receipt.prepare',{expected,values:drafts}).promise;
            expected=state.version!;
            setQueue({...queue,receipt:state});
            if(generation!==editRevision.current)break;
            const result=await bridge.request('excel.previewDraft',{expected,...destination}).promise;
            if(generation!==editRevision.current)break;
            if(result.workbookId!==destination.workbookId||result.sheet!==destination.sheet||result.sectionId!==destination.sectionId)
              throw new Error('The preview destination changed. Check the destination again.');
            if(checks.remaining===0)setPreview(result);
          } catch(e) { showError(e); }
        }
        if(checks.remaining>0 && generation!==editRevision.current)
          setError('The source or draft changed. Check the row again with the current values.');
      } finally { pendingChecks.current=null; }
    });
  }
  async function add(){
    if(!preview||!receipt?.version||!queue?.activeId||uncertain||inserted)return;
    const checked=preview, id=queue.activeId;
    await run(async()=>{
      try{
        const result=await bridge.request('excel.addRow',{expected:receipt.version!,previewToken:checked.token,queueId:id}).promise;
        setCatalog(c=>({...c,changes:[...c.changes,result.change]}));
        setNotice(`Added ${active?.displayName} to ${result.change.workbook} · ${result.change.sheet}!${result.change.range}. Save in Excel.`);
        await adopt(result.queue);
      }catch(e){
        if(e instanceof BridgeTransportError&&e.outcomeUnknown)unknownSubmissions.current.add(id);
        showError(e,true);
      }
    });
  }
  const index=queue?.items.findIndex(i=>i.id===queue.activeId)??-1;
  const pending=queue?.items.find((i,n)=>n>index&&i.status==='Pending')??queue?.items.find(i=>i.status==='Pending'&&i.id!==queue.activeId);
  const count=(status:string)=>queue?.items.filter(i=>i.status===status).length??0;
  const sectionLabel=catalog.sections.find(s=>s.id===destination.sectionId)?.label??destination.sectionId;
  const accountField=profile?.fields.find(f=>f.kind==='accountChoice');
  const accountValue=accountField?drafts[accountField.key]??'':'';
  const accountLabel=profile?.accounts?.find(a=>a.column===accountValue);

  return <section className="fast-receipt-loop unified-receipts" aria-label="Fast receipt entry" aria-busy={busy} data-mode={preview?'check':'edit'}>
    <header className="receipt-toolbar"><div><h2>Receipt queue</h2><p aria-label="Queue progress">{count('Pending')} Pending · {count('Skipped')} Skipped · {count('Inserted')} Inserted</p></div>
      <button className={!queue?.items.length||queue.items.every(i=>i.status==='Inserted')?'primary':undefined} disabled={blocked||!queue} onClick={()=>void run(async()=>adopt(await bridge.request('receipt.enqueue',{}).promise))}>Add receipts</button>
      <details className="queue-tools"><summary>Queue tools</summary><button disabled={blocked} onClick={()=>void run(async()=>adopt(await bridge.request('receipt.queue',{}).promise))}>Refresh queue</button><p>Reviews stay in memory until the app closes.</p></details>
    </header>
    {error&&<Notice kind="error" title="Receipt action blocked"><p>{error}</p><details><summary>Details</summary>{errorDetails}</details></Notice>}
    {notice&&<div role="status" className="semantic-notice success">{notice}</div>}
    {!queue?.items.length&&<div className="empty-state"><h3>Add your receipts</h3><p>Select one or more PDF, PNG or JPEG files.</p></div>}
    {!!queue?.items.length&&<>
      <nav className="queue-navigation" aria-label="Receipt navigation"><button disabled={blocked||index<=0} onClick={()=>move(queue.items[index-1].id)}>Previous receipt</button>
        <label>Receipt {index+1} of {queue.items.length}<select aria-label="Review receipt" value={queue.activeId??''} disabled={blocked} onChange={e=>move(e.target.value)}>{queue.items.map(i=><option key={i.id} value={i.id}>{i.displayName} · {i.status}</option>)}</select></label>
        <button disabled={blocked||index>=queue.items.length-1} onClick={()=>move(queue.items[index+1].id)}>Next receipt</button><button disabled={blocked||inserted} onClick={()=>move(pending?.id??queue.activeId!,true)}>Skip receipt</button>
      </nav>
      <section aria-label="Queue destination" className="receipt-destination"><p><strong>{selectedBook?.name??activeWorkbook?.name??'Choose workbook'}</strong> · {destination.sheet||'Choose sheet'} · {sectionLabel||'Choose section'}{profile?.accounts?.length?` · ${accountLabel?accountLabel.label+' ('+accountLabel.column+')':'Choose account'}`:''}</p>
        <details className="receipt-configuration" open={!destination.sheet||!destination.sectionId||changedDestination||profileChoice!==receipt?.version?.profileId}><summary>Profile and destination</summary>
          <div className="destination-controls">
            <label>Workflow profile<select aria-label="Queue profile" value={profileChoice} disabled={blocked||inserted} onChange={e=>{setProfileChoice(e.target.value);if(!receipt?.version)setProfileId(e.target.value);invalidate();}}>{profiles.map(p=><option key={p.id} value={p.id}>{p.displayName}</option>)}</select></label>
            {!activeWorkbook&&<><button disabled={blocked} onClick={()=>void run(async()=>{const c=await bridge.request('excel.listWorkbooks',{open:true}).promise;setCatalog(c);if(c.openedWorkbookId)setChoice({...emptyDestination,workbookId:c.openedWorkbookId});})}>Open workbook in Excel</button>
              <label>Workbook<select aria-label="Queue workbook" disabled={blocked||inserted} value={choice.workbookId} onChange={e=>{setChoice({...emptyDestination,workbookId:e.target.value});invalidate();}}><option value="">Choose workbook</option>{catalog.workbooks.map(w=><option key={w.id} value={w.id}>{w.name} · {w.id.slice(0,8)}</option>)}</select></label></>}
            <label>Sheet<select aria-label="Queue sheet" disabled={blocked||inserted} value={choice.sheet} onChange={e=>{setChoice({...choice,sheet:e.target.value,sectionId:''});invalidate();}}><option value="">Choose sheet</option>{book?.sheets.map(s=><option key={s}>{s}</option>)}</select></label>
            <label>Section<select aria-label="Queue section" disabled={blocked||inserted} value={choice.sectionId} onChange={e=>{setChoice({...choice,sectionId:e.target.value});invalidate();}}><option value="">Choose section</option>{catalog.sections.filter(s=>!s.sheetName||s.sheetName===choice.sheet).map(s=><option key={s.id} value={s.id}>{s.label}</option>)}</select></label>
          </div>
          {profileChoice!==receipt?.version?.profileId&&receipt?.version&&<button disabled={blocked||inserted} onClick={()=>void run(async()=>{
            const state=await bridge.request('receipt.changeProfile',{expected:receipt.version!,profileId:profileChoice,values:drafts}).promise;
            setProfileId(profileChoice);setQueue({...queue,receipt:state});const values=Object.fromEntries(state.fields.map(f=>[f.key,f.value]));setDrafts(values);cache.current[queue.activeId!]=values;
            delete bindings.current[queue.activeId!];setContext(initialDestination);setChoice(initialDestination);setCatalog(c=>({...c,sections:[]}));setAccount('');
          })}>Change profile for this receipt and new receipts</button>}
          <div className="actions"><button disabled={blocked||inserted||!choice.workbookId||!choice.sheet||!choice.sectionId} onClick={()=>{bindings.current[queue.activeId!]={...choice};setContext({...choice});invalidate();}}>Use destination for this receipt and new receipts</button>
            <button disabled={blocked} onClick={()=>void run(async()=>setCatalog(await bridge.request('excel.listWorkbooks',{open:false}).promise))}>Refresh Excel workbooks</button></div>
          {changedDestination&&<p className="destination-warning">Destination change needs confirmation. Previously visited receipts keep their destination.</p>}
        </details>
      </section>
      {!receipt?.version&&<div className="actions"><button className="primary" disabled={blocked||!profileId} onClick={()=>void run(async()=>adopt(queue))}>Start receipt review</button><p>Choose a profile, then start this receipt. Existing drafts are retained.</p></div>}
      {receipt?.version&&<div className="receipt-layout">
        <aside className="source-inspector"><h3>{receipt.source?.displayName}</h3>{receipt.source&&renderingAvailable?<SourcePreview key={queue.activeId} bridge={bridge} source={receipt.source} association={receipt.pageNumber} activeValue={drafts[focusedField]} onWord={text=>{const match=profile?.fields.find(f=>drafts[f.key]?.toLowerCase().includes(text.toLowerCase()));if(match){invalidate();setFocusedField(match.key);setTimeout(()=>document.getElementById(`queue-${match.key}`)?.focus(),0);}}} getProposalEpoch={()=>proposalEpoch.current} onSourceChange={invalidate} onProposals={inserted?undefined:values=>{
          const next={...drafts};for(const f of profile?.fields??[]){if(!next[f.key]&&values[f.key])next[f.key]=values[f.key].slice(0,f.maxLength);}
          setDrafts(next);if(queue.activeId)cache.current[queue.activeId]=next;invalidate();
        }}/>:<p>Source rendering unavailable. Review the original locally and enter the fields.</p>}</aside>
        <div className="receipt-editor">
          {!preview?<><div className="receipt-edit-heading"><h3>Edit</h3><span>{active?.status}{!inserted?' · Unverified drafts':''}</span></div>
            <div className="receipt-fields">{profile?.fields.map(f=><div className="receipt-field" key={f.key}><label htmlFor={`queue-${f.key}`}>{f.header}{f.required?' (required)':''}</label>{f.kind==='accountChoice'?<select id={`queue-${f.key}`} aria-label={`Queue ${f.key}`} disabled={blocked||inserted} value={drafts[f.key]??''} onFocus={()=>setFocusedField(f.key)} onChange={e=>{edit(f.key,e.target.value);setAccount(e.target.value);}}><option value="">Choose account</option>{profile.accounts?.map(a=><option key={a.column} value={a.column}>{a.code} · {a.label} · {a.column}</option>)}</select>:<input onFocus={()=>setFocusedField(f.key)} id={`queue-${f.key}`} aria-label={`Queue ${f.key}`} disabled={blocked||inserted} maxLength={f.maxLength} value={drafts[f.key]??''} onChange={e=>edit(f.key,e.target.value)}/>}</div>)}</div>
            <div className="receipt-next"><p id="check-reason">{reason||(busy?'Checking the Excel row…':'Check the exact Excel row before confirming insertion.')}</p>{!inserted&&<button className="primary" disabled={!!reason} aria-describedby="check-reason" onClick={()=>void check()}>Check row</button>}</div>
          </>:<section aria-label="Queue row preview" className="receipt-check"><div className="receipt-edit-heading"><h3>Check</h3><span>{active?.displayName}</span></div>
            <dl className="check-destination"><dt>Workbook</dt><dd>{selectedBook?.name??activeWorkbook?.name}</dd><dt>Sheet / section</dt><dd>{preview.sheet} · {sectionLabel}</dd><dt>Account</dt><dd>{accountLabel?accountLabel.label+' · '+accountLabel.column:accountValue||'Not applicable'}</dd><dt>Target row / range</dt><dd>{preview.insertRow} · {preview.sheet}!{preview.range}</dd></dl>
            <div className="mapped-values"><table><caption>Confirm these exact values against the source</caption><thead><tr><th>Field</th><th>Column</th><th>Value</th></tr></thead><tbody>{preview.values.map(v=><tr key={v.key}><th>{v.header}</th><td>{column(v.column)}</td><td>{v.displayValue??String(v.value??'')}</td></tr>)}</tbody></table></div>
            {preview.advisoryWarnings?.map((warning,i)=><Notice key={i} kind="warning" title="Advisory: possible duplicate">{warning}</Notice>)}
            {preview.manualReview.map(w=><Notice key={w.cell} kind="warning" title={`Review ${w.cell} in Excel`}>{w.reason}</Notice>)}
            <p id="insert-confirmation">Add row to Excel confirms these values as human-verified and writes this row to the displayed destination. Save in Excel.</p>
            <div className="actions"><button onClick={invalidate} disabled={blocked}>Back to edit</button><button className="primary" disabled={blocked||uncertain||inserted||preview.manualReview.length>0} aria-describedby="insert-confirmation" onClick={()=>void add()}>Add row to Excel</button></div>
            {preview.manualReview.length>0&&<p>Resolve the warnings in Excel, then return to Edit and check the row again.</p>}
            <details><summary>Details</summary><p>Workbook identity: {preview.workbookId}. Profile: {profile?.displayName}. Review revision: {receipt.version.revision}. Source association: page {receipt.pageNumber}.</p></details>
          </section>}
        </div>
      </div>}
    </>}
    {catalog.changes.length>0&&<details className="inserted-history" aria-label="Inserted queue rows"><summary>Inserted rows ({catalog.changes.length})</summary><ul>{catalog.changes.map((c:ExcelChange,i)=><li key={i}>{c.workbook} · {c.sheet}!{c.range}</li>)}</ul></details>}
  </section>;
}
