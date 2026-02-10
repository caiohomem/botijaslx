'use client';

import { useEffect, useRef, useCallback } from 'react';
import QRCode from 'qrcode';

// 12 px/mm = ~300 DPI for sharp thermal print
const SCALE = 12;

function getLabelSettings() {
  try {
    const s = localStorage.getItem('botijas_settings');
    if (s) {
      const parsed = JSON.parse(s);
      return {
        widthMm: parsed.labelWidthMm || 50,
        heightMm: parsed.labelHeightMm || 75,
        printerType: (parsed.printerType || 'label') as 'label' | 'a4',
        storeLink: parsed.storeLink || '',
      };
    }
  } catch {}
  return { widthMm: 50, heightMm: 75, printerType: 'label' as const, storeLink: '' };
}

// Format phone number with spaces every 3 digits (e.g., 926 060 863)
function formatPhoneNumber(phone: string): string {
  const digits = phone.replace(/\D/g, '');
  const parts = [];
  for (let i = 0; i < digits.length; i += 3) {
    parts.push(digits.substr(i, 3));
  }
  return parts.join(' ');
}

interface LabelPreviewProps {
  qrContent: string;
  customerName?: string;
  customerPhone?: string;
  sequentialNumber?: number;
  storeName?: string;
  storeLink?: string;
}

export function LabelPreview({
  qrContent,
  customerName,
  customerPhone,
  sequentialNumber,
  storeName,
  storeLink,
}: LabelPreviewProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const { widthMm, heightMm, storeLink: configStoreLink } = getLabelSettings();
  const W = widthMm * SCALE;
  const H = heightMm * SCALE;
  const displayStoreLink = storeLink || configStoreLink;

  const renderLabel = useCallback(async () => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    canvas.width = W;
    canvas.height = H;

    // White background
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, W, H);

    // Light border (cut guide)
    ctx.strokeStyle = '#cccccc';
    ctx.lineWidth = 1;
    ctx.strokeRect(0.5, 0.5, W - 1, H - 1);

    // Proportional spacing based on label height
    const padSide = Math.round(widthMm * 0.08 * SCALE); // 8% side padding
    const usableW = W - padSide * 2;

    // Zone heights (proportional to label height)
    // Top: store name + link = 16% of height
    // QR: 52% of height
    // Bottom: text = 32% of height
    const topZone = Math.round(H * 0.16);
    const qrZone = Math.round(H * 0.52);
    const bottomZone = H - topZone - qrZone;

    // --- TOP: Store name and link ---
    if (storeName) {
      const nameSize = Math.round(widthMm * 0.07 * SCALE);
      const linkSize = Math.round(widthMm * 0.045 * SCALE);
      const topPadding = Math.round(topZone * 0.15);

      ctx.fillStyle = '#000000';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';

      // Store name
      ctx.font = `bold ${nameSize}px sans-serif`;
      ctx.fillText(storeName, W / 2, topPadding + nameSize / 2, usableW);

      // Store link (if available)
      if (displayStoreLink) {
        ctx.font = `${linkSize}px sans-serif`;
        ctx.fillText(displayStoreLink, W / 2, topPadding + nameSize + linkSize, usableW);
      }
    }

    // Separator
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = Math.max(1, Math.round(SCALE * 0.15));
    ctx.beginPath();
    ctx.moveTo(padSide, topZone);
    ctx.lineTo(W - padSide, topZone);
    ctx.stroke();

    // --- MIDDLE: QR Code ---
    const qrPad = Math.round(qrZone * 0.08);
    const qrSize = Math.min(usableW, qrZone - qrPad * 2);
    const qrX = (W - qrSize) / 2;
    const qrY = topZone + (qrZone - qrSize) / 2;

    try {
      const qrDataUrl = await QRCode.toDataURL(qrContent, {
        width: qrSize,
        margin: 1,
        errorCorrectionLevel: 'M',
        color: { dark: '#000000', light: '#ffffff' },
      });

      await new Promise<void>((resolve) => {
        const img = new Image();
        img.onload = () => {
          ctx.drawImage(img, qrX, qrY, qrSize, qrSize);
          resolve();
        };
        img.onerror = () => resolve();
        img.src = qrDataUrl;
      });
    } catch {
      ctx.strokeStyle = '#999';
      ctx.lineWidth = 2;
      ctx.strokeRect(qrX, qrY, qrSize, qrSize);
      ctx.fillStyle = '#999';
      ctx.font = `${Math.round(3 * SCALE)}px sans-serif`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText('QR', W / 2, qrY + qrSize / 2);
    }

    // Separator
    const sep2Y = topZone + qrZone;
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = Math.max(1, Math.round(SCALE * 0.15));
    ctx.beginPath();
    ctx.moveTo(padSide, sep2Y);
    ctx.lineTo(W - padSide, sep2Y);
    ctx.stroke();

    // --- BOTTOM: Name, Phone, Seq ---
    ctx.fillStyle = '#000000';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';

    // Divide bottom zone into slots
    const hasName = !!customerName;
    const hasPhone = !!customerPhone;
    const hasSeq = sequentialNumber !== undefined;
    const lines = [hasName, hasPhone, hasSeq].filter(Boolean).length;

    if (lines > 0) {
      const lineH = bottomZone / (lines + 1);
      let slot = 1;

      if (hasName) {
        const fontSize = Math.round(widthMm * 0.07 * SCALE);
        ctx.font = `bold ${fontSize}px sans-serif`;
        ctx.fillText(customerName!, W / 2, sep2Y + lineH * slot, usableW);
        slot++;
      }

      if (hasPhone) {
        const fontSize = Math.round(widthMm * 0.06 * SCALE);
        ctx.font = `${fontSize}px sans-serif`;
        const formattedPhone = formatPhoneNumber(customerPhone!);
        ctx.fillText(formattedPhone, W / 2, sep2Y + lineH * slot, usableW);
        slot++;
      }

      if (hasSeq) {
        const fontSize = Math.round(widthMm * 0.08 * SCALE);
        ctx.font = `bold ${fontSize}px monospace`;
        ctx.fillText(
          `#${String(sequentialNumber).padStart(4, '0')}`,
          W / 2,
          sep2Y + lineH * slot,
          usableW
        );
      }
    }
  }, [qrContent, customerName, customerPhone, sequentialNumber, storeName, displayStoreLink, W, H, widthMm]);

  useEffect(() => {
    renderLabel();
  }, [renderLabel]);

  return (
    <canvas
      ref={canvasRef}
      className="border rounded shadow-sm bg-white"
      style={{
        maxWidth: '100%',
        aspectRatio: `${widthMm} / ${heightMm}`,
        width: Math.min(250, widthMm * 4),
      }}
    />
  );
}

