'use client';

import { useState, useEffect, useRef, useCallback } from 'react';

interface QrScannerProps {
  onScan: (result: string) => void;
  label?: string;
}

export function QrScanner({ onScan, label }: QrScannerProps) {
  const [scanning, setScanning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scannerRef = useRef<any>(null);
  const containerId = useRef(`qr-reader-${Math.random().toString(36).slice(2)}`);
  const onScanRef = useRef(onScan);
  onScanRef.current = onScan;

  const stopScanner = useCallback(async () => {
    if (scannerRef.current) {
      try {
        await scannerRef.current.stop();
      } catch {
        // already stopped
      }
      try {
        scannerRef.current.clear();
      } catch {
        // ignore
      }
      scannerRef.current = null;
    }
    setScanning(false);
  }, []);

  // Start scanner via useEffect so the DOM element exists
  useEffect(() => {
    if (!scanning) return;

    let cancelled = false;

    const init = async () => {
      setError(null);

      try {
        // Dynamic import to avoid SSR issues
        const { Html5Qrcode } = await import('html5-qrcode');

        if (cancelled) return;

        const el = document.getElementById(containerId.current);
        if (!el) {
          setError('Elemento de camera nao encontrado');
          setScanning(false);
          return;
        }

        const scanner = new Html5Qrcode(containerId.current);
        scannerRef.current = scanner;

        await scanner.start(
          { facingMode: 'environment' },
          {
            fps: 10,
            qrbox: { width: 250, height: 250 },
          },
          (decodedText: string) => {
            onScanRef.current(decodedText);
            // Stop after successful scan
            scanner.stop().catch(() => {});
            try { scanner.clear(); } catch {}
            scannerRef.current = null;
            setScanning(false);
          },
          () => {
            // no QR found in frame - ignore
          }
        );
      } catch (err: any) {
        if (cancelled) return;
        console.error('QR Scanner error:', err);
        const msg = typeof err === 'string' ? err : err?.message || '';
        if (msg.includes('NotAllowedError') || msg.includes('Permission')) {
          setError('Permissao de camera negada. Verifique as configuracoes do navegador.');
        } else if (msg.includes('NotFoundError') || msg.includes('Requested device not found')) {
          setError('Camera nao encontrada neste dispositivo.');
        } else if (msg.includes('NotReadableError') || msg.includes('Could not start')) {
          setError('Camera em uso por outra aplicacao.');
        } else if (msg.includes('insecure') || msg.includes('secure context')) {
          setError('Camera requer HTTPS. Use localhost ou um certificado SSL.');
        } else {
          setError(msg || 'Erro ao aceder a camera');
        }
        scannerRef.current = null;
        setScanning(false);
      }
    };

    // Small delay to ensure DOM is painted
    const timer = setTimeout(init, 100);

    return () => {
      cancelled = true;
      clearTimeout(timer);
      if (scannerRef.current) {
        scannerRef.current.stop().catch(() => {});
        try { scannerRef.current.clear(); } catch {}
        scannerRef.current = null;
      }
    };
  }, [scanning]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (scannerRef.current) {
        scannerRef.current.stop().catch(() => {});
        try { scannerRef.current.clear(); } catch {}
        scannerRef.current = null;
      }
    };
  }, []);

  return (
    <div className="space-y-2">
      {!scanning ? (
        <button
          type="button"
          onClick={() => setScanning(true)}
          className="px-4 py-3 border-2 border-dashed border-primary/50 rounded-lg hover:bg-primary/5 transition-colors w-full flex items-center justify-center gap-2 text-primary font-medium"
        >
          <span className="text-xl">📷</span>
          {label || 'Escanear QR Code'}
        </button>
      ) : (
        <div className="relative border-2 border-primary rounded-lg overflow-hidden">
          <div id={containerId.current} className="w-full" style={{ minHeight: 300 }} />
          <button
            type="button"
            onClick={stopScanner}
            className="absolute top-2 right-2 z-10 bg-black/60 text-white px-3 py-1 rounded-full text-sm hover:bg-black/80"
          >
            ✕
          </button>
        </div>
      )}
      {error && (
        <div className="text-sm text-destructive bg-destructive/10 p-2 rounded">
          {error}
        </div>
      )}
    </div>
  );
}
