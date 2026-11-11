import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import FastReceiptLoop from './FastReceiptLoop';
import { BridgeError, BridgeTransportError } from './bridge/client';
import type { BridgeClient } from './bridge/client';
import { validReceiptQueue, type ReceiptQueueState, type ReceiptState } from './bridge/receipts';
import type { WorkflowProfile } from './bridge/workflows';

afterEach(()=>{cleanup();vi.unstubAllGlobals();});
const profile:WorkflowProfile={id:'synthetic-liquidation',displayName:'Synthetic liquidation',revision:1,profileHash:'A'.repeat(64),fields:[{key:'vendor',header:'Vendor',kind:'text',required:false,maxLength:256},{key:'itemNo',header:'Item no.',kind:'text',required:true,maxLength:256},{key:'account',header:'Account',kind:'accountChoice',required:true,maxLength:256},{key:'amount',header:'Amount',kind:'money',required:true,maxLength:256}],accounts:[{code:'FAKE',label:'Travel',column:'J',vatable:true},{code:'FAKE',label:'Travel',column:'Q',vatable:false}]};
function setup(count=3,workspace=false,options:{empty?:boolean;render?:boolean}={},entryProfile=profile){
  let imported=!options.empty,holdPreview=false;let finishPreview!:()=>void,finishOcr!:(value:unknown)=>void;
  let warnings:any[]=[];
  let advisoryWarnings:string[]=[];
  let previewNumber=0;
  if(options.render)vi.stubGlobal('URL',{createObjectURL:()=>'blob:synthetic',revokeObjectURL:vi.fn()});
  const states:Record<string,ReceiptState>={};
  const items=Array.from({length:count},(_,i)=>({id:String(i+1).padStart(32,'a'),displayName:`synthetic-${i}.pdf`,status:'Pending' as const}));
  let active=items[0].id;
  const blank=():ReceiptState=>({source:{displayName:items.find(i=>i.id===active)!.displayName,document:{version:{documentId:'11111111-1111-4111-8111-111111111111',revision:0,sha256:'A'.repeat(64)},kind:0,byteLength:128,status:0}},version:null,fields:[],pageNumber:1,status:'NotStarted'});
  for(const item of items){active=item.id;states[active]=blank();}active=items[0].id;
  function queue():ReceiptQueueState{return structuredClone(imported?{activeId:active,items,receipt:states[active]}:{activeId:null,items:[],receipt:{source:null,version:null,pageNumber:1,status:'NotStarted',fields:[]}});}
  let failure:Error|null=null;
  const other={...entryProfile,id:'other-liquidation',displayName:'Other liquidation'};
  const request=vi.fn((command:string,p:any)=>({promise:Promise.resolve().then(()=>{
    if(failure&&command==='excel.addRow')throw failure;
    if(command==='receipt.queue')return queue();
    if(command==='receipt.enqueue'){imported=true;return queue();}
    if(command==='receipt.cancelRender')return {status:'requested'};
    if(command==='receipt.render')return {source:states[active].source!.document.version,page:{pageNumber:p.pageNumber,pageCount:2,width:1,height:1,pngBase64:'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR1sAAAAASUVORK5CYII='}};
    if(command==='receipt.ocr')return new Promise(resolve=>{finishOcr=resolve;});
    if(command==='receipt.start'){states[active]={...states[active],version:{reviewId:`11111111-1111-4111-8111-${active.slice(-12).replace(/a/g,'0')}`,revision:0,profileId:entryProfile.id,profileHash:entryProfile.profileHash},status:'Active',fields:entryProfile.fields.map(f=>({key:f.key,value:'',valueRevision:0,status:'Draft'}))};return structuredClone(states[active]);}
    if(command==='excel.listWorkbooks')return {openedWorkbookId:'b'.repeat(32),workbooks:[{id:'b'.repeat(32),name:'Synthetic.xlsx',sheets:['Person 1','Person 2']}],sections:[{id:'expenses',label:'Expenses',sheetName:''},{id:'credits',label:'Credits',sheetName:''}],changes:[]};
    if(command==='receipt.changeProfile'){const state=states[active];state.version={...state.version!,profileId:p.profileId,revision:0};state.fields=state.fields.map(f=>({...f,value:p.values[f.key],status:'Draft'}));return structuredClone(state);}
    if(command==='receipt.prepare'){const state=states[active];state.version!.revision++;state.fields=state.fields.map(f=>({...f,value:p.values[f.key],status:'Corrected'}));return structuredClone(state);}
    if(command==='excel.previewDraft'){const result={token:(++previewNumber).toString(16).padStart(32,'c'),workbookId:p.workbookId,sectionId:p.sectionId,sheet:p.sheet,range:'B29:AF29',insertRow:29,values:[{key:'amount',header:'Amount',column:states[active].fields.find(f=>f.key==='account')?.value==='J'?10:17,value:123.5,displayValue:'123.50'}],manualReview:warnings,advisoryWarnings};return holdPreview?new Promise(resolve=>{finishPreview=()=>resolve(result);}):result;}
    if(command==='excel.addRow'){(items.find(i=>i.id===active)! as {status:string}).status='Inserted';active=items.find(i=>i.status==='Pending')?.id??active;return {change:{workbook:'Synthetic.xlsx',sheet:'Person 1',range:'B29:AF29',manualReview:[]},queue:queue()};}
    if(command==='receipt.navigate'){if(p.skip)(items.find(i=>i.id===active)! as {status:string}).status='Skipped';active=p.targetId;return queue();}
    throw new Error(command);
  })}));
  render(<FastReceiptLoop activeWorkbook={workspace?{id:'b'.repeat(32),name:'Synthetic.xlsx',sheets:['Person 1','Person 2']}:undefined} bridge={{request} as unknown as BridgeClient} profiles={[entryProfile,other]} disabled={false} renderingAvailable={options.render??false}/>);
  return {request,items,fail:(message:string|Error)=>{failure=typeof message==='string'?new Error(message):message;},hold:()=>holdPreview=true,finish:()=>finishPreview(),ocr:(values:Record<string,string>)=>finishOcr({values,words:[{text:'123.50',confidence:.9,x:.1,y:.1,width:.2,height:.1}]}),warnings:(value:any[])=>warnings=value,advisories:(value:string[])=>advisoryWarnings=value};
}
async function start(){await screen.findByLabelText('Queue itemNo');}
function configuration(){const details=screen.getByText('Profile and destination').closest('details')!;if(!details.open)fireEvent.click(screen.getByText('Profile and destination'));}
async function destination(){fireEvent.click(screen.getByRole('button',{name:'Open workbook in Excel'}));await screen.findByRole('option',{name:/Synthetic.xlsx/});fireEvent.change(screen.getByLabelText('Queue sheet'),{target:{value:'Person 1'}});fireEvent.change(screen.getByLabelText('Queue section'),{target:{value:'expenses'}});fireEvent.click(screen.getByRole('button',{name:'Use destination for this receipt and new receipts'}));}
function fields(id='LOOP-1'){fireEvent.change(screen.getByLabelText('Queue itemNo'),{target:{value:id}});fireEvent.change(screen.getByLabelText('Queue account'),{target:{value:'Q'}});fireEvent.change(screen.getByLabelText('Queue amount'),{target:{value:'123.50'}});}

