import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, test, vi } from 'vitest';
import SourcePreview from './SourcePreview';
import type { BridgeClient } from './bridge/client';
import { validSourceRender } from './bridge/receipts';

afterEach(()=>{cleanup();vi.unstubAllGlobals();});
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
