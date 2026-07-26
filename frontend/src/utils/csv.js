function escapeCsvField(value) {
  const str = value == null ? '' : String(value);
  if (/[",\n]/.test(str)) {
    return `"${str.replace(/"/g, '""')}"`;
  }
  return str;
}

// columns: [{ header: 'Price', get: (row) => row.property.price }]
export function toCsv(rows, columns) {
  const headerLine = columns.map((c) => escapeCsvField(c.header)).join(',');
  const lines = rows.map((row) =>
    columns.map((c) => escapeCsvField(c.get(row))).join(',')
  );
  return [headerLine, ...lines].join('\r\n');
}

export function downloadCsv(filename, csvContent) {
  // Leading BOM so Excel opens UTF-8 (e.g. accented Portuguese text) correctly.
  const blob = new Blob(['﻿' + csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