it('keeps one destination across twenty confirmed inserts, inherits account as draft and advances automatically',async()=>{
  const f=setup(20);await start();await destination();
  for(let i=0;i<20;i++){
    await waitFor(()=>expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).disabled).toBe(false));
    if(i)expect((screen.getByLabelText('Queue account') as HTMLSelectElement).value).toBe('Q');
    fields(`LOOP-${i}`);
    expect(screen.getByText('Pending · Unverified drafts')).toBeTruthy();
    fireEvent.click(screen.getByRole('button',{name:'Check row'}));
    fireEvent.click(await screen.findByRole('button',{name:'Add row to Excel'}));
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
  configuration();fireEvent.change(screen.getByLabelText('Queue sheet'),{target:{value:'Person 2'}});
  fireEvent.change(screen.getByLabelText('Queue section'),{target:{value:'credits'}});
  fields('LOOP-2');
  expect((screen.getByRole('button',{name:'Check row'}) as HTMLButtonElement).disabled).toBe(true);
  expect(screen.getByText(/Destination change needs confirmation/)).toBeTruthy();
  fireEvent.click(screen.getByRole('button',{name:'Use destination for this receipt and new receipts'}));
  fireEvent.change(screen.getByLabelText('Review receipt'),{target:{value:f.items[0].id}});
  await waitFor(()=>expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).value).toBe('LOOP-1'));
  expect((screen.getByLabelText('Queue sheet') as HTMLSelectElement).value).toBe('Person 1');
  expect((screen.getByLabelText('Queue section') as HTMLSelectElement).value).toBe('expenses');
  expect(screen.getByRole('option',{name:'synthetic-0.pdf · Skipped'})).toBeTruthy();
});

