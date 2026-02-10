'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { useTranslations } from 'next-intl';
import { customersApi } from '@/lib/api';

interface Customer {
  customerId: string;
  name: string;
  phone: string;
}

interface CustomerSearchProps {
  onSelect: (customer: Customer) => void;
  onCreateNew?: () => void;
  disabled?: boolean;
}

export function CustomerSearch({ onSelect, onCreateNew, disabled = false }: CustomerSearchProps) {
  const t = useTranslations();
  const [query, setQuery] = useState('');
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>();

  const search = useCallback(async (q: string) => {
    if (q.trim().length < 2) {
      setCustomers([]);
      setOpen(false);
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const result = await customersApi.search(q);
      setCustomers(result.customers.slice(0, 5));
      setOpen(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao buscar clientes');
      setCustomers([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (query.trim().length < 2) {
      setCustomers([]);
      setOpen(false);
      return;
    }
    debounceRef.current = setTimeout(() => search(query), 300);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [query, search]);

  // Close dropdown on outside click
  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, []);

  const handleSelect = (customer: Customer) => {
    setOpen(false);
    setQuery('');
    onSelect(customer);
  };

  const highlightMatch = (text: string) => {
    const q = query.trim().toLowerCase();
    if (!q) return text;
    const idx = text.toLowerCase().indexOf(q);
    if (idx === -1) return text;
    return (
      <>
        {text.slice(0, idx)}
        <span className="font-bold text-primary">{text.slice(idx, idx + q.length)}</span>
        {text.slice(idx + q.length)}
      </>
    );
  };

  return (
    <div ref={containerRef} className="relative">
      <div className="relative">
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={t('customer.search')}
          className="w-full px-4 py-3 border rounded-lg bg-background text-foreground text-lg pr-10"
          disabled={disabled}
          autoFocus
        />
        {loading && (
          <div className="absolute right-3 top-1/2 -translate-y-1/2">
            <div className="w-5 h-5 border-2 border-primary/30 border-t-primary rounded-full animate-spin" />
          </div>
        )}
      </div>

      {error && (
        <div className="mt-2 p-2 bg-destructive/10 text-destructive rounded-lg text-sm">
          {error}
        </div>
      )}

      {/* Dropdown results */}
      {open && (
        <div className="absolute z-50 w-full mt-1 bg-background border rounded-lg shadow-lg overflow-hidden">
          {customers.length > 0 ? (
            <>
              {customers.map((customer) => (
                <button
                  key={customer.customerId}
                  onClick={() => handleSelect(customer)}
                  disabled={disabled}
                  className="w-full px-4 py-3 text-left hover:bg-accent transition-colors flex items-center justify-between border-b last:border-b-0"
                >
                  <div>
                    <div className="font-semibold">{highlightMatch(customer.name)}</div>
                    <div className="text-sm text-muted-foreground">{highlightMatch(customer.phone)}</div>
                  </div>
                </button>
              ))}
            </>
          ) : (
            <div className="px-4 py-3 text-sm text-muted-foreground">
              {t('customer.notFound')}
            </div>
          )}

          {/* Create new customer link */}
          {onCreateNew && (
            <button
              onClick={() => { setOpen(false); onCreateNew(); }}
              className="w-full px-4 py-3 text-left text-sm text-primary hover:bg-accent transition-colors border-t font-medium"
            >
              + {t('customer.create')}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