/**
 * Print labels. Reads printer type from settings.
 * - 'label': One label per page at exact label dimensions (for Zebra GK420T etc.)
 * - 'a4': Grid layout on A4 paper with cut guides
 */
export function printLabels(canvases: HTMLCanvasElement[]) {
  const { widthMm, heightMm, printerType } = getLabelSettings();
  const images = canvases.map((c) => c.toDataURL('image/png'));

  if (printerType === 'label') {
    printLabelPrinter(images, widthMm, heightMm);
  } else {
    printA4(images, widthMm, heightMm);
  }
}

function printLabelPrinter(images: string[], wMm: number, hMm: number) {
  const printWindow = window.open('', '_blank');
  if (!printWindow) return;

  const labels = images
    .map((src) => `<div class="label"><img src="${src}" /></div>`)
    .join('\n');

  printWindow.document.write(`<!DOCTYPE html>
<html>
<head>
<title>Etiquetas</title>
<style>
  @page {
    size: ${wMm}mm ${hMm}mm;
    margin: 0;
  }
  * { margin: 0; padding: 0; box-sizing: border-box; }
  html, body { width: ${wMm}mm; background: white; }
  .label {
    width: ${wMm}mm;
    height: ${hMm}mm;
    page-break-after: always;
    overflow: hidden;
  }
  .label:last-child { page-break-after: auto; }
  .label img {
    width: 100%;
    height: 100%;
    display: block;
  }
  @media screen {
    body { background: #f0f0f0; padding: 10px; display: flex; flex-direction: column; align-items: center; gap: 10px; width: auto; }
    .label { border: 1px dashed #999; background: white; }
  }
</style>
</head>
<body>
${labels}
<script>
  window.onload = function() { setTimeout(function() { window.print(); }, 400); };
</script>
</body>
</html>`);

  printWindow.document.close();
}

function printA4(images: string[], wMm: number, hMm: number) {
  const printWindow = window.open('', '_blank');
  if (!printWindow) return;

  // A4 usable = 190x277mm (10mm margins)
  const cols = Math.floor(190 / wMm);
  const rows = Math.floor(277 / hMm);
  const perPage = cols * rows;

  const pages: string[] = [];
  for (let i = 0; i < images.length; i += perPage) {
    const pageImages = images.slice(i, i + perPage);
    let rowsHtml = '';
    for (let r = 0; r < Math.ceil(pageImages.length / cols); r++) {
      const start = r * cols;
      const end = Math.min(start + cols, pageImages.length);
      const rowCells = pageImages
        .slice(start, end)
        .map((src) => `<td class="cell"><img src="${src}" /></td>`)
        .join('');
      const empty = end - start < cols
        ? Array(cols - (end - start)).fill('<td class="cell"></td>').join('')
        : '';
      rowsHtml += `<tr>${rowCells}${empty}</tr>`;
    }
    pages.push(`<div class="page"><table class="grid"><tbody>${rowsHtml}</tbody></table></div>`);
  }

  printWindow.document.write(`<!DOCTYPE html>
<html>
<head>
<title>Etiquetas</title>
<style>
  @page { size: A4 portrait; margin: 10mm; }
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body { background: white; }
  .page { page-break-after: always; display: flex; justify-content: center; }
  .page:last-child { page-break-after: auto; }
  .grid { border-collapse: collapse; }
  .cell { width: ${wMm}mm; height: ${hMm}mm; border: 0.5px dashed #ccc; padding: 0; vertical-align: top; text-align: center; }
  .cell img { width: ${wMm}mm; height: ${hMm}mm; display: block; }
  @media screen {
    body { padding: 20px; background: #f0f0f0; }
    .page { background: white; box-shadow: 0 2px 8px rgba(0,0,0,0.15); padding: 10mm; margin: 0 auto 20px; width: 210mm; min-height: 297mm; }
  }
</style>
</head>
<body>
${pages.join('\n')}
<script>
  window.onload = function() { setTimeout(function() { window.print(); }, 400); };
</script>
</body>
</html>`);

  printWindow.document.close();
}