it('blocks missing values, clears a preview after editing and retains the receipt on stale or duplicate rejection',async()=>{
  const f=setup();await start();await destination();
  expect((screen.getByRole('button',{name:'Check row'}) as HTMLButtonElement).disabled).toBe(true);
  fields();fireEvent.click(screen.getByRole('button',{name:'Check row'}));await screen.findByLabelText('Queue row preview');
  fireEvent.click(screen.getByRole('button',{name:'Back to edit'}));fireEvent.change(screen.getByLabelText('Queue amount'),{target:{value:'10'}});
  expect(screen.queryByLabelText('Queue row preview')).toBeNull();
  fireEvent.click(screen.getByRole('button',{name:'Check row'}));await screen.findByLabelText('Queue row preview');
  f.fail(new BridgeError('STALE_INSERT_PREVIEW','The workbook changed after preview. Request a new preview.'));
  fireEvent.click(screen.getByRole('button',{name:'Add row to Excel'}));
  await screen.findByRole('alert');expect(screen.getByText(/The workbook changed after preview/)).toBeTruthy();
  expect((screen.getByLabelText('Review receipt') as HTMLSelectElement).value).toBe(f.items[0].id);
  expect(screen.queryByRole('button',{name:'Add row to Excel'})).toBeNull();
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
  configuration();fireEvent.change(screen.getByLabelText('Queue profile'),{target:{value:'other-liquidation'}});
  expect((screen.getByRole('button',{name:'Check row'}) as HTMLButtonElement).disabled).toBe(true);
  expect(f.request.mock.calls.some(c=>c[0]==='receipt.changeProfile')).toBe(false);
  fireEvent.click(screen.getByRole('button',{name:'Change profile for this receipt and new receipts'}));
  await waitFor(()=>expect((screen.getByLabelText('Queue workbook') as HTMLSelectElement).value).toBe(''));
  expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).value).toBe('LOOP-1');
  expect((screen.getByLabelText('Queue amount') as HTMLInputElement).value).toBe('123.50');
  expect(screen.getByText('Pending · Unverified drafts')).toBeTruthy();
});

it('uses the active workspace without reopening Excel and retains it after an explicit profile change',async()=>{
 const f=setup(2,true);await start();await screen.findByRole('option',{name:'Person 1'});
 expect(screen.queryByLabelText('Queue workbook')).toBeNull();
 expect(screen.queryByRole('button',{name:'Open workbook in Excel'})).toBeNull();
 fireEvent.change(screen.getByLabelText('Queue sheet'),{target:{value:'Person 1'}});
 fireEvent.change(screen.getByLabelText('Queue section'),{target:{value:'expenses'}});
 fireEvent.click(screen.getByRole('button',{name:'Use destination for this receipt and new receipts'}));fields();
 fireEvent.click(screen.getByRole('button',{name:'Check row'}));await screen.findByLabelText('Queue row preview');
 expect(f.request.mock.calls.find(c=>c[0]==='excel.previewDraft')?.[1].workbookId).toBe('b'.repeat(32));
 expect(f.request.mock.calls.filter(c=>c[0]==='excel.listWorkbooks').every(c=>c[1].open===false)).toBe(true);
 configuration();fireEvent.change(screen.getByLabelText('Queue profile'),{target:{value:'other-liquidation'}});
 fireEvent.click(screen.getByRole('button',{name:'Change profile for this receipt and new receipts'}));
 await waitFor(()=>expect((screen.getByLabelText('Queue section') as HTMLSelectElement).value).toBe(''));
 expect(screen.queryByLabelText('Queue workbook')).toBeNull();
 expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).value).toBe('LOOP-1');
});

