import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, test, vi } from 'vitest';
import SourcePreview from './SourcePreview';
import type { BridgeClient } from './bridge/client';
import { validSourceRender } from './bridge/receipts';

afterEach(()=>{cleanup();vi.unstubAllGlobals();});

test('OCR returns proposals and source boxes without invoking verification or insertion',async()=>{
  vi.stubGlobal('URL',{createObjectURL:()=> 'blob:fake',revokeObjectURL:vi.fn()});
  const proposals=vi.fn();
  const request=vi.fn((command:string,payload:any)=>({promise:Promise.resolve(command==='receipt.ocr'?{values:{amount:'123.50'},words:[{text:'123.50',confidence:.9,x:.1,y:.2,width:.2,height:.05}]}:command==='receipt.cancelRender'?{status:'requested'}:{source:source.document.version,page:{pageNumber:payload.pageNumber,pageCount:2,width:1,height:1,pngBase64:png}})}));
  render(<SourcePreview bridge={{request} as unknown as BridgeClient} source={source} association={1} onProposals={proposals}/>);
  await screen.findByAltText('Rendered source page 1');fireEvent.click(screen.getByText('Recognize page'));
  await waitFor(()=>expect(proposals).toHaveBeenCalledWith({amount:'123.50'}));
  expect(screen.getByLabelText('Source word 123.50')).toBeDefined();
  expect(request.mock.calls.some(c=>/verify|excel\./.test(c[0]))).toBe(false);
});

test('navigation cancels OCR and ignores a late result for the previous page',async()=>{
  vi.stubGlobal('URL',{createObjectURL:()=> 'blob:fake',revokeObjectURL:vi.fn()});
  const proposals=vi.fn();let finish!:(value:unknown)=>void;
  const request=vi.fn((command:string,payload:any)=>({promise:command==='receipt.ocr'?new Promise(resolve=>{finish=resolve;}):Promise.resolve(command==='receipt.cancelRender'?{status:'requested'}:{source:source.document.version,page:{pageNumber:payload.pageNumber,pageCount:2,width:1,height:1,pngBase64:png}})}));
  render(<SourcePreview bridge={{request} as unknown as BridgeClient} source={source} association={1} onProposals={proposals}/>);
  await screen.findByAltText('Rendered source page 1');fireEvent.click(screen.getByText('Recognize page'));await waitFor(()=>expect(finish).toBeDefined());fireEvent.click(screen.getByText('Next source page'));
  finish({values:{amount:'999'},words:[]});await screen.findByAltText('Rendered source page 2');
  await waitFor(()=>expect(request.mock.calls.some(c=>c[0]==='receipt.cancelRender')).toBe(true));expect(proposals).not.toHaveBeenCalled();
});

test('OCR failure keeps manual review available',async()=>{
  vi.stubGlobal('URL',{createObjectURL:()=> 'blob:fake',revokeObjectURL:vi.fn()});
  const request=vi.fn((command:string,payload:any)=>({promise:command==='receipt.ocr'?Promise.reject(new Error('Worker stopped')):Promise.resolve(command==='receipt.cancelRender'?{status:'requested'}:{source:source.document.version,page:{pageNumber:payload.pageNumber,pageCount:1,width:1,height:1,pngBase64:png}})}));
  render(<SourcePreview bridge={{request} as unknown as BridgeClient} source={source} association={1} onProposals={vi.fn()}/>);
  await screen.findByAltText('Rendered source page 1');fireEvent.click(screen.getByText('Recognize page'));
  expect((await screen.findByRole('alert')).textContent).toContain('Manual entry remains available');
});

