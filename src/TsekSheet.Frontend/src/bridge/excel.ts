import type { ReviewVersion } from './receipts';
export interface ExcelWorkbook { id:string; name:string; sheets:string[] }
export interface ExcelSection { id:string; label:string; sheetName:string }
export interface FormulaWarning { cell:string; reason:string }
export interface ExcelChange { workbook:string; sheet:string; range:string; manualReview:FormulaWarning[] }
export interface ExcelCatalog { openedWorkbookId?:string|null; workbooks:ExcelWorkbook[]; sections:ExcelSection[]; changes:ExcelChange[] }
export interface ExcelPreview { token:string; workbookId:string; sectionId:string; sheet:string; range:string; insertRow:number; values:{key:string;header:string;column:number;value:string|number|boolean|null;displayValue?:string|null}[]; manualReview:FormulaWarning[] }
export type ExcelPreviewRequest = { expected:ReviewVersion; workbookId:string; sectionId:string; sheet?:string };
const object = (v:unknown): v is Record<string,unknown> => !!v && typeof v==='object' && !Array.isArray(v);
const text = (v:unknown):v is string => typeof v==='string' && v.length<=1024;
const id = (v:unknown) => typeof v==='string' && /^[a-f0-9]{32}$/i.test(v);
const range = (v:unknown) => typeof v==='string' && /^[A-Z]{1,3}[1-9][0-9]*:[A-Z]{1,3}[1-9][0-9]*$/.test(v);
const warnings = (v:unknown) => Array.isArray(v) && v.length<=16384 && v.every(w=>object(w)&&range(w.cell)&&text(w.reason));
export function validExcelChange(v:unknown):boolean { return object(v)&&text(v.workbook)&&!/[\\/]/.test(v.workbook)&&text(v.sheet)&&range(v.range)&&warnings(v.manualReview); }
export function validExcelCatalog(v:unknown):boolean { return object(v)&&(v.openedWorkbookId==null||(id(v.openedWorkbookId)&&Array.isArray(v.workbooks)&&v.workbooks.some(w=>object(w)&&w.id===v.openedWorkbookId)))&&Array.isArray(v.workbooks)&&v.workbooks.length<=256&&v.workbooks.every(w=>object(w)&&id(w.id)&&text(w.name)&&!/[\\/]/.test(w.name)&&Array.isArray(w.sheets)&&w.sheets.every(text))&&Array.isArray(v.sections)&&v.sections.length<=16&&v.sections.every(s=>object(s)&&text(s.id)&&text(s.label)&&text(s.sheetName))&&Array.isArray(v.changes)&&v.changes.length<=128&&v.changes.every(validExcelChange); }
export function validExcelPreview(v:unknown):boolean { return object(v)&&id(v.token)&&id(v.workbookId)&&text(v.sectionId)&&text(v.sheet)&&range(v.range)&&Number.isInteger(v.insertRow)&&Number(v.insertRow)>=1&&Number(v.insertRow)<=1048576&&Array.isArray(v.values)&&v.values.length<=16&&v.values.every(f=>object(f)&&text(f.key)&&text(f.header)&&(f.displayValue==null||text(f.displayValue))&&Number.isInteger(f.column)&&Number(f.column)>=1&&Number(f.column)<=16384&&(f.value===null||typeof f.value==='boolean'||text(f.value)||(typeof f.value==='number'&&Number.isFinite(f.value))))&&warnings(v.manualReview); }
