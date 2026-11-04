import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import FastReceiptLoop from './FastReceiptLoop';
import type { BridgeClient } from './bridge/client';
import { validReceiptQueue, type ReceiptQueueState, type ReceiptState } from './bridge/receipts';
import type { WorkflowProfile } from './bridge/workflows';

afterEach(cleanup);
const profile:WorkflowProfile={id:'synthetic-liquidation',displayName:'Synthetic liquidation',revision:1,profileHash:'A'.repeat(64),fields:[{key:'vendor',header:'Vendor',kind:'text',required:false,maxLength:256},{key:'itemNo',header:'Item no.',kind:'text',required:true,maxLength:256},{key:'account',header:'Account',kind:'accountChoice',required:true,maxLength:256},{key:'amount',header:'Amount',kind:'money',required:true,maxLength:256}],accounts:[{code:'FAKE',label:'Travel',column:'J',vatable:true},{code:'FAKE',label:'Travel',column:'Q',vatable:false}]};
function setup(count=3){
  const states:Record<string,ReceiptState>={};
  const items=Array.from({length:count},(_,i)=>({id:String(i+1).padStart(32,'a'),displayName:`synthetic-${i}.pdf`,status:'Pending' as const}));
  let active=items[0].id;
  const blank=():ReceiptState=>({source:{displayName:items.find(i=>i.id===active)!.displayName,document:{version:{documentId:'11111111-1111-4111-8111-111111111111',revision:0,sha256:'A'.repeat(64)},kind:0,byteLength:128,status:0}},version:null,fields:[],pageNumber:1,status:'NotStarted'});
  for(const item of items){active=item.id;states[active]=blank();}active=items[0].id;
  function queue():ReceiptQueueState{return structuredClone({activeId:active,items,receipt:states[active]});}
  let failure='';
  const other={...profile,id:'other-liquidation',displayName:'Other liquidation'};
  const request=vi.fn((command:string,p:any)=>({promise:Promise.resolve().then(()=>{
    if(failure&&command==='excel.addRow')throw new Error(failure);
    if(command==='receipt.queue'||command==='receipt.enqueue')return queue();
    if(command==='receipt.start'){states[active]={...states[active],version:{reviewId:`11111111-1111-4111-8111-${active.slice(-12).replace(/a/g,'0')}`,revision:0,profileId:profile.id,profileHash:profile.profileHash},status:'Active',fields:profile.fields.map(f=>({key:f.key,value:'',valueRevision:0,status:'Draft'}))};return structuredClone(states[active]);}
    if(command==='excel.listWorkbooks')return {workbooks:[{id:'b'.repeat(32),name:'Synthetic.xlsx',sheets:['Person 1','Person 2']}],sections:[{id:'expenses',label:'Expenses',sheetName:''},{id:'credits',label:'Credits',sheetName:''}],changes:[]};
    if(command==='receipt.changeProfile'){const state=states[active];state.version={...state.version!,profileId:p.profileId,revision:0};state.fields=state.fields.map(f=>({...f,value:p.values[f.key],status:'Draft'}));return structuredClone(state);}
    if(command==='receipt.prepare'){const state=states[active];state.version!.revision++;state.fields=state.fields.map(f=>({...f,value:p.values[f.key],status:'Corrected'}));return structuredClone(state);}
    if(command==='excel.previewDraft')return {token:'c'.repeat(32),workbookId:p.workbookId,sectionId:p.sectionId,sheet:p.sheet,range:'B29:AF29',insertRow:29,values:[{key:'amount',header:'Amount',column:states[active].fields.find(f=>f.key==='account')?.value==='J'?10:17,value:123.5,displayValue:'123.50'}],manualReview:[]};
    if(command==='excel.addRow'){(items.find(i=>i.id===active)! as {status:string}).status='Inserted';active=items.find(i=>i.status==='Pending')?.id??active;return {change:{workbook:'Synthetic.xlsx',sheet:'Person 1',range:'B29:AF29',manualReview:[]},queue:queue()};}
    if(command==='receipt.navigate'){if(p.skip)(items.find(i=>i.id===active)! as {status:string}).status='Skipped';active=p.targetId;return queue();}
    throw new Error(command);
  })}));
  render(<FastReceiptLoop bridge={{request} as unknown as BridgeClient} profiles={[profile,other]} disabled={false} renderingAvailable={false}/>);
  return {request,items,fail:(message:string)=>{failure=message;}};
}
async function start(){fireEvent.click(await screen.findByRole('button',{name:'Start receipt queue'}));await screen.findByLabelText('Queue itemNo');}
async function destination(){fireEvent.click(screen.getByRole('button',{name:'Open workbook in Excel'}));await screen.findByRole('option',{name:/Synthetic.xlsx/});fireEvent.change(screen.getByLabelText('Queue sheet'),{target:{value:'Person 1'}});fireEvent.change(screen.getByLabelText('Queue section'),{target:{value:'expenses'}});fireEvent.click(screen.getByRole('button',{name:'Use destination for this receipt and new receipts'}));}
function fields(id='LOOP-1'){fireEvent.change(screen.getByLabelText('Queue itemNo'),{target:{value:id}});fireEvent.change(screen.getByLabelText('Queue account'),{target:{value:'Q'}});fireEvent.change(screen.getByLabelText('Queue amount'),{target:{value:'123.50'}});}