test('dragged region is normalized and sent only to OCR',async()=>{
  vi.stubGlobal('PointerEvent',MouseEvent);
  vi.stubGlobal('URL',{createObjectURL:()=> 'blob:fake',revokeObjectURL:vi.fn()});
  const request=vi.fn((command:string,payload:any)=>({promise:Promise.resolve(command==='receipt.ocr'?{values:{},words:[]}:command==='receipt.cancelRender'?{status:'requested'}:{source:source.document.version,page:{pageNumber:payload.pageNumber,pageCount:1,width:1,height:1,pngBase64:png}})}));
  render(<SourcePreview bridge={{request} as unknown as BridgeClient} source={source} association={1} onProposals={vi.fn()}/>);
  const image=await screen.findByAltText('Rendered source page 1');const canvas=image.parentElement!;
  canvas.getBoundingClientRect=()=>({left:0,top:0,width:100,height:100} as DOMRect);canvas.setPointerCapture=vi.fn();
  fireEvent.pointerDown(canvas,{clientX:10,clientY:20});fireEvent.pointerUp(canvas,{clientX:60,clientY:70});
  fireEvent.click(screen.getByText('Recognize selected region'));
  await waitFor(()=>expect(request.mock.calls.find(c=>c[0]==='receipt.ocr')?.[1].region).toEqual({x:.1,y:.2,width:.5,height:.49999999999999994}));
});
const source={displayName:'artificial.pdf',document:{version:{documentId:'11111111-1111-1111-1111-111111111111',revision:0,sha256:'A'.repeat(64)},kind:0,byteLength:100,status:0}};
const png='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR1sAAAAASUVORK5CYII=';
test('renders path-free PNG bytes, navigates pages and revokes old image URLs',async()=>{
  const created=vi.fn(()=> 'blob:artificial-'+Math.random()),revoked=vi.fn();
  vi.stubGlobal('URL',{createObjectURL:created,revokeObjectURL:revoked});
  const request=vi.fn((command:string,payload:any)=>({promise:Promise.resolve(command==='receipt.cancelRender'?{status:'requested'}:{source:source.document.version,page:{pageNumber:payload.pageNumber,pageCount:2,width:1,height:1,pngBase64:png}})}));
  const {unmount}=render(<SourcePreview bridge={{request} as unknown as BridgeClient} source={source} association={1}/>);
  await screen.findByAltText('Rendered source page 1');
  fireEvent.click(screen.getByRole('button',{name:'Next source page'}));
  await screen.findByAltText('Rendered source page 2');
  expect(request.mock.calls.filter(c=>c[0]==='receipt.render').at(-1)?.[1].pageNumber).toBe(2);
  expect(screen.getByText('Viewing does not verify fields. Review page association: 1.')).toBeDefined();
  unmount();expect(revoked).toHaveBeenCalledTimes(2);
});
test('failure removes the current image and never substitutes a sample preview',async()=>{
  vi.stubGlobal('URL',{createObjectURL:vi.fn(),revokeObjectURL:vi.fn()});
  const request=vi.fn(()=>({promise:Promise.reject(new Error('Malformed document'))}));
  render(<SourcePreview bridge={{request} as unknown as BridgeClient} source={source} association={1}/>);
  await waitFor(()=>expect(screen.getByRole('alert').textContent).toContain('Malformed document'));
  expect(screen.queryByRole('img')).toBeNull();
});
test('render wire validator rejects oversized, path-bearing and malformed results',()=>{
  const good={source:source.document.version,page:{pageNumber:1,pageCount:2,width:1,height:1,pngBase64:png}};
  expect(validSourceRender(good)).toBe(true);
  expect(validSourceRender({...good,page:{...good.page,width:1601}})).toBe(false);
  expect(validSourceRender({...good,page:{...good.page,pngBase64:'file:///private.png'}})).toBe(false);
  expect(validSourceRender({...good,page:{...good.page,pageNumber:3}})).toBe(false);
});

