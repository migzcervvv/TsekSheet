import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import App from './App';
import type { Transport } from './bridge/client';
import { validExcelCatalog } from './bridge/excel';

afterEach(()=>{cleanup();delete (window as any).chrome;});
const empty={source:null,version:null,fields:[],pageNumber:1,status:'NotStarted'};
const target={id:'b'.repeat(32),name:'Selected.xlsx',sheets:['Columns']};
const other={id:'a'.repeat(32),name:'Other.xlsx',sheets:['Columns']};
function host(){
  const listeners=new Set<(event:{data:unknown})=>void>();
  const calls:{command:string;payload:any}[]=[];
  let failure=false,cancel=false,pending=false,selectOther=false;
  const transport:Transport={addEventListener:(_,l)=>{listeners.add(l);},removeEventListener:(_,l)=>{listeners.delete(l);},postMessage:message=>{
    const m=message as {command:string;payload:any;requestId:string};calls.push(m);
    queueMicrotask(()=>{
      const queue={activeId:pending?'c'.repeat(32):null,items:pending?[{id:'c'.repeat(32),displayName:'Synthetic.pdf',status:'Pending'}]:[],receipt:empty};
      let result:unknown;
      if(m.command==='foundation.handshake')result={selectedVersion:1,applicationName:'TsekSheet',version:'1',capabilities:{diagnostics:true,fileSelection:true,workbooks:true,receipts:true,templates:false,ocr:false}};
      if(m.command==='foundation.getInfo')result={applicationName:'TsekSheet',version:'1',platform:'Windows',hasUnsavedWork:false};
      if(m.command==='workflow.profiles')result={profiles:[{id:'synthetic',displayName:'Synthetic',revision:1,profileHash:'A'.repeat(64),fields:['vendor','rfp','amount'].map(key=>({key,header:key,kind:'text',required:true,maxLength:256}))}]};
      if(m.command==='receipt.state')result=empty;
      if(m.command==='receipt.queue')result=queue;
      if(m.command==='receipt.enqueue'){pending=true;result={...queue,activeId:'c'.repeat(32),items:[{id:'c'.repeat(32),displayName:'Synthetic.pdf',status:'Pending'}]};}
      if(m.command==='excel.listWorkbooks')result={openedWorkbookId:m.payload.open&&!cancel?(selectOther?other.id:target.id):null,workbooks:[other,target],sections:[],changes:[]};
      const fail=failure&&m.command==='excel.listWorkbooks';
      listeners.forEach(l=>l({data:{protocolVersion:1,type:'response',requestId:m.requestId,ok:!fail,...(fail?{error:{code:'EXCEL_BUSY',message:'Excel is busy.'}}:{result})}}));
    });
  }};
  (window as any).chrome={webview:transport};
  return {calls,selectOther:()=>selectOther=true,fail:(v:boolean)=>failure=v,cancel:(v:boolean)=>cancel=v};
}
async function open(){fireEvent.click((await screen.findAllByRole('button',{name:'Open workbook'}))[0]);await screen.findByText('Selected.xlsx',{selector:'strong'});}

it('adopts the opened Excel identity and shows the queue directly without inspection',async()=>{
  const h=host();render(<App/>);await screen.findByText('Connected');await open();
  expect(screen.getByRole('heading',{level:1}).textContent).toBe('Process Receipts');
  expect(await screen.findByRole('button',{name:'Add PDFs or images'})).toBeTruthy();
  expect(screen.queryByText('Workbook opened in inspection mode')).toBeNull();
  expect(h.calls.filter(c=>c.command==='excel.listWorkbooks'&&c.payload.open)).toHaveLength(1);
  expect(h.calls.some(c=>c.command==='workbook.open'||c.command==='workbook.inspect')).toBe(false);
  fireEvent.click(screen.getByText('Secondary tools'));
  fireEvent.click(screen.getByRole('button',{name:'Legacy receipt review'}));
  expect(screen.getByRole('heading',{level:1}).textContent).toBe('Legacy receipt review');
  fireEvent.click(screen.getByRole('button',{name:'Process Receipts'}));
  expect(screen.getByText('Selected.xlsx',{selector:'strong'})).toBeTruthy();
});

it('retains the active workspace after cancellation or failure and provides recovery',async()=>{
  const h=host();render(<App/>);await screen.findByText('Connected');await open();
  h.cancel(true);fireEvent.click(screen.getByRole('button',{name:'Switch workbook…'}));
  await waitFor(()=>expect((screen.getByRole('button',{name:'Switch workbook…'}) as HTMLButtonElement).disabled).toBe(false));
  expect(screen.getByText('Selected.xlsx',{selector:'strong'})).toBeTruthy();
  h.cancel(false);h.fail(true);fireEvent.click(screen.getByRole('button',{name:'Switch workbook…'}));
  await screen.findByRole('alert');expect(screen.getByRole('button',{name:'Retry workbook opening'})).toBeTruthy();
  expect(screen.getByText('Selected.xlsx',{selector:'strong'})).toBeTruthy();
  expect(h.calls.some(c=>c.command==='workbook.inspect')).toBe(false);
});

it('blocks switching before the file dialog when queue work exists across navigation',async()=>{
  const h=host();render(<App/>);await screen.findByText('Connected');await open();
  fireEvent.click(await screen.findByRole('button',{name:'Add PDFs or images'}));
  await screen.findByRole('option',{name:'Synthetic.pdf · Pending'});
  fireEvent.click(screen.getByRole('button',{name:'Settings'}));
  fireEvent.click(screen.getByRole('button',{name:'Switch workbook…'}));
  expect((await screen.findByRole('alert')).textContent).toContain('switching is blocked');
  expect(h.calls.filter(c=>c.command==='excel.listWorkbooks'&&c.payload.open)).toHaveLength(1);
  fireEvent.click(screen.getByRole('button',{name:'Process Receipts'}));
  expect(screen.getByRole('option',{name:'Synthetic.pdf · Pending'})).toBeTruthy();
  fireEvent.click(screen.getByText('Secondary tools'));
  fireEvent.click(screen.getByRole('button',{name:'Legacy receipt review'}));
  expect(screen.getByRole('heading',{level:1}).textContent).toBe('Process Receipts');
  expect(screen.getByRole('alert').textContent).toContain('drafts and destination bindings');
});

it('validates opened identity against the catalog and rejects paths or absent identities',()=>{
  const catalog={openedWorkbookId:target.id,workbooks:[other,target],sections:[],changes:[]};
  expect(validExcelCatalog(catalog)).toBe(true);
  expect(validExcelCatalog({...catalog,openedWorkbookId:'c'.repeat(32)})).toBe(false);
  expect(validExcelCatalog({...catalog,openedWorkbookId:'C:\\private.xlsx'})).toBe(false);
});

it('switches an empty workspace safely without choosing by catalog order',async()=>{
 const h=host();render(<App/>);await screen.findByText('Connected');await open();
 await screen.findByRole('button',{name:'Add PDFs or images'});h.selectOther();
 fireEvent.click(screen.getByRole('button',{name:'Switch workbook…'}));
 await screen.findByText('Other.xlsx',{selector:'strong'});
 expect(screen.getByRole('heading',{level:1}).textContent).toBe('Process Receipts');
 expect(h.calls.filter(c=>c.command==='excel.listWorkbooks'&&c.payload.open)).toHaveLength(2);
});