it('keeps one destination across twenty confirmed inserts, inherits account as draft and advances automatically',async()=>{
  const f=setup(20);await start();await destination();
  for(let i=0;i<20;i++){
    await waitFor(()=>expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).disabled).toBe(false));
    if(i)expect((screen.getByLabelText('Queue account') as HTMLSelectElement).value).toBe('Q');
    fields(`LOOP-${i}`);
    expect(screen.getAllByText('Draft — confirmation required')).toHaveLength(4);
    fireEvent.click(screen.getByRole('button',{name:'Preview row'}));
    fireEvent.click(await screen.findByRole('button',{name:'Add Row — confirm values and destination'}));
    await waitFor(()=>expect(f.request.mock.calls.filter(c=>c[0]==='excel.addRow')).toHaveLength(i+1));
    await waitFor(()=>expect(screen.getByText(new RegExp(`Added synthetic-${i}.pdf`))).toBeTruthy());
  }
  expect(f.request.mock.calls.filter(c=>c[0]==='excel.listWorkbooks')).toHaveLength(1);
  expect(f.request.mock.calls.filter(c=>c[0]==='excel.previewDraft').every(c=>c[1].sheet==='Person 1'&&c[1].sectionId==='expenses')).toBe(true);
  expect(screen.getByLabelText('Inserted queue rows').querySelectorAll('li')).toHaveLength(20);
});

it('preserves skipped drafts and their destination when another receipt explicitly changes destination',async()=>{
  const f=setup();await start();await destination();fields();
  fireEvent.click(screen.getByRole('button',{name:'Skip receipt'}));
  await waitFor(()=>expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).value).toBe(''));
  fireEvent.change(screen.getByLabelText('Queue sheet'),{target:{value:'Person 2'}});
  fireEvent.change(screen.getByLabelText('Queue section'),{target:{value:'credits'}});
  fields('LOOP-2');
  expect((screen.getByRole('button',{name:'Preview row'}) as HTMLButtonElement).disabled).toBe(true);
  expect(screen.getByText('Destination change needs confirmation')).toBeTruthy();
  fireEvent.click(screen.getByRole('button',{name:'Use destination for this receipt and new receipts'}));
  fireEvent.change(screen.getByLabelText('Review receipt'),{target:{value:f.items[0].id}});
  await waitFor(()=>expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).value).toBe('LOOP-1'));
  expect((screen.getByLabelText('Queue sheet') as HTMLSelectElement).value).toBe('Person 1');
  expect((screen.getByLabelText('Queue section') as HTMLSelectElement).value).toBe('expenses');
  expect(screen.getByRole('option',{name:'synthetic-0.pdf · Skipped'})).toBeTruthy();
});

it('blocks missing values, clears a preview after editing and retains the receipt on stale or duplicate rejection',async()=>{
  const f=setup();await start();await destination();
  expect((screen.getByRole('button',{name:'Preview row'}) as HTMLButtonElement).disabled).toBe(true);
  fields();fireEvent.click(screen.getByRole('button',{name:'Preview row'}));await screen.findByLabelText('Queue row preview');
  fireEvent.change(screen.getByLabelText('Queue amount'),{target:{value:'10'}});
  expect(screen.queryByLabelText('Queue row preview')).toBeNull();
  fireEvent.click(screen.getByRole('button',{name:'Preview row'}));await screen.findByLabelText('Queue row preview');
  f.fail('The workbook changed after preview. Request a new preview.');
  fireEvent.click(screen.getByRole('button',{name:'Add Row — confirm values and destination'}));
  await screen.findByRole('alert');expect(screen.getByText(/The workbook changed after preview/)).toBeTruthy();
  expect((screen.getByLabelText('Review receipt') as HTMLSelectElement).value).toBe(f.items[0].id);
  expect(screen.queryByRole('button',{name:'Add Row — confirm values and destination'})).toBeNull();
});

it('rejects queue responses containing native paths, duplicate IDs or unknown active receipts',()=>{
  const q={activeId:'a'.repeat(32),items:[{id:'a'.repeat(32),displayName:'synthetic.pdf',status:'Pending'}],receipt:{source:null,version:null,pageNumber:1,status:'NotStarted',fields:[]}};
  expect(validReceiptQueue(q)).toBe(true);
  expect(validReceiptQueue({...q,items:[...q.items,...q.items]})).toBe(false);
  expect(validReceiptQueue({...q,activeId:'b'.repeat(32)})).toBe(false);
  expect(validReceiptQueue({...q,items:[{...q.items[0],displayName:'C:\\private.pdf'}]})).toBe(false);
});

it('changes profile only explicitly, retaining matching unsaved fields as drafts and requiring a fresh destination',async()=>{
  const f=setup();await start();await destination();fields();
  fireEvent.change(screen.getByLabelText('Queue profile'),{target:{value:'other-liquidation'}});
  expect((screen.getByRole('button',{name:'Preview row'}) as HTMLButtonElement).disabled).toBe(true);
  expect(f.request.mock.calls.some(c=>c[0]==='receipt.changeProfile')).toBe(false);
  fireEvent.click(screen.getByRole('button',{name:'Change profile for this receipt and new receipts'}));
  await waitFor(()=>expect((screen.getByLabelText('Queue workbook') as HTMLSelectElement).value).toBe(''));
  expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).value).toBe('LOOP-1');
  expect((screen.getByLabelText('Queue amount') as HTMLInputElement).value).toBe('123.50');
  expect(screen.getAllByText('Draft — confirmation required')).toHaveLength(4);
});
