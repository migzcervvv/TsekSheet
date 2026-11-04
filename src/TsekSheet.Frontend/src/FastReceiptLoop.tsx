import { useEffect, useRef, useState } from 'react';
import type { BridgeClient } from './bridge/client';
import type { ReceiptQueueState } from './bridge/receipts';
import type { ExcelCatalog, ExcelChange, ExcelPreview } from './bridge/excel';
import type { WorkflowProfile } from './bridge/workflows';
import SourcePreview from './SourcePreview';
import { Notice } from './ui/Workspace';

type Destination = {workbookId:string;sheet:string;sectionId:string};
const emptyDestination:Destination={workbookId:'',sheet:'',sectionId:''};
function column(n:number){let s='';while(n){n--;s=String.fromCharCode(65+n%26)+s;n=Math.floor(n/26);}return s;}

export default function FastReceiptLoop({bridge,profiles,disabled,renderingAvailable,onActiveChange}:{bridge:BridgeClient;profiles:WorkflowProfile[];disabled:boolean;renderingAvailable:boolean;onActiveChange?:(active:boolean)=>void}) {
  const [queue,setQueue]=useState<ReceiptQueueState|null>(null);
  const [profileId,setProfileId]=useState(profiles.find(p=>p.id==='exti-liquidation')?.id??profiles[0]?.id??'');
  const [profileChoice,setProfileChoice]=useState(profileId);
  const [started,setStarted]=useState(false),[account,setAccount]=useState('');
  const [drafts,setDrafts]=useState<Record<string,string>>({});
  const cache=useRef<Record<string,Record<string,string>>>({});
  const bindings=useRef<Record<string,Destination>>({});
  const [catalog,setCatalog]=useState<ExcelCatalog>({workbooks:[],sections:[],changes:[]});
  const [choice,setChoice]=useState<Destination>(emptyDestination),[context,setContext]=useState<Destination>(emptyDestination);
  const [preview,setPreview]=useState<ExcelPreview|null>(null);
  const [busy,setBusy]=useState(false),[error,setError]=useState(''),[notice,setNotice]=useState('');
  const lock=useRef(false);
  const receipt=queue?.receipt, active=queue?.items.find(i=>i.id===queue.activeId);
  const profile=profiles.find(p=>p.id===(receipt?.version?.profileId??profileId));
  const destination=queue?.activeId?bindings.current[queue.activeId]??context:context;
  const book=catalog.workbooks.find(w=>w.id===choice.workbookId);
  const selectedBook=catalog.workbooks.find(w=>w.id===destination.workbookId);
  const blocked=busy||disabled, inserted=active?.status==='Inserted';
  const missingFields=profile?.fields.filter(f=>f.required&&!drafts[f.key]?.trim())??[];
  const requiredMissing=!profile||missingFields.length>0;
  useEffect(()=>{void bridge.request('receipt.queue',{}).promise.then(q=>{setQueue(q);setDrafts(Object.fromEntries(q.receipt.fields.map(f=>[f.key,f.value])));}).catch(e=>setError((e as Error).message));},[bridge]);
  useEffect(()=>{onActiveChange?.(!!receipt?.source);},[!!receipt?.source,onActiveChange]);
  async function run(action:()=>Promise<void>){
    if(lock.current)return;lock.current=true;setBusy(true);setError('');setPreview(null);
    try{await action();}catch(e){setError((e as Error).message);}finally{lock.current=false;setBusy(false);}
  }
  async function adopt(next:ReceiptQueueState, autoStart=started){
    if(next.activeId&&autoStart&&!next.receipt.version){next={...next,receipt:await bridge.request('receipt.start',{profileId,pageNumber:1}).promise};}
    setQueue(next);setPreview(null);
    setProfileChoice(next.receipt.version?.profileId??profileId);
    if(next.activeId){
      const values={...Object.fromEntries(next.receipt.fields.map(f=>[f.key,f.value|| (profile?.fields.find(p=>p.key===f.key)?.kind==='accountChoice'?account:'')])),...cache.current[next.activeId]};
      cache.current[next.activeId]=values;setDrafts(values);
      const target=bindings.current[next.activeId]??context;setChoice(target);
      if(target.workbookId&&!bindings.current[next.activeId])bindings.current[next.activeId]={...target};
    }
  }
  function edit(key:string,value:string){const next={...drafts,[key]:value};setDrafts(next);if(queue?.activeId)cache.current[queue.activeId]=next;setPreview(null);}
  function move(targetId:string,skip=false){if(!queue?.activeId)return;void run(async()=>adopt(await bridge.request('receipt.navigate',{expectedId:queue.activeId!,targetId,skip}).promise));}
  const index=queue?.items.findIndex(i=>i.id===queue.activeId)??-1;
  const pending=queue?.items.find((i,n)=>n>index&&i.status==='Pending')??queue?.items.find(i=>i.status==='Pending'&&i.id!==queue.activeId);
  const changedDestination=JSON.stringify(choice)!==JSON.stringify(destination);
  return <section className="fast-receipt-loop" aria-label="Fast receipt entry" aria-busy={busy}>
    <div className="reader-heading"><div><h2>Receipt queue</h2><p>Review each source, confirm its row, then continue. This queue lasts for this app session.</p></div><button disabled={blocked||!queue} onClick={()=>void run(async()=>adopt(await bridge.request('receipt.enqueue',{}).promise))}>Add PDFs or images</button><button disabled={blocked} onClick={()=>void run(async()=>adopt(await bridge.request('receipt.queue',{}).promise))}>Refresh queue</button></div>
    {error&&<Notice kind="error" title="Receipt action blocked">{error}</Notice>}
    {notice&&<div role="status" className="semantic-notice success">{notice}</div>}
    {!queue?.items.length&&<p>Select multiple local PDF, PNG or JPEG receipts to begin.</p>}
    {!!queue?.items.length&&<>
      <nav className="actions" aria-label="Receipt navigation"><button disabled={blocked||index<=0} onClick={()=>move(queue.items[index-1].id)}>Previous receipt</button><span>{index+1} of {queue.items.length} · {active?.displayName} · {active?.status}</span><button disabled={blocked||index>=queue.items.length-1} onClick={()=>move(queue.items[index+1].id)}>Next receipt</button><button disabled={blocked||inserted} onClick={()=>move(pending?.id??queue.activeId!,true)}>Skip receipt</button><label>Review receipt<select aria-label="Review receipt" value={queue.activeId??''} disabled={blocked} onChange={e=>move(e.target.value)}>{queue.items.map(i=><option key={i.id} value={i.id}>{i.displayName} · {i.status}</option>)}</select></label></nav>
      <div className="actions"><label>Workflow profile<select aria-label="Queue profile" value={profileChoice} disabled={blocked||inserted} onChange={e=>{setProfileChoice(e.target.value);if(!receipt?.version)setProfileId(e.target.value);setPreview(null);}}>{profiles.map(p=><option key={p.id} value={p.id}>{p.displayName}</option>)}</select></label>{!receipt?.version?<button disabled={blocked||!profileId} onClick={()=>void run(async()=>{setStarted(true);await adopt(queue,true);})}>Start receipt queue</button>:profileChoice!==receipt.version.profileId&&<button disabled={blocked||inserted} onClick={()=>void run(async()=>{
        const state=await bridge.request('receipt.changeProfile',{expected:receipt.version!,profileId:profileChoice,values:drafts}).promise;
        setProfileId(profileChoice);setQueue({...queue,receipt:state});const values=Object.fromEntries(state.fields.map(f=>[f.key,f.value]));setDrafts(values);cache.current[queue.activeId!]=values;delete bindings.current[queue.activeId!];setContext(emptyDestination);setChoice(emptyDestination);setCatalog(c=>({...c,sections:[]}));setAccount('');
      })}>Change profile for this receipt and new receipts</button>}</div>
      {receipt?.version&&<>
        <section aria-label="Queue destination" className="excel-insertion"><h3>Destination for this receipt</h3><p>{selectedBook?.name??'Choose workbook'} · Identity {destination.workbookId||'not selected'} · {profile?.displayName} · {destination.sheet||'Choose sheet'} · {destination.sectionId||'Choose section'} · Account {drafts.account||'Choose account'}</p>
          <div className="actions"><button disabled={blocked} onClick={()=>void run(async()=>{const c=await bridge.request('excel.listWorkbooks',{open:true}).promise;setCatalog(c);setChoice({...emptyDestination,workbookId:c.workbooks[0]?.id??''});})}>Open workbook in Excel</button><button disabled={blocked} onClick={()=>void run(async()=>{setCatalog(await bridge.request('excel.listWorkbooks',{open:false}).promise);})}>Refresh Excel workbooks</button>
            <label>Workbook<select aria-label="Queue workbook" disabled={blocked||inserted} value={choice.workbookId} onChange={e=>{setChoice({...emptyDestination,workbookId:e.target.value});setPreview(null);}}><option value="">Choose workbook</option>{catalog.workbooks.map(w=><option key={w.id} value={w.id}>{w.name} · {w.id.slice(0,8)}</option>)}</select></label>
            <label>Sheet<select aria-label="Queue sheet" disabled={blocked||inserted} value={choice.sheet} onChange={e=>{setChoice({...choice,sheet:e.target.value,sectionId:''});setPreview(null);}}><option value="">Choose sheet</option>{book?.sheets.map(s=><option key={s}>{s}</option>)}</select></label>
            <label>Section<select aria-label="Queue section" disabled={blocked||inserted} value={choice.sectionId} onChange={e=>{setChoice({...choice,sectionId:e.target.value});setPreview(null);}}><option value="">Choose section</option>{catalog.sections.filter(s=>!s.sheetName||s.sheetName===choice.sheet).map(s=><option key={s.id} value={s.id}>{s.label}</option>)}</select></label>
            <button disabled={blocked||inserted||!choice.workbookId||!choice.sheet||!choice.sectionId} onClick={()=>{bindings.current[queue.activeId!]={...choice};setContext({...choice});setPreview(null);}}>Use destination for this receipt and new receipts</button>
          </div>{changedDestination&&<Notice kind="warning" title="Destination change needs confirmation">Use destination explicitly before previewing. Previously reviewed receipts keep their own destination.</Notice>}
        </section>
        <div className="receipt-layout"><aside className="source-inspector"><h3>{receipt.source?.displayName}</h3>{receipt.source&&renderingAvailable&&<SourcePreview bridge={bridge} source={receipt.source} association={receipt.pageNumber}/>}</aside><div className="receipt-editor"><div className="receipt-fields">{profile?.fields.map(f=><div className="receipt-field" key={f.key}><label htmlFor={`queue-${f.key}`}>{f.header}{f.required?' (required)':''}</label>{f.kind==='accountChoice'?<select id={`queue-${f.key}`} aria-label={`Queue ${f.key}`} disabled={blocked||inserted} value={drafts[f.key]??''} onChange={e=>{edit(f.key,e.target.value);setAccount(e.target.value);}}><option value="">Choose account</option>{profile.accounts?.map(a=><option key={a.column} value={a.column}>{a.code} · {a.label} · {a.column}</option>)}</select>:<input id={`queue-${f.key}`} aria-label={`Queue ${f.key}`} disabled={blocked||inserted} maxLength={f.maxLength} value={drafts[f.key]??''} onChange={e=>edit(f.key,e.target.value)}/>}<span>{inserted?'Inserted': 'Draft — confirmation required'}</span></div>)}</div>
          <p>Amounts are written as entered. Confirm the total from the source; no VAT conversion is applied.</p>
          {missingFields.length>0&&<p role="status">Required fields missing: {missingFields.map(f=>f.header).join(', ')}.</p>}
          <button disabled={blocked||inserted||requiredMissing||profileChoice!==receipt.version.profileId||changedDestination||!destination.workbookId||!destination.sheet||!destination.sectionId} onClick={()=>void run(async()=>{
            const state=await bridge.request('receipt.prepare',{expected:receipt.version!,values:drafts}).promise;setQueue({...queue,receipt:state});
            const result=await bridge.request('excel.previewDraft',{expected:state.version!,...destination}).promise;
            if(result.workbookId!==destination.workbookId||result.sheet!==destination.sheet||result.sectionId!==destination.sectionId)throw new Error('The preview destination changed. Review the destination again.');
            setPreview(result);
          })}>Preview row</button>
          {preview&&<section aria-label="Queue row preview"><h3>{selectedBook?.name} · {preview.sheet}!{preview.range}</h3><div className="mapped-values"><table><caption>Confirm every supplied value against the source</caption><thead><tr><th>Field</th><th>Column</th><th>Value</th></tr></thead><tbody>{preview.values.map(v=><tr key={v.key}><th>{v.header}</th><td>{column(v.column)}</td><td>{v.displayValue??String(v.value??'')}</td></tr>)}</tbody></table></div><p>Account {drafts.account}. Add Row confirms these values as human-verified and writes this row to this destination.</p><button className="primary" disabled={blocked||inserted} onClick={()=>void run(async()=>{
            const result=await bridge.request('excel.addRow',{expected:receipt.version!,previewToken:preview.token,queueId:queue.activeId!}).promise;
            setCatalog(c=>({...c,changes:[...c.changes,result.change]}));setNotice(`Added ${active?.displayName} to ${result.change.workbook} · ${result.change.sheet}!${result.change.range}. Changes are highlighted; save in Excel explicitly.`);
            await adopt(result.queue);
          })}>Add Row — confirm values and destination</button></section>}
        </div></div>
      </>}
    </>}
    {catalog.changes.length>0&&<section aria-label="Inserted queue rows"><h3>Inserted rows</h3><ul>{catalog.changes.map((c:ExcelChange,i)=><li key={i}>{c.workbook} · {c.sheet}!{c.range}</li>)}</ul></section>}
  </section>;
}
