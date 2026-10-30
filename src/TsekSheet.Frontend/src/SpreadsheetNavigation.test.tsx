import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {afterEach,expect,test} from 'vitest';
import {BridgeClient} from './bridge/client';
import type {Transport,Viewport} from './bridge/client';
import type {ReviewDecision} from './bridge/sectionReview';
import {reviewBook,reviewFixture} from './reviewTestFixture';
import WorkbookViewer from './WorkbookViewer';
import {parseCell} from './worksheetNavigation';

const clients:BridgeClient[]=[];
afterEach(()=>{cleanup();clients.splice(0).forEach(c=>c.dispose());});
function setup(delay=0){
 const messages:{command:string;payload:Record<string,unknown>}[]=[];
 const listeners=new Set<(e:{data:unknown})=>void>();let snapshot=reviewFixture(),inFlight=0,peak=0,opens=0;
 const book={...reviewBook,worksheets:[...reviewBook.worksheets,{...reviewBook.worksheets[0],id:'44444444-4444-4444-4444-444444444444',name:'Other',order:2}],compatibility:{fileBytes:100,compressedPartBytes:90,expandedPartBytes:200,partCount:4,definedNames:0,relationships:2,externalLinks:false,categories:[],countStatus:'Synthetic',capabilities:['Metadata inspection','Read-only browsing','Worksheet selection','Cell editing','Working copies','Structural insertion','Merge','Structural Merge','Export'].map((operation,i)=>({operation,allowed:i<3,reason:'Synthetic guard'}))}};
 const host:Transport={addEventListener:(_,l)=>{listeners.add(l);},removeEventListener:(_,l)=>{listeners.delete(l);},postMessage:m=>{
  const req=m as {command:string;requestId:string;payload:Record<string,unknown>};messages.push(req);
  if(req.command==='workbook.readViewport'){inFlight++;peak=Math.max(peak,inFlight);}
  setTimeout(()=>{
   let result:unknown={status:'cancelled'};
   if(req.command==='workbook.inspect'||req.command==='workbook.open')result={status:'opened',workbook:{...book,sessionId:opens++===0?book.sessionId:'55555555-5555-5555-5555-555555555555'}};
   if(req.command==='workbook.close')result={status:'closed'};
   if(req.command==='sectionReview.start'){snapshot=reviewFixture();result={status:'started',review:snapshot};}
   if(req.command==='sectionReview.decide'){snapshot=structuredClone(snapshot);snapshot.binding.reviewRevision++;snapshot.decisions.push(req.payload.decision as ReviewDecision);result=snapshot;}
   if(req.command==='workbook.readViewport'){
    const p=req.payload,r=p.startRow as number,c=p.startColumn as number;
    const view:Viewport={sessionId:p.sessionId as string,worksheetId:p.worksheetId as string,startRow:r,startColumn:c,rows:20,columns:6,cells:Array.from({length:120},(_,i)=>({row:r+Math.floor(i/6),column:c+i%6,value:'',kind:'Blank',formula:null,truncated:false,format:{numberFormat:'',numberFormatId:0,bold:false,italic:false,fillColor:''}})),scan:{declaredDimension:'A1:XFD1048576',dimensionReliability:'Complete scan',storedCells:1000,nonemptyValues:3,formulas:1,styledCells:996,lastRow:10000,lastColumn:16384,lastRowElement:20000,meaningfulLastRow:1000,meaningfulLastColumn:12,potentialInflation:true,warnings:[]}};
    inFlight--;result=view;
   }
   listeners.forEach(l=>l({data:{protocolVersion:1,type:'response',requestId:req.requestId,ok:true,result}}));
  },req.command==='workbook.readViewport'?delay:0);
 }};
 const bridge=new BridgeClient(host);clients.push(bridge);render(<WorkbookViewer bridge={bridge} available/>);
 return {messages,peak:()=>peak};
}
async function open(){fireEvent.click(screen.getByRole('button',{name:'Inspect workbook read-only'}));await screen.findByRole('button',{name:'Inspect C1'});}
async function ready(){await waitFor(()=>expect((screen.getByRole('button',{name:'Close workbook'}) as HTMLButtonElement).disabled).toBe(false));}
test('A1 parser rejects invalid and out-of-sheet coordinates',()=>{expect(parseCell(' c52 ')).toEqual({row:52,column:3});expect(parseCell('$XFD$1048576')).toEqual({row:1048576,column:16384});for(const value of ['A0','XFE1','A1048577','1C','C1:D2','C01','C1.5'])expect(parseCell(value)).toBeNull();});
test('Go To Cell, native scroll events and keys use bounded source windows while preserving selection',async()=>{
 const s=setup();await open();
 expect(screen.getByText('Useful data: 1000 rows × 12 columns')).toBeDefined();
 fireEvent.change(screen.getByRole('textbox',{name:'Go To Cell'}),{target:{value:'G900'}});fireEvent.click(screen.getByRole('button',{name:'Go'}));
 await screen.findByRole('button',{name:'Inspect G900'});expect(screen.getByLabelText('Read-only cell details').textContent).toContain('Inspect G900');
 const grid=screen.getByLabelText('Worksheet viewport');fireEvent.keyDown(grid,{key:'ArrowDown'});
 expect(screen.getByLabelText('Read-only cell details').textContent).toContain('Inspect G901');
 fireEvent.keyDown(grid,{key:'PageUp'});await screen.findByRole('button',{name:'Inspect G881'});
 fireEvent.scroll(grid,{target:{scrollTop:882*28,scrollLeft:2*140}});await screen.findByRole('button',{name:'Inspect C883'});
 expect(screen.getByLabelText('Read-only cell details').textContent).toContain('Inspect G881');
 fireEvent.click(screen.getByRole('checkbox'));expect(screen.getByText('Stored range: 20000 rows × 16384 columns')).toBeDefined();
 expect(s.messages.filter(m=>m.command==='workbook.readViewport').every(m=>m.payload.rows===20&&m.payload.columns===6)).toBe(true);
});
test('rapid scrolling coalesces requests and obsolete in-flight responses never overwrite the newest window',async()=>{
 const s=setup(180);await open();const grid=screen.getByLabelText('Worksheet viewport');
 fireEvent.scroll(grid,{target:{scrollTop:100*28}});await new Promise(r=>setTimeout(r,100));
 for(let n=200;n<=240;n++)fireEvent.scroll(grid,{target:{scrollTop:n*28}});
 await screen.findByRole('button',{name:'Inspect A241'});expect(s.peak()).toBe(1);
 expect(s.messages.filter(m=>m.command==='workbook.readViewport').length).toBeLessThanOrEqual(3);
 expect(screen.queryByRole('button',{name:'Inspect A101'})).toBeNull();
});
test('direct navigation reaches the final Excel cell without an unbounded request',async()=>{
 const s=setup();await open();fireEvent.change(screen.getByRole('textbox',{name:'Go To Cell'}),{target:{value:'XFD1048576'}});fireEvent.click(screen.getByRole('button',{name:'Go'}));
 await screen.findByRole('button',{name:'Inspect XFD1048576'});expect(screen.getByLabelText('Read-only cell details').textContent).toContain('Inspect XFD1048576');
 expect(s.messages.filter(m=>m.command==='workbook.readViewport').at(-1)?.payload).toMatchObject({startRow:1048557,startColumn:16379,rows:20,columns:6});
 fireEvent.keyDown(screen.getByLabelText('Worksheet viewport'),{key:'Home',ctrlKey:true});await screen.findByRole('button',{name:'Inspect A1'});
});
test.each(['Close workbook','Open workbook','Inspect workbook read-only','worksheet'])('review decisions survive canceled %s and require explicit discard',async action=>{
 const s=setup();await open();fireEvent.click(screen.getByRole('button',{name:'Select profile and start review'}));await screen.findByRole('table',{name:'Review coverage'});fireEvent.click(screen.getByRole('button',{name:'Unassigned Rows'}));
 fireEvent.change(screen.getByRole('textbox',{name:'Review reason'}),{target:{value:'Artificial investigation'}});fireEvent.click(screen.getByRole('button',{name:'Confirm read-only assertion'}));await ready();
 const trigger=()=>action==='worksheet'?fireEvent.change(screen.getByRole('combobox',{name:'Worksheet'}),{target:{value:'44444444-4444-4444-4444-444444444444'}}):fireEvent.click(screen.getByRole('button',{name:action}));
 const before=s.messages.length;trigger();expect(screen.getByRole('alertdialog').textContent).toContain('1 in-memory review decision');expect(s.messages.length).toBe(before);
 fireEvent.click(screen.getByRole('button',{name:'Continue Reviewing'}));expect(screen.getByRole('textbox',{name:'Review reason'})).toBeDefined();
 trigger();fireEvent.click(screen.getByRole('button',{name:'Discard & Close'}));await waitFor(()=>expect(screen.queryByLabelText('Review coverage')).toBeNull());
});
