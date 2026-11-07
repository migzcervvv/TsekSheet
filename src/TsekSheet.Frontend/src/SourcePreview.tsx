import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { BridgeClient } from './bridge/client';
import type { ReceiptState } from './bridge/receipts';
import { Notice } from './ui/Workspace';
type Box={x:number;y:number;width:number;height:number};
type Page={url:string;width:number;height:number};
// A receipt switch can remount the viewer before native cancellation completes.
// Share the operation tail per bridge so its successor waits for that completion.
const lanes=new WeakMap<BridgeClient,{current:Promise<unknown>}>();
function renderLane(bridge:BridgeClient){let lane=lanes.get(bridge);if(!lane){lane={current:Promise.resolve()};lanes.set(bridge,lane);}return lane;}

export default function SourcePreview({bridge,source,association,onProposals,activeValue,onWord,onBusyChange,onSourceChange,getProposalEpoch}:{bridge:BridgeClient;source:NonNullable<ReceiptState['source']>;association:number;onProposals?:(values:Record<string,string>)=>void;activeValue?:string;onWord?:(text:string)=>void;onBusyChange?:(busy:boolean)=>void;onSourceChange?:()=>void;getProposalEpoch?:()=>number}) {
 const version=source.document.version,key=JSON.stringify(version);
 const [page,setPage]=useState(association),[count,setCount]=useState<number|null>(null),[pages,setPages]=useState<Record<number,Page>>({});
 const metadata=useRef<Record<number,{width:number;height:number}>>({});
 const cache=useRef<Record<number,Page>>({}),flight=renderLane(bridge),generation=useRef(0),ocrToken=useRef(0);
 const [busy,setBusy]=useState(false),[ocrBusy,setOcrBusy]=useState(false),[error,setError]=useState(''),[ocrError,setOcrError]=useState(''),[attempt,setAttempt]=useState(0);
 const [words,setWords]=useState<(Box&{text:string})[]>([]),[region,setRegion]=useState<Box|null>(null),[selected,setSelected]=useState(-1);
 const anchor=useRef<{x:number;y:number;page:number}|null>(null),viewport=useRef<HTMLDivElement>(null),nodes=useRef(new Map<number,HTMLDivElement>());
 const [mode,setMode]=useState('width'),[zoom,setZoom]=useState(1),[size,setSize]=useState({width:600,height:500});
 const callback=useRef(onProposals);callback.current=onProposals;
 const fallback={width:1131,height:1600};
 function pageDimensions(n:number){return pages[n]??metadata.current[n]??fallback;}
 function pageScale(n:number){const d=pageDimensions(n);return mode==='width'?Math.max(.001,(size.width-26)/d.width):mode==='page'?Math.max(.001,Math.min((size.width-26)/d.width,(size.height-26)/d.height)):zoom;}
 const scale=pageScale(page);
 const readingAnchor=useRef<{page:number;fraction:number;horizontal:number}|null>(null);
 function capturePosition(){
  const root=viewport.current;if(!root||readingAnchor.current)return;
  const top=root.getBoundingClientRect().top+12;
  for(const [n,node] of nodes.current){const rect=node.getBoundingClientRect();if(rect.bottom>top&&rect.height>0){readingAnchor.current={page:n,fraction:(top-rect.top)/rect.height,horizontal:root.scrollLeft/Math.max(1,rect.width)};break;}}
 }
 useLayoutEffect(()=>{
  const saved=readingAnchor.current,root=viewport.current;readingAnchor.current=null;
  const node=saved&&nodes.current.get(saved.page);
  if(!saved||!root||!node)return;
  const rect=node.getBoundingClientRect();
  root.scrollTop+=rect.top+saved.fraction*rect.height-root.getBoundingClientRect().top-12;
  root.scrollLeft=saved.horizontal*rect.width;
 },[pages,count,size,mode,zoom]);
 useEffect(()=>{onBusyChange?.(busy||ocrBusy);},[busy,ocrBusy,onBusyChange]);
 useEffect(()=>{const element=viewport.current;if(!element||typeof ResizeObserver==='undefined')return;const observer=new ResizeObserver(()=>{capturePosition();setSize({width:element.clientWidth,height:element.clientHeight});});observer.observe(element);return()=>observer.disconnect();},[]);
 useEffect(()=>{setPage(association);},[association]);
 useEffect(()=>{
  generation.current++;readingAnchor.current=null;setPages({});setCount(null);setPage(association);setError('');
  return()=>{generation.current++;ocrToken.current++;void bridge.request('receipt.cancelRender',{}).promise.catch(()=>{});Object.values(cache.current).forEach(p=>URL.revokeObjectURL(p.url));cache.current={};metadata.current={};};
 },[bridge,key]);
 useEffect(()=>{ocrToken.current++;if(anchor.current?.page!==page)anchor.current=null;setRegion(null);setWords([]);setSelected(-1);setOcrBusy(false);setOcrError('');},[key,page]);
 useEffect(()=>{
  if(ocrBusy)return;
  const current=generation.current;let disposed=false;
  const targets=[page,...(count?[page-1,page+1].filter(n=>n>=1&&n<=count):[])];
  for(const n of Object.keys(cache.current).map(Number))if(!targets.includes(n)){URL.revokeObjectURL(cache.current[n].url);delete cache.current[n];}
  setPages({...cache.current});setBusy(!cache.current[page]);setError('');
  const load=async()=>{
   for(const target of targets){
    if(disposed||current!==generation.current)return;
    if(cache.current[target])continue;
    try {
     const result=await bridge.request('receipt.render',{source:version,pageNumber:target}).promise;
     if(disposed||current!==generation.current)return;
     if(JSON.stringify(result.source)!==key||result.page.pageNumber!==target)throw new Error('Preview does not match the registered source and page.');
     const bytes=Uint8Array.from(atob(result.page.pngBase64),c=>c.charCodeAt(0));
     cache.current[target]={url:URL.createObjectURL(new Blob([bytes],{type:'image/png'})),width:result.page.width,height:result.page.height};
     capturePosition();metadata.current[target]={width:result.page.width,height:result.page.height};setPages({...cache.current});setCount(result.page.pageCount);
    }catch(e){if(!disposed&&current===generation.current)setError(`Page ${target}: ${(e as Error).message}`);}
   }
   if(!disposed&&current===generation.current)setBusy(false);
  };
  flight.current=flight.current.catch(()=>{}).then(load);
  return()=>{disposed=true;};
 },[bridge,key,page,count,attempt,ocrBusy]);
 function navigate(target:number,explicit=false){if(ocrBusy){ocrToken.current++;void bridge.request('receipt.cancelRender',{}).promise.catch(()=>{});}if(explicit)onSourceChange?.();setPage(target);if(explicit)nodes.current.get(target)?.scrollIntoView?.({block:'start'});}
 function changeZoom(next:string,value=scale){
  capturePosition();
  if(next==='page')readingAnchor.current={page,fraction:0,horizontal:0};
  setMode(next);setZoom(Math.max(.5,Math.min(4,value)));setPages(current=>({...current}));
 }
 async function recognize(){
  onSourceChange?.();const epoch=getProposalEpoch?.(),current=generation.current,token=++ocrToken.current;setOcrBusy(true);setOcrError('');
  try{await flight.current.catch(()=>{});if(token!==ocrToken.current||current!==generation.current)return;
   const operation=bridge.request('receipt.ocr',{source:version,pageNumber:page,region}).promise;flight.current=operation;const result=await operation;
   if(token!==ocrToken.current||current!==generation.current)return;
   setWords(result.words);if(epoch===getProposalEpoch?.())callback.current?.(result.values);
  }catch(e){if(token===ocrToken.current&&current===generation.current)setOcrError((e as Error).message);}
  finally{if(token===ocrToken.current&&current===generation.current)setOcrBusy(false);}
 }
 function point(e:React.PointerEvent<HTMLDivElement>){const b=e.currentTarget.getBoundingClientRect();return {x:Math.max(0,Math.min(1,(e.clientX-b.left)/b.width)),y:Math.max(0,Math.min(1,(e.clientY-b.top)/b.height))};}
 return <div className="source-preview" aria-label="Local source preview" aria-busy={busy}>
 <div className="actions">{count!==1&&<button disabled={page<=1} onClick={()=>navigate(page-1,true)}>Previous source page</button>}<span>Page {page}{count?' of '+count:''}</span>{count!==1&&<button disabled={!count||page>=count} onClick={()=>navigate(page+1,true)}>Next source page</button>}</div>
 <div className="actions" role="group" aria-label="Document zoom"><button aria-label="Zoom Out" onClick={()=>changeZoom('custom',scale-.25)}>−</button><output>{Math.round(scale*100)}%</output><button aria-label="Zoom In" onClick={()=>changeZoom('custom',scale+.25)}>+</button><button onClick={()=>changeZoom('width')}>Fit Width</button><button onClick={()=>changeZoom('page')}>Fit Page</button><button onClick={()=>changeZoom('custom',1)}>Actual Size (100%)</button></div>
 {busy&&<p role="status">Rendering local page…</p>}
 {error&&<Notice kind="error" title="Source preview unavailable"><p>Retry rendering or continue with manual entry.</p><p>{error}</p><button onClick={()=>setAttempt(n=>n+1)}>Retry failed pages</button></Notice>}
 {onProposals&&<><div className="actions"><button disabled={busy||ocrBusy||!pages[page]} onClick={()=>void recognize()}>{region?'Recognize selected region':'Recognize page'}</button>{ocrBusy&&<button onClick={()=>{ocrToken.current++;setOcrBusy(false);void bridge.request('receipt.cancelRender',{}).promise.catch(()=>{});}}>Cancel OCR</button>}<button onClick={()=>{onSourceChange?.();setRegion(null);}}>Clear region</button></div><p>OCR target: Page {page}. Drag with a mouse to select a region. Touch scrolls.</p>{ocrError&&<div role="alert">OCR unavailable. Manual entry remains available. {ocrError}</div>}</>}
 <div className="document-viewport" ref={viewport} tabIndex={0} aria-label="Scrollable document" onScroll={()=>{const root=viewport.current;if(!root)return;const top=root.getBoundingClientRect().top;let nearest=page,distance=Infinity;nodes.current.forEach((node,n)=>{const b=node.getBoundingClientRect(),d=Math.abs(b.top-top);if(b.bottom>top&&d<distance){nearest=n;distance=d;}});if(nearest!==page)navigate(nearest);}}>
 {Array.from({length:count??1},(_,i)=>i+1).map(n=>{const entry=pages[n];return <div key={n} className="document-page" ref={node=>{if(node)nodes.current.set(n,node);else nodes.current.delete(n);}} style={{width:pageDimensions(n).width*pageScale(n),minHeight:pageDimensions(n).height*pageScale(n)}} aria-label={`Document page ${n}`}>
 {!entry?<p>Page {n} — not loaded</p>:<div style={{position:'relative',touchAction:'pan-x pan-y'}} onPointerDown={e=>{if(e.pointerType==='touch'||!onProposals||(e.target as HTMLElement).closest('button'))return;if(n!==page)navigate(n);anchor.current={...point(e),page:n};e.currentTarget.setPointerCapture(e.pointerId);}} onPointerCancel={()=>{anchor.current=null;}} onPointerUp={e=>{const a=anchor.current;anchor.current=null;if(!a||a.page!==n)return;const p=point(e);if(Math.abs(p.x-a.x)>.01&&Math.abs(p.y-a.y)>.01){onSourceChange?.();setRegion({x:Math.min(p.x,a.x),y:Math.min(p.y,a.y),width:Math.abs(p.x-a.x),height:Math.abs(p.y-a.y)});}}}>
 <img style={{width:'100%',display:'block'}} draggable={false} className="rendered-source" src={entry.url} alt={`Rendered source page ${n}`}/>
 {n===page&&words.map((w,i)=><button key={i} title={w.text} aria-label={`Source word ${w.text}`} onClick={()=>{setSelected(i);onWord?.(w.text);}} style={{position:'absolute',left:`${w.x*100}%`,top:`${w.y*100}%`,width:`${w.width*100}%`,height:`${w.height*100}%`,padding:0,minHeight:0,border:(selected===i||!!activeValue?.toLowerCase().includes(w.text.toLowerCase()))?'2px solid blue':'1px solid transparent',background:selected===i?'#ffff0060':'transparent'}}/>)}
 {n===page&&region&&<div style={{pointerEvents:'none',position:'absolute',left:`${region.x*100}%`,top:`${region.y*100}%`,width:`${region.width*100}%`,height:`${region.height*100}%`,border:'2px solid blue'}}/>}
 </div>}</div>;})}</div>
 {selected>=0&&words[selected]&&<p>Source selection: {words[selected].text} — unverified</p>}
 <p>Viewing does not verify fields. Review page association: {association}.</p>
 </div>;
}