it.each([1,3])('imports %i receipts through the same Add receipts and Edit / Check states',async count=>{
 const f=setup(count,true,{empty:true});
 const add=await screen.findByRole('button',{name:'Add receipts'});await waitFor(()=>expect((add as HTMLButtonElement).disabled).toBe(false));
 fireEvent.click(add);await start();await screen.findByRole('option',{name:'Person 1'});
 expect(screen.getByLabelText('Queue progress').textContent).toBe(`${count} Pending · 0 Skipped · 0 Inserted`);
 expect(screen.queryByRole('button',{name:'Receipt queue'})).toBeNull();
 fireEvent.change(screen.getByLabelText('Queue sheet'),{target:{value:'Person 1'}});fireEvent.change(screen.getByLabelText('Queue section'),{target:{value:'expenses'}});
 fireEvent.click(screen.getByRole('button',{name:'Use destination for this receipt and new receipts'}));fields();
 expect(document.querySelectorAll('.fast-receipt-loop button.primary')).toHaveLength(1);
 fireEvent.click(screen.getByRole('button',{name:'Check row'}));await screen.findByLabelText('Queue row preview');
 expect(screen.queryByLabelText('Queue amount')).toBeNull();
 expect(screen.getByText('123.50',{selector:'td'})).toBeTruthy();
 expect(document.querySelectorAll('.fast-receipt-loop button.primary')).toHaveLength(1);
 expect(screen.getByRole('button',{name:'Back to edit'})).toBeTruthy();
 expect(f.request.mock.calls.filter(c=>c[0]==='excel.addRow')).toHaveLength(0);
 expect(f.request.mock.calls.filter(c=>c[0]==='excel.listWorkbooks').every(c=>!c[1].open)).toBe(true);
});

it('keeps visited blank accounts blank and inherits the active account only for a new receipt',async()=>{
 setup(3);await start();fireEvent.click(screen.getByRole('button',{name:'Next receipt'}));await waitFor(()=>expect((screen.getByLabelText('Review receipt') as HTMLSelectElement).selectedIndex).toBe(1));
 fireEvent.click(screen.getByRole('button',{name:'Previous receipt'}));await waitFor(()=>expect((screen.getByLabelText('Review receipt') as HTMLSelectElement).selectedIndex).toBe(0));
 fireEvent.change(screen.getByLabelText('Queue account'),{target:{value:'Q'}});
 fireEvent.click(screen.getByRole('button',{name:'Next receipt'}));await waitFor(()=>expect((screen.getByLabelText('Review receipt') as HTMLSelectElement).selectedIndex).toBe(1));
 expect((screen.getByLabelText('Queue account') as HTMLSelectElement).value).toBe('');
 fireEvent.click(screen.getByRole('button',{name:'Next receipt'}));await waitFor(()=>expect((screen.getByLabelText('Queue account') as HTMLSelectElement).value).toBe('Q'));
});

it('invalidates Check immediately when the destination changes and requires another explicit binding',async()=>{
 const f=setup();await start();await destination();fields();fireEvent.click(screen.getByRole('button',{name:'Check row'}));await screen.findByLabelText('Queue row preview');
 configuration();fireEvent.change(screen.getByLabelText('Queue sheet'),{target:{value:'Person 2'}});
 expect(screen.queryByRole('button',{name:'Add row to Excel'})).toBeNull();
 expect((screen.getByRole('button',{name:'Check row'}) as HTMLButtonElement).disabled).toBe(true);
 expect(f.request.mock.calls.some(c=>c[0]==='excel.addRow')).toBe(false);
});

it('shows every manual-review warning and keeps insertion blocked until a fresh safe Check',async()=>{
 const f=setup();await start();await destination();fields();f.warnings([{cell:'J31:J31',reason:'Review subtotal in Excel.'},{cell:'Q31:Q31',reason:'Review second subtotal.'}]);
 fireEvent.click(screen.getByRole('button',{name:'Check row'}));await screen.findByLabelText('Queue row preview');
 expect(screen.getByText('Review subtotal in Excel.')).toBeTruthy();expect(screen.getByText('Review second subtotal.')).toBeTruthy();
 expect((screen.getByRole('button',{name:'Add row to Excel'}) as HTMLButtonElement).disabled).toBe(true);
});

