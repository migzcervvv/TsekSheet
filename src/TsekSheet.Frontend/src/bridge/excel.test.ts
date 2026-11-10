import { expect, it } from 'vitest';
import { validExcelCatalog, validExcelPreview } from './excel';

it('accepts all twenty-two flat Payables destinations within the bounded catalog',()=>{
  const catalog={workbooks:[],changes:[],sections:Array.from({length:22},(_,i)=>({id:`section-${i}`,label:i<8?`Main ${i+1}`:`Member Company ${i-7}`,sheetName:''}))};
  expect(validExcelCatalog(catalog)).toBe(true);
  expect(validExcelCatalog({...catalog,sections:Array.from({length:33},()=>catalog.sections[0])})).toBe(false);
});

it('validates advisory warnings separately from blocking formula reviews',()=>{
  const preview={token:'a'.repeat(32),workbookId:'b'.repeat(32),sectionId:'employee',sheet:'Entry',range:'B8:O8',insertRow:8,values:[],manualReview:[],advisoryWarnings:['Possible duplicate RFP No.']};
  expect(validExcelPreview(preview)).toBe(true);
  expect(validExcelPreview({...preview,advisoryWarnings:[{reason:'wrong shape'}]})).toBe(false);
  expect(validExcelPreview({...preview,advisoryWarnings:Array(257).fill('warning')})).toBe(false);
});