function viewerBridge(total=8){const request=vi.fn((command:string,payload:any)=>({promise:Promise.resolve(command==='receipt.ocr'?{values:{},words:[]}:command==='receipt.cancelRender'?{}:{source:source.document.version,page:{pageNumber:payload.pageNumber,pageCount:total,width:800,height:1200,pngBase64:png}})}));return {request,bridge:{request} as unknown as BridgeClient};}
test('continuous placeholders retain order and decoded images stay bounded',async()=>{
 const revoked=vi.fn();vi.stubGlobal('URL',{createObjectURL:()=> 'blob:page',revokeObjectURL:revoked});const {bridge}=viewerBridge();const {unmount}=render(<SourcePreview bridge={bridge} source={source} association={1}/>);
 await screen.findByAltText('Rendered source page 2');expect(screen.getAllByLabelText(/Document page /)).toHaveLength(8);
 for(let i=0;i<5;i++){fireEvent.click(screen.getByText('Next source page'));await screen.findByAltText(`Rendered source page ${i+2}`);}
 await waitFor(()=>expect(screen.getAllByRole('img').length).toBeLessThanOrEqual(3));expect(revoked).toHaveBeenCalled();unmount();
});
test('zoom modes change native page size and keep source association unchanged',async()=>{
 vi.stubGlobal('URL',{createObjectURL:()=> 'blob:page',revokeObjectURL:vi.fn()});const {bridge,request}=viewerBridge(1);render(<SourcePreview bridge={bridge} source={source} association={1}/>);await screen.findByAltText('Rendered source page 1');
 fireEvent.click(screen.getByText('Actual Size (100%)'));expect(screen.getByText('100%')).toBeDefined();fireEvent.click(screen.getByLabelText('Zoom In'));expect(screen.getByText('125%')).toBeDefined();fireEvent.click(screen.getByLabelText('Zoom Out'));expect(screen.getByText('100%')).toBeDefined();fireEvent.click(screen.getByText('Fit Page'));fireEvent.click(screen.getByText('Fit Width'));expect(request.mock.calls.every(c=>c[0]==='receipt.render')).toBe(true);
});
test.each([1,1.5,.72])('page 3 region coordinates use scaled image bounds at scale %s',async scale=>{
 vi.stubGlobal('PointerEvent',MouseEvent);vi.stubGlobal('URL',{createObjectURL:()=> 'blob:page',revokeObjectURL:vi.fn()});const {bridge,request}=viewerBridge();render(<SourcePreview bridge={bridge} source={source} association={3} onProposals={vi.fn()}/>);
 const image=await screen.findByAltText('Rendered source page 3'),canvas=image.parentElement!;canvas.getBoundingClientRect=()=>({left:30,top:-100,width:800*scale,height:1200*scale} as DOMRect);canvas.setPointerCapture=vi.fn();
 fireEvent.pointerDown(canvas,{clientX:30+80*scale,clientY:-100+240*scale});fireEvent.pointerUp(canvas,{clientX:30+480*scale,clientY:-100+840*scale});fireEvent.click(screen.getByText('Recognize selected region'));
 await waitFor(()=>expect(request.mock.calls.find(c=>c[0]==='receipt.ocr')?.[1].pageNumber).toBe(3));const box=request.mock.calls.find(c=>c[0]==='receipt.ocr')![1].region;expect(box.x).toBeCloseTo(.1);expect(box.y).toBeCloseTo(.2);expect(box.width).toBeCloseTo(.5);expect(box.height).toBeCloseTo(.5);
});

