import { useEffect, useRef, useState } from 'react';
import type { BridgeClient } from './bridge/client';
import type { ReceiptState } from './bridge/receipts';
import { Notice } from './ui/Workspace';

export default function SourcePreview({bridge, source, association,onProposals,activeValue,onWord,onBusyChange,onSourceChange}: {onBusyChange?:(busy:boolean)=>void;onSourceChange?:()=>void;activeValue?:string;onWord?:(text:string)=>void;onProposals?:(values:Record<string,string>)=>void;bridge:BridgeClient;source:NonNullable<ReceiptState['source']>;association:number}) {
  const proposalCallback=useRef(onProposals);proposalCallback.current=onProposals;
  const [page,setPage]=useState(1),[count,setCount]=useState<number|null>(null),[url,setUrl]=useState('');
  const [busy,setBusy]=useState(false),[error,setError]=useState(''),[attempt,setAttempt]=useState(0);
  const [ocrBusy,setOcrBusy]=useState(false),[ocrError,setOcrError]=useState('');
  useEffect(()=>{onBusyChange?.(busy||ocrBusy);},[busy,ocrBusy,onBusyChange]);
  const [words,setWords]=useState<{text:string;x:number;y:number;width:number;height:number}[]>([]);
  const [selected,setSelected]=useState(-1);
  const [region,setRegion]=useState<{x:number;y:number;width:number;height:number}|null>(null);
  const anchor=useRef<{x:number;y:number}|null>(null);
  useEffect(()=>{setWords([]);setRegion(null);setOcrError('');setOcrBusy(false);},[versionKey(source),page]);
  async function recognize(){
    onSourceChange?.();const current=generation.current;setOcrBusy(true);setOcrError('');
    try{const result=await bridge.request('receipt.ocr',{source:version,pageNumber:page,region}).promise;
      if(current!==generation.current)return;setWords(result.words);proposalCallback.current?.(result.values);
    }catch(e){if(current===generation.current)setOcrError((e as Error).message);}
    finally{if(current===generation.current)setOcrBusy(false);}
  }
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
    <div className="actions"><button disabled={busy||page<=1} onClick={()=>{onSourceChange?.();setPage(n=>n-1);}}>Previous source page</button><span>Page {page}{count?' of '+count:''}</span><button disabled={busy||!count||page>=count} onClick={()=>{onSourceChange?.();setPage(n=>n+1);}}>Next source page</button></div>
    {busy && <><p role="status">Rendering local page…</p><button onClick={()=>void bridge.request('receipt.cancelRender',{}).promise.catch(()=>{})}>Cancel rendering</button></>}
    {error && <Notice kind="error" title="Source preview unavailable"><p>Retry rendering or continue with manual entry.</p><details><summary>Details</summary>{error}</details></Notice>}
    {!busy && <button onClick={()=>{onSourceChange?.();setAttempt(n=>n+1);}}>Render source again</button>}
    {onProposals&&<div className="actions"><button disabled={busy||ocrBusy||!url} onClick={()=>void recognize()}>{region?'Recognize selected region':'Recognize page'}</button>{ocrBusy&&<button onClick={()=>void bridge.request('receipt.cancelRender',{}).promise.catch(()=>{})}>Cancel OCR</button>}<button onClick={()=>{onSourceChange?.();setRegion(null);}}>Clear region</button><details className="source-details"><summary>OCR help</summary>OCR proposals are unverified. Drag a rectangle on the page to re-OCR a region. Existing typed values are retained.</details>{ocrError&&<div role="alert"><p>OCR unavailable. Manual entry remains available.</p><details><summary>Details</summary>{ocrError}</details></div>}</div>}
    {url && <><button aria-pressed={expanded} onClick={()=>setExpanded(value=>!value)}>{expanded?'Fit source inspector':'Enlarge source page'}</button><div style={{position:'relative',touchAction:'none'}} onPointerDown={e=>{if(!onProposals||(e.target as HTMLElement).closest('button'))return;const b=e.currentTarget.getBoundingClientRect();anchor.current={x:(e.clientX-b.left)/b.width,y:(e.clientY-b.top)/b.height};e.currentTarget.setPointerCapture(e.pointerId);}} onPointerUp={e=>{const a=anchor.current;if(!a)return;anchor.current=null;const b=e.currentTarget.getBoundingClientRect();const x=Math.max(0,Math.min(1,(e.clientX-b.left)/b.width)),y=Math.max(0,Math.min(1,(e.clientY-b.top)/b.height));if(Math.abs(x-a.x)>.01&&Math.abs(y-a.y)>.01){onSourceChange?.();setRegion({x:Math.min(x,a.x),y:Math.min(y,a.y),width:Math.abs(x-a.x),height:Math.abs(y-a.y)});}}}>
<img style={{width:'100%',display:'block'}} draggable={false} className="rendered-source" src={url} alt={`Rendered source page ${page}`} />
{words.map((w,i)=><button key={i} title={w.text} aria-label={`Source word ${w.text}`} onClick={()=>{setSelected(i);onWord?.(w.text);}} style={{position:'absolute',left:`${w.x*100}%`,top:`${w.y*100}%`,width:`${w.width*100}%`,height:`${w.height*100}%`,padding:0,minHeight:0,border:(selected===i||!!activeValue?.toLowerCase().includes(w.text.toLowerCase()))?'2px solid blue':'1px solid transparent',background:selected===i?'#ffff0060':'transparent'}}/>)}
{region&&<div style={{pointerEvents:'none',position:'absolute',left:`${region.x*100}%`,top:`${region.y*100}%`,width:`${region.width*100}%`,height:`${region.height*100}%`,border:'2px solid blue'}}/>}
</div>{selected>=0&&words[selected]&&<p>Source selection: {words[selected].text} — unverified</p>}</>}
    <p>Viewing does not verify fields. Review page association: {association}.</p>
  </div>;
}

function versionKey(source:NonNullable<ReceiptState['source']>){return JSON.stringify(source.document.version);}