it('never resubmits an uncertain insertion after navigation or refresh',async()=>{
 const f=setup();await start();await destination();fields();fireEvent.click(screen.getByRole('button',{name:'Check row'}));await screen.findByLabelText('Queue row preview');
 f.fail(new BridgeTransportError('timeout',true,'The insertion response was lost.'));
 fireEvent.click(screen.getByRole('button',{name:'Add row to Excel'}));await screen.findByRole('alert');
 expect(screen.getByText(/insertion result is unknown/)).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Next receipt'}));await waitFor(()=>expect((screen.getByLabelText('Review receipt') as HTMLSelectElement).selectedIndex).toBe(1));
 fireEvent.click(screen.getByRole('button',{name:'Previous receipt'}));await waitFor(()=>expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).value).toBe('LOOP-1'));
 fireEvent.click(screen.getByText('Queue tools'));fireEvent.click(screen.getByRole('button',{name:'Refresh queue'}));
 await waitFor(()=>expect((screen.getByRole('button',{name:'Check row'}) as HTMLButtonElement).disabled).toBe(true));
 expect(f.request.mock.calls.filter(c=>c[0]==='excel.addRow')).toHaveLength(1);
});

it('keeps typed values during OCR and preserves source highlighting with manual correction',async()=>{
 const f=setup(1,true,{render:true});await start();await screen.findByAltText('Rendered source page 1');
 fireEvent.change(screen.getByLabelText('Queue amount'),{target:{value:'123.50'}});
 fireEvent.click(screen.getByRole('button',{name:'Recognize page'}));
 await waitFor(()=>expect(f.request.mock.calls.some(c=>c[0]==='receipt.ocr')).toBe(true));
 fireEvent.change(screen.getByLabelText('Queue itemNo'),{target:{value:'TYPED'}});
 f.ocr({vendor:'Synthetic OCR vendor',itemNo:'OCR VALUE',amount:'999'});
 await screen.findByLabelText('Source word 123.50');
 expect((screen.getByLabelText('Queue amount') as HTMLInputElement).value).toBe('123.50');expect((screen.getByLabelText('Queue itemNo') as HTMLInputElement).value).toBe('TYPED');
 expect((screen.getByLabelText('Queue vendor') as HTMLInputElement).value).toBe('Synthetic OCR vendor');
 fireEvent.change(screen.getByLabelText('Queue vendor'),{target:{value:'  '}});
 fireEvent.click(screen.getByRole('button',{name:'Recognize page'}));
 await waitFor(()=>expect(f.request.mock.calls.filter(c=>c[0]==='receipt.ocr')).toHaveLength(2));
 f.ocr({vendor:'Replacement proposal'});
 await waitFor(()=>expect((screen.getByRole('button',{name:'Recognize page'}) as HTMLButtonElement).disabled).toBe(false));
 expect((screen.getByLabelText('Queue vendor') as HTMLInputElement).value).toBe('  ');
 fireEvent.click(screen.getByLabelText('Source word 123.50'));await waitFor(()=>expect(document.activeElement).toBe(screen.getByLabelText('Queue amount')));
 expect(f.request.mock.calls.some(c=>/receipt.verify|excel.addRow/.test(c[0]))).toBe(false);
});

it('ignores a late Excel preview when the source page changes during Check',async()=>{
 const f=setup(1,false,{render:true});await start();await destination();fields();await screen.findByAltText('Rendered source page 1');
 f.hold();fireEvent.click(screen.getByRole('button',{name:'Check row'}));await waitFor(()=>expect(f.request.mock.calls.some(c=>c[0]==='excel.previewDraft')).toBe(true));
 fireEvent.click(screen.getByRole('button',{name:'Next source page'}));await screen.findByAltText('Rendered source page 2');f.finish();
 await waitFor(()=>expect((screen.getByLabelText('Queue amount') as HTMLInputElement).disabled).toBe(false));
 expect(screen.queryByLabelText('Queue row preview')).toBeNull();expect(screen.queryByRole('button',{name:'Add row to Excel'})).toBeNull();
});