test('scroll tracks visible page without changing source association or invoking OCR',async()=>{
 vi.stubGlobal('URL',{createObjectURL:()=> 'blob:page',revokeObjectURL:vi.fn()});const {bridge,request}=viewerBridge(5),changed=vi.fn();render(<SourcePreview bridge={bridge} source={source} association={1} onSourceChange={changed}/>);await screen.findByAltText('Rendered source page 2');
 const viewport=screen.getByLabelText('Scrollable document');viewport.getBoundingClientRect=()=>({top:10} as DOMRect);
 screen.getAllByLabelText(/Document page /).forEach((node,i)=>{node.getBoundingClientRect=()=>({top:(i-2)*400+10,bottom:(i-1)*400+10} as DOMRect);});fireEvent.scroll(viewport);
 await screen.findByText('Page 3 of 5');expect(changed).not.toHaveBeenCalled();expect(request.mock.calls.some(c=>c[0]==='receipt.ocr')).toBe(false);
});
test('a switched source rejects an outstanding render before making an image URL',async()=>{
 const created=vi.fn(()=> 'blob:new');vi.stubGlobal('URL',{createObjectURL:created,revokeObjectURL:vi.fn()});let finish!:(value:unknown)=>void;
 const other={...source,document:{...source.document,version:{...source.document.version,revision:1}}};
 const request=vi.fn((command:string,payload:any)=>({promise:command==='receipt.render'&&payload.source.revision===0?new Promise(resolve=>{finish=resolve;}):Promise.resolve(command==='receipt.render'?{source:other.document.version,page:{pageNumber:1,pageCount:1,width:800,height:1200,pngBase64:png}}:{})}));const bridge={request} as unknown as BridgeClient;
 const {rerender}=render(<SourcePreview bridge={bridge} source={source} association={1}/>);await waitFor(()=>expect(finish).toBeDefined());rerender(<SourcePreview bridge={bridge} source={other} association={1}/>);
 finish({source:source.document.version,page:{pageNumber:1,pageCount:1,width:800,height:1200,pngBase64:png}});await screen.findByAltText('Rendered source page 1');expect(created).toHaveBeenCalledTimes(1);
});
test('recognition waits for a neighboring render and no background render starts during OCR',async()=>{
 vi.stubGlobal('URL',{createObjectURL:()=> 'blob:page',revokeObjectURL:vi.fn()});let neighbor!:(value:unknown)=>void,ocr!:(value:unknown)=>void;
 const result=(n:number)=>({source:source.document.version,page:{pageNumber:n,pageCount:5,width:800,height:1200,pngBase64:png}});
 const request=vi.fn((command:string,payload:any)=>({promise:command==='receipt.ocr'?new Promise(resolve=>{ocr=resolve;}):command==='receipt.render'&&payload.pageNumber===2?new Promise(resolve=>{neighbor=resolve;}):Promise.resolve(command==='receipt.render'?result(payload.pageNumber):{})}));
 render(<SourcePreview bridge={{request} as unknown as BridgeClient} source={source} association={1} onProposals={vi.fn()}/>);
 await waitFor(()=>expect(neighbor).toBeDefined());fireEvent.click(screen.getByText('Recognize page'));expect(ocr).toBeUndefined();neighbor(result(2));await waitFor(()=>expect(ocr).toBeDefined());const renders=request.mock.calls.filter(c=>c[0]==='receipt.render').length;
 expect(request.mock.calls.filter(c=>c[0]==='receipt.render')).toHaveLength(renders);ocr({values:{},words:[]});await waitFor(()=>expect(screen.queryByText('Cancel OCR')).toBeNull());
});
const mixedSizes=[{width:800,height:1200},{width:1200,height:800},{width:600,height:1600}];
function mixedResult(n:number){return {source:source.document.version,page:{pageNumber:n,pageCount:3,...mixedSizes[n-1],pngBase64:png}};}
function mixedSetup(deferNeighbor=false){
 vi.stubGlobal('URL',{createObjectURL:()=> 'blob:mixed',revokeObjectURL:vi.fn()});
 let resize!:()=>void,finish!:(value:unknown)=>void;
 vi.stubGlobal('ResizeObserver',class {constructor(cb:()=>void){resize=cb;}observe(){}disconnect(){}});
 const request=vi.fn((command:string,payload:any)=>({promise:command==='receipt.render'?(deferNeighbor&&payload.pageNumber===2?new Promise(resolve=>{finish=resolve;}):Promise.resolve(mixedResult(payload.pageNumber))):Promise.resolve(command==='receipt.ocr'?{values:{},words:[{text:'total',x:.1,y:.2,width:.3,height:.1}]}:{})}));
 return {bridge:{request} as unknown as BridgeClient,request,resize:()=>resize(),finish:()=>finish(mixedResult(2))};
}
function pageNode(n:number){return screen.getByLabelText(`Document page ${n}`);}
test('mixed page fit sizes are independent of active page; manual zoom uses a common scale',async()=>{
 const {bridge}=mixedSetup();render(<SourcePreview bridge={bridge} source={source} association={2}/>);await screen.findByAltText('Rendered source page 3');
 const widths=()=>mixedSizes.map((_,i)=>parseFloat(pageNode(i+1).style.width));
 widths().forEach(w=>expect(w).toBeCloseTo(574));
 const viewport=screen.getByLabelText('Scrollable document');viewport.getBoundingClientRect=()=>({top:0} as DOMRect);mixedSizes.forEach((_,i)=>{pageNode(i+1).getBoundingClientRect=()=>({top:i*400,bottom:(i+1)*400} as DOMRect);});fireEvent.scroll(viewport);await screen.findByText('Page 1 of 3');widths().forEach(w=>expect(w).toBeCloseTo(574));
 fireEvent.click(screen.getByText('Fit Page'));
 mixedSizes.forEach((d,i)=>{const scale=Math.min(574/d.width,474/d.height);expect(parseFloat(pageNode(i+1).style.width)).toBeCloseTo(d.width*scale);expect(parseFloat(pageNode(i+1).style.minHeight)).toBeLessThanOrEqual(474);});
 fireEvent.click(screen.getByText('Next source page'));await screen.findByAltText('Rendered source page 3');
 mixedSizes.forEach((d,i)=>expect(parseFloat(pageNode(i+1).style.width)).toBeCloseTo(d.width*Math.min(574/d.width,474/d.height)));
 fireEvent.click(screen.getByText('Actual Size (100%)'));mixedSizes.forEach((d,i)=>expect(parseFloat(pageNode(i+1).style.width)).toBe(d.width));
 fireEvent.click(screen.getByLabelText('Zoom In'));fireEvent.click(screen.getByLabelText('Zoom In'));mixedSizes.forEach((d,i)=>expect(parseFloat(pageNode(i+1).style.width)).toBe(d.width*1.5));
});
test('landscape OCR regions and word boxes use the scaled landscape image',async()=>{
 vi.stubGlobal('PointerEvent',MouseEvent);const {bridge,request}=mixedSetup();render(<SourcePreview bridge={bridge} source={source} association={2} onProposals={vi.fn()}/>);
 const image=await screen.findByAltText('Rendered source page 2'),canvas=image.parentElement!;canvas.setPointerCapture=vi.fn();canvas.getBoundingClientRect=()=>({left:-30,top:-80,width:574,height:574*800/1200} as DOMRect);
 const bounds=canvas.getBoundingClientRect();fireEvent.pointerDown(canvas,{clientX:bounds.left+bounds.width*.1,clientY:bounds.top+bounds.height*.2});fireEvent.pointerUp(canvas,{clientX:bounds.left+bounds.width*.6,clientY:bounds.top+bounds.height*.7});fireEvent.click(screen.getByText('Recognize selected region'));
 await screen.findByLabelText('Source word total');const payload=request.mock.calls.find(c=>c[0]==='receipt.ocr')![1];expect(payload.pageNumber).toBe(2);expect(payload.region.x).toBeCloseTo(.1);expect(payload.region.y).toBeCloseTo(.2);expect(payload.region.width).toBeCloseTo(.5);expect(payload.region.height).toBeCloseTo(.5);
 fireEvent.click(screen.getByText('Actual Size (100%)'));const word=screen.getByLabelText('Source word total');expect(word.style.left).toBe('10%');expect(word.style.top).toBe('20%');expect(word.style.width).toBe('30%');
});
test('neighbor loading, resizing and zoom preserve the visible page reading anchor',async()=>{
 const setup=mixedSetup(true);render(<SourcePreview bridge={setup.bridge} source={source} association={3}/>);await screen.findByAltText('Rendered source page 3');
 const root=screen.getByLabelText('Scrollable document');let width=600;Object.defineProperty(root,'clientWidth',{get:()=>width});Object.defineProperty(root,'clientHeight',{value:500});
 root.getBoundingClientRect=()=>({top:100} as DOMRect);
 for(let n=1;n<=3;n++){const node=pageNode(n);node.getBoundingClientRect=()=>{const height=parseFloat(node.style.minHeight)+2;let top=112-root.scrollTop;for(let i=1;i<n;i++)top+=parseFloat(pageNode(i).style.minHeight)+18;return {top,bottom:top+height,height,width:parseFloat(node.style.width)} as DOMRect;};}
 root.scrollTop=parseFloat(pageNode(1).style.minHeight)+parseFloat(pageNode(2).style.minHeight)+36+100;
 const fraction=()=>{const r=pageNode(3).getBoundingClientRect();return (112-r.top)/r.height;};const before=fraction();
 setup.finish();await screen.findByAltText('Rendered source page 2');expect(fraction()).toBeCloseTo(before);
 width=420;fireEvent(root,new Event('resize'));setup.resize();await waitFor(()=>expect(parseFloat(pageNode(3).style.width)).toBeCloseTo(394));expect(fraction()).toBeCloseTo(before);
 fireEvent.click(screen.getByText('Actual Size (100%)'));expect(fraction()).toBeCloseTo(before);
});
