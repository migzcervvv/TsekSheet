import { useEffect, useRef, useState } from 'react';
import type { BridgeClient } from './bridge/client';
import type { ReceiptState } from './bridge/receipts';
import { Notice } from './ui/Workspace';

export default function SourcePreview({bridge, source, association}: {bridge:BridgeClient;source:NonNullable<ReceiptState['source']>;association:number}) {
  const [page,setPage]=useState(1),[count,setCount]=useState<number|null>(null),[url,setUrl]=useState('');
  const [busy,setBusy]=useState(false),[error,setError]=useState(''),[attempt,setAttempt]=useState(0);
  const [expanded,setExpanded]=useState(false);
  const pendingCancel=useRef<Promise<unknown>>(Promise.resolve());
  const generation=useRef(0),objectUrl=useRef('');
  const version=source.document.version;
  useEffect(()=>{setPage(association);},[association]);
  useEffect(()=>{
    const current=++generation.current;
    setBusy(true);setError('');setUrl('');setCount(null);
    if(objectUrl.current){URL.revokeObjectURL(objectUrl.current);objectUrl.current='';}
    void pendingCancel.current.catch(()=>{}).then(()=>current===generation.current?bridge.request('receipt.render',{source:version,pageNumber:page}).promise:null).then(result=>{
      if(!result)return;
      if(current!==generation.current)return;
      if(JSON.stringify(result.source)!==JSON.stringify(version)||result.page.pageNumber!==page)throw new Error('Preview does not match the registered source and page.');
      const binary=atob(result.page.pngBase64),bytes=Uint8Array.from(binary,c=>c.charCodeAt(0));
      const rendered=URL.createObjectURL(new Blob([bytes],{type:'image/png'}));objectUrl.current=rendered;
      setUrl(rendered);setCount(result.page.pageCount);
    }).catch(e=>{if(current===generation.current)setError((e as Error).message);})
      .finally(()=>{if(current===generation.current)setBusy(false);});
    return ()=>{generation.current++;pendingCancel.current=bridge.request('receipt.cancelRender',{}).promise.catch(()=>{});};
  },[bridge,version.documentId,version.revision,version.sha256,page,attempt]);
  useEffect(()=>()=>{if(objectUrl.current)URL.revokeObjectURL(objectUrl.current);},[]);
  return <div className="source-preview" data-expanded={expanded} aria-label="Local source preview" aria-busy={busy}>
    <div className="actions"><button disabled={busy||page<=1} onClick={()=>setPage(n=>n-1)}>Previous source page</button><span>Page {page}{count?' of '+count:''}</span><button disabled={busy||!count||page>=count} onClick={()=>setPage(n=>n+1)}>Next source page</button></div>
    {busy && <><p role="status">Rendering local page…</p><button onClick={()=>void bridge.request('receipt.cancelRender',{}).promise.catch(()=>{})}>Cancel rendering</button></>}
    {error && <Notice kind="error" title="Source preview unavailable">{error}</Notice>}
    {!busy && <button onClick={()=>setAttempt(n=>n+1)}>Render source again</button>}
    {url && <><button aria-pressed={expanded} onClick={()=>setExpanded(value=>!value)}>{expanded?'Fit source inspector':'Enlarge source page'}</button><img className="rendered-source" src={url} alt={`Rendered source page ${page}`} /></>}
    <p>Viewing does not verify fields. Review page association: {association}.</p>
  </div>;
}