it('checks manual values during pending OCR and ignores its late proposals after Check',async()=>{
 const f=setup(1,false,{render:true});await start();await destination();fields();await screen.findByAltText('Rendered source page 1');
 fireEvent.click(screen.getByRole('button',{name:'Recognize page'}));await waitFor(()=>expect(f.request.mock.calls.some(c=>c[0]==='receipt.ocr')).toBe(true));
 expect((screen.getByRole('button',{name:'Check row'}) as HTMLButtonElement).disabled).toBe(false);
 fireEvent.click(screen.getByRole('button',{name:'Check row'}));await screen.findByLabelText('Queue row preview');
 f.ocr({vendor:'Late vendor',amount:'999'});await screen.findByLabelText('Source word 123.50');
 expect(screen.getByLabelText('Queue row preview')).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Back to edit'}));
 expect((screen.getByLabelText('Queue vendor') as HTMLInputElement).value).toBe('');
 expect((screen.getByLabelText('Queue amount') as HTMLInputElement).value).toBe('123.50');
 expect(f.request.mock.calls.filter(c=>c[0]==='excel.addRow')).toHaveLength(0);
 expect(f.request.mock.calls.filter(c=>c[0]==='excel.previewDraft')).toHaveLength(1);
});

it('drains successive Check requests in order and offers only the latest preview for confirmation',async()=>{
  const f=setup();await start();await destination();fields();
  f.hold();
  const button=screen.getByRole('button',{name:'Check row'});
  fireEvent.click(button);
  await waitFor(()=>expect(f.request.mock.calls.filter(c=>c[0]==='excel.previewDraft')).toHaveLength(1));
  fireEvent.click(button);
  expect(f.request.mock.calls.filter(c=>c[0]==='receipt.prepare')).toHaveLength(1);
  expect(screen.queryByRole('button',{name:'Add row to Excel'})).toBeNull();
  f.finish();
  await waitFor(()=>expect(f.request.mock.calls.filter(c=>c[0]==='excel.previewDraft')).toHaveLength(2));
  const prepares=f.request.mock.calls.filter(c=>c[0]==='receipt.prepare');
  expect(prepares.map(c=>c[1].expected.revision)).toEqual([0,1]);
  expect(screen.queryByRole('button',{name:'Add row to Excel'})).toBeNull();
  f.finish();
  const add=await screen.findByRole('button',{name:'Add row to Excel'});
  expect(f.request.mock.calls.filter(c=>c[0]==='excel.addRow')).toHaveLength(0);
  fireEvent.click(add);
  fireEvent.click(add);
  await waitFor(()=>expect(f.request.mock.calls.filter(c=>c[0]==='excel.addRow')).toHaveLength(1));
  expect(f.request.mock.calls.find(c=>c[0]==='excel.addRow')?.[1]).toMatchObject({
    previewToken:'2'.padStart(32,'c'),expected:{revision:2}
  });
});

const payables:WorkflowProfile={id:'payables-rfp',displayName:'Payables — RFP',revision:1,profileHash:'A'.repeat(64),fields:[
  {key:'payee',header:'Payee',kind:'text',required:true,maxLength:256},
  {key:'rfpNo',header:'RFP No.',kind:'text',required:true,maxLength:256},
  {key:'amount',header:'Amount',kind:'money',required:true,maxLength:256},
  {key:'invoice',header:'Invoice #',kind:'text',required:false,maxLength:256}
],accounts:[]};
it('checks manual fixed-column drafts with blank invoice and leaves advisory duplicate insertion available',async()=>{
  const f=setup(1,false,{},payables);
  await screen.findByLabelText('Queue rfpNo');await destination();
  expect(screen.queryByLabelText('Queue account')).toBeNull();
  fireEvent.change(screen.getByLabelText('Queue rfpNo'),{target:{value:'00001'}});
  fireEvent.change(screen.getByLabelText('Queue amount'),{target:{value:'123.50'}});
  expect((screen.getByRole('button',{name:'Check row'}) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.change(screen.getByLabelText('Queue payee'),{target:{value:'Manual vendor'}});
  f.advisories(['Possible duplicate RFP No. Review Excel; insertion is still available.']);
  fireEvent.click(screen.getByRole('button',{name:'Check row'}));
  await screen.findByText('Advisory: possible duplicate');
  expect((screen.getByRole('button',{name:'Add row to Excel'}) as HTMLButtonElement).disabled).toBe(false);
  expect(f.request.mock.calls.find(c=>c[0]==='receipt.prepare')![1].values).toEqual({payee:'Manual vendor',rfpNo:'00001',amount:'123.50',invoice:''});
  fireEvent.click(screen.getByRole('button',{name:'Add row to Excel'}));
  await waitFor(()=>expect(f.request.mock.calls.some(c=>c[0]==='excel.addRow')).toBe(true));
});
