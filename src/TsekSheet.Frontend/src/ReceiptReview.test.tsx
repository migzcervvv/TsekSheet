import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';
import ReceiptReview from './ReceiptReview';
import { BridgeClient, type Transport } from './bridge/client';
import { validReceiptState, validReceiptPreview, type ReceiptState } from './bridge/receipts';

const clients: BridgeClient[] = [];
afterEach(() => { cleanup(); clients.forEach(c => c.dispose()); clients.length = 0; });
function setup() {
  const listeners = new Set<(e: {data: unknown}) => void>();
  let state: ReceiptState = { source: null, version: null, pageNumber: 1, status: 'NotStarted', fields: [] };
  let fail = false;
  const sent: {command:string; payload:any}[] = [];
  const profile = {id:'synthetic-payables',revision:1,displayName:'Synthetic Payables',profileHash:'B'.repeat(64),fields:[
    {key:'vendor',header:'Vendor name',kind:'text',required:true,maxLength:128},
    {key:'amount',header:'Amount',kind:'decimal',required:true,maxLength:32},
    {key:'invoice',header:'Invoice number',kind:'text',required:false,maxLength:64},
  ]};
  const transport: Transport = {
    addEventListener: (_, f) => {listeners.add(f);}, removeEventListener: (_, f) => {listeners.delete(f);},
    postMessage: message => { const m = message as {command:string;payload:any;requestId:string}; sent.push(m);
      if (m.command === 'receipt.select') state.source = {displayName:'artificial.pdf',document:{version:{documentId:'11111111-1111-1111-1111-111111111111',revision:0,sha256:'A'.repeat(64)},kind:0,byteLength:128,status:0}};
      if (m.command === 'receipt.start') {state.status='Active';state.version={reviewId:'22222222-2222-2222-2222-222222222222',revision:0,profileId:profile.id,profileHash:profile.profileHash};state.fields=profile.fields.map(f=>({key:f.key,value:'',valueRevision:0,status:'Draft'}));}
      if (['receipt.correct','receipt.verify','receipt.retract'].includes(m.command) && !fail) {
        const field=state.fields.find(f=>f.key===m.payload.key)!;
        if(m.command==='receipt.correct'){field.value=m.payload.value;field.valueRevision++;field.status='Corrected';}
        else field.status=m.command==='receipt.verify'?'Verified':'Corrected';
        state.version!.revision++;
      }
      if (m.command==='receipt.close') state={source:null,version:null,status:'NotStarted',fields:[],pageNumber:1};
      const result = structuredClone(m.command==='workflow.profiles'?{profiles:[profile]}:state);
      queueMicrotask(()=>listeners.forEach(f=>f({data:{protocolVersion:1,type:'response',requestId:m.requestId,ok:!fail||m.command==='receipt.state',result,error:{code:'SOURCE_INVALIDATED',message:'Source changed'}}})));
    },
  };
  const bridge = new BridgeClient(transport); clients.push(bridge);
  return {bridge,sent,fail:()=>{fail=true;},props:{bridge,destination:null,disabled:false}};
}
async function start() {
  const select = await screen.findByRole('button',{name:'Select receipt PDF or image'});
  await waitFor(()=>expect((select as HTMLButtonElement).disabled).toBe(false)); fireEvent.click(select);
  fireEvent.click(await screen.findByRole('button',{name:'Start manual receipt review'}));
  await screen.findByLabelText('Receipt vendor');
}
test('manual edits require separate save and verification; correction and retraction remove verified state',async()=>{
  const f=setup();render(<ReceiptReview {...f.props}/>);await start();
  expect(screen.getByText(/Offline OCR and source rendering are unavailable/)).toBeTruthy();
  expect(screen.queryByText('Human-verified manual entry')).toBeNull();
  fireEvent.change(screen.getByLabelText('Receipt vendor'),{target:{value:'=literal text'}});
  fireEvent.change(screen.getByLabelText('Receipt amount'),{target:{value:'125.50'}});
  expect((screen.getByRole('button',{name:'Verify Vendor name'}) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button',{name:'Save Vendor name'}));
  await waitFor(()=>expect((screen.getByRole('button',{name:'Verify Vendor name'}) as HTMLButtonElement).disabled).toBe(false));
  expect((screen.getByLabelText('Receipt amount') as HTMLInputElement).value).toBe('125.50');
  expect(f.sent.filter(s=>s.command==='receipt.verify')).toHaveLength(0);
  fireEvent.click(screen.getByRole('button',{name:'Verify Vendor name'}));
  await screen.findByText('Human-verified manual entry');
  fireEvent.change(screen.getByLabelText('Receipt vendor'),{target:{value:'Corrected entity'}});
  expect(screen.getAllByText('Unsaved edit — not verified')).toHaveLength(2);
  fireEvent.click(screen.getByRole('button',{name:'Save Vendor name'}));
  await waitFor(()=>expect(screen.queryByText('Human-verified manual entry')).toBeNull());
  fireEvent.click(screen.getByRole('button',{name:'Verify Vendor name'}));
  const retract=await screen.findByRole('button',{name:'Retract Vendor name'});fireEvent.click(retract);
  await waitFor(()=>expect(screen.queryByText('Human-verified manual entry')).toBeNull());
  expect(f.sent.find(s=>s.command==='receipt.correct')?.payload.value).toBe('=literal text');
});
test('host failure blocks verification until an authoritative state refresh',async()=>{
  const f=setup();render(<ReceiptReview {...f.props}/>);await start();f.fail();
  fireEvent.change(screen.getByLabelText('Receipt amount'),{target:{value:'125.50'}});
  fireEvent.click(screen.getByRole('button',{name:'Save Amount'}));
  await screen.findByRole('alert');expect((screen.getByLabelText('Receipt amount') as HTMLInputElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button',{name:'Refresh receipt state'}));
  await waitFor(()=>expect((screen.getByLabelText('Receipt amount') as HTMLInputElement).disabled).toBe(false));
  expect((screen.getByLabelText('Receipt amount') as HTMLInputElement).value).toBe('');
});
test('wire validators reject fabricated verification states, file paths and write-enabled previews',()=>{
  expect(validReceiptState({source:null,version:null,pageNumber:1,status:'NotStarted',fields:[]})).toBe(true);
  expect(validReceiptState({source:null,version:null,pageNumber:1,status:'Verified',fields:[]})).toBe(false);
  expect(validReceiptState({source:{displayName:'C:\\private.pdf'},version:null,pageNumber:1,status:'NotStarted',fields:[]})).toBe(false);
  expect(validReceiptPreview({canApply:true})).toBe(false);
});
test('receipt workflow steps retain unsaved fields and keep placement in the same review',async()=>{
  const f=setup();render(<ReceiptReview {...f.props} view="workflow"/>);await start();
  fireEvent.change(screen.getByLabelText('Receipt amount'),{target:{value:'125.50'}});
  fireEvent.click(screen.getByRole('button',{name:'Source'}));
  fireEvent.click(screen.getByRole('button',{name:'Destination'}));
  expect(screen.getByText('Validated destination unavailable')).toBeDefined();
  fireEvent.click(screen.getByRole('button',{name:'Review'}));
  expect((screen.getByRole('button',{name:'Preview verified receipt placement'}) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button',{name:'Fields'}));
  expect((screen.getByLabelText('Receipt amount') as HTMLInputElement).value).toBe('125.50');
});
test('an optional field cleared after entry can be explicitly verified as absent',async()=>{
  const f=setup();render(<ReceiptReview {...f.props}/>);await start();
  const input=screen.getByLabelText('Receipt invoice');
  fireEvent.change(input,{target:{value:'ARTIFICIAL-1'}});
  fireEvent.click(screen.getByRole('button',{name:'Save Invoice number'}));
  await waitFor(()=>expect((screen.getByRole('button',{name:'Verify Invoice number'}) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.change(input,{target:{value:''}});
  fireEvent.click(screen.getByRole('button',{name:'Save Invoice number'}));
  await waitFor(()=>expect((screen.getByRole('button',{name:'Verify Invoice number'}) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button',{name:'Verify Invoice number'}));
  await screen.findByRole('button',{name:'Retract Invoice number'});
  expect(f.sent.filter(s=>s.command==='receipt.verify').at(-1)?.payload.key).toBe('invoice');
});
