export function parseCell(text: string): {row:number;column:number} | null {
  const m = /^\$?([A-Z]{1,3})\$?([1-9][0-9]{0,6})$/i.exec(text.trim());
  if (!m) return null;
  const column = [...m[1].toUpperCase()].reduce((n,c)=>n*26+c.charCodeAt(0)-64,0), row = Number(m[2]);
  return column <= 16384 && row <= 1048576 ? {row,column} : null;
}
