'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { LanguageSelector } from './LanguageSelector';
import { ThemeToggle } from './ThemeToggle';
import { SETTINGS_UPDATED_EVENT, loadAppSettings } from '@/lib/settings';
import { useAuth } from '@/components/AuthProvider';

const navItems = [
  { href: '/delivery', key: 'delivery' },
  { href: '/filling', key: 'filling' },
  { href: '/pickup', key: 'pickup' },
  { href: '/dashboard', key: 'dashboard' },
  { href: '/business', key: 'business' },
  { href: '/clientes', key: 'customers' },
  { href: '/debug', key: 'debug' },
];

export function Header() {
  const t = useTranslations();
  const { logout, isAdmin } = useAuth();
  const pathname = usePathname();
  const [appTitle, setAppTitle] = useState('');
  const [debugEnabled, setDebugEnabled] = useState(false);

  useEffect(() => {
    loadAppSettings().then((settings) => {
      if (settings.appTitle) {
        setAppTitle(settings.appTitle);
      }
      setDebugEnabled(settings.debugEnabled);
    });
  }, []);

  useEffect(() => {
    const handleSettingsUpdated = (event: Event) => {
      const customEvent = event as CustomEvent<{ appTitle?: string; debugEnabled?: boolean }>;
      setAppTitle(customEvent.detail?.appTitle || '');
      setDebugEnabled(Boolean(customEvent.detail?.debugEnabled));
    };

    window.addEventListener(SETTINGS_UPDATED_EVENT, handleSettingsUpdated);
    return () => window.removeEventListener(SETTINGS_UPDATED_EVENT, handleSettingsUpdated);
  }, []);

  return (
    <header className="border-b">
      <div className="container mx-auto px-4 py-3">
        <div className="flex justify-between items-center">
          <Link href="/" className="text-xl font-bold">{appTitle || t('common.appName')}</Link>
          <div className="flex gap-3 items-center">
            <Link 
              href="/settings" 
              className={`text-sm hover:text-primary ${pathname === '/settings' ? 'text-primary' : 'text-muted-foreground'}`}
            >
              ⚙️
            </Link>
            <button
              onClick={() => {
                logout();
                window.location.href = '/login';
              }}
              className="text-sm text-muted-foreground hover:text-foreground"
              title={t('auth.logout')}
            >
              {t('auth.logout')}
            </button>
            <LanguageSelector />
            <ThemeToggle />
          </div>
        </div>
        <nav className="flex gap-1 mt-3 -mb-3 overflow-x-auto">
          {navItems
            .filter((item) => {
              if (item.key === 'debug') return debugEnabled;
              if (item.key === 'business') return isAdmin;
              return true;
            })
            .map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={`px-4 py-2 text-sm rounded-t-lg transition-colors ${
                pathname === item.href
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:text-foreground hover:bg-muted'
              }`}
            >
              {t(`navigation.${item.key}`)}
            </Link>
          ))}
        </nav>
      </div>
    </header>
  );
}
