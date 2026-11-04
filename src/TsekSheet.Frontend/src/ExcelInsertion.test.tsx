import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ExcelInsertion from './ExcelInsertion';
import type { BridgeClient } from './bridge/client';
import type { ReceiptState } from './bridge/receipts';

describe('verified Excel insertion',()=>{
  afterEach(cleanup);
  const version={reviewId:'11111111-1111-4111-8111-111111111111',revision:4,profileId:'synthetic-payables',profileHash:'A'.repeat(64)};
  const review:ReceiptState={source:null,version,pageNumber:1,status:'Active',fields:[{key:'vendor',value:'=literal',valueRevision:1,status:'Verified'}]};
  function setup(){
    const request=vi.fn((command:string)=>({promise:Promise.resolve(command==='excel.listWorkbooks'?{workbooks:[{id:'a'.repeat(32),name:'Synthetic.xlsx',sheets:['Columns']}],sections:[{id:'columns',label:'Columns section',sheetName:'Columns'}],changes:[]}:command==='excel.previewInsert'?{token:'b'.repeat(32),workbookId:'a'.repeat(32),sectionId:'columns',sheet:'Columns',range:'A5:G5',insertRow:5,values:[{key:'vendor',header:'Vendor name',column:1,value:'=literal'}],manualReview:[{cell:'G6:G6',reason:'Formula left unchanged; review subtotal.'}]}:{workbook:'Synthetic.xlsx',sheet:'Columns',range:'A5:G5',manualReview:[]})}));
    return {request,bridge:{request} as unknown as BridgeClient};
  }
  it('previews verified values, reports subtotal review and lists highlighted inserts',async()=>{
    const {bridge,request}=setup();render(<ExcelInsertion bridge={bridge} review={review} dirty={false} disabled={false}/>);
    fireEvent.click(screen.getByRole('button',{name:'Open workbook in Excel'}));
    await screen.findByRole('option',{name:'Synthetic.xlsx'});
    fireEvent.change(screen.getByLabelText('Excel section'),{target:{value:'columns'}});
    fireEvent.click(screen.getByRole('button',{name:'Preview Excel insert'}));
    await screen.findByText('=literal');
    expect(screen.getByText(/Formula left unchanged/)).toBeTruthy();
    expect(request).toHaveBeenCalledWith('excel.previewInsert',{expected:version,workbookId:'a'.repeat(32),sectionId:'columns'});
    fireEvent.click(screen.getByRole('button',{name:'Insert verified row'}));
    await screen.findByLabelText('Inserted Excel rows');
    expect(request).toHaveBeenCalledWith('excel.insert',{expected:version,previewToken:'b'.repeat(32)});
    expect(screen.getByText(/Synthetic.xlsx · Columns!A5:G5/)).toBeTruthy();
  });
  it('blocks unverified fields and clears previews after unsaved edits',async()=>{
    const {bridge}=setup();const {rerender}=render(<ExcelInsertion bridge={bridge} review={review} dirty={false} disabled={false}/>);
    fireEvent.click(screen.getByRole('button',{name:'Refresh Excel workbooks'}));await screen.findByRole('option',{name:'Synthetic.xlsx'});
    fireEvent.change(screen.getByLabelText('Excel section'),{target:{value:'columns'}});
    rerender(<ExcelInsertion bridge={bridge} review={{...review,fields:[{...review.fields[0],status:'Corrected'}]}} dirty={false} disabled={false}/>);
    expect((screen.getByRole('button',{name:'Preview Excel insert'}) as HTMLButtonElement).disabled).toBe(true);
    rerender(<ExcelInsertion bridge={bridge} review={review} dirty={false} disabled={false}/>);
    fireEvent.click(screen.getByRole('button',{name:'Preview Excel insert'}));await screen.findByLabelText('Excel insertion preview');
    rerender(<ExcelInsertion bridge={bridge} review={review} dirty={true} disabled={false}/>);
    await waitFor(()=>expect(screen.queryByLabelText('Excel insertion preview')).toBeNull());
  });
});
