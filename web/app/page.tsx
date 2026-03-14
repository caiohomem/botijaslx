'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { loadAppSettings } from '@/lib/settings';

export default function HomePage() {
  const t = useTranslations();
  const [debugEnabled, setDebugEnabled] = useState(false);

  useEffect(() => {
    loadAppSettings().then((settings) => {
      setDebugEnabled(settings.debugEnabled);
    });
  }, []);

  return (
    <div className="space-y-8">
      <h1 className="text-3xl font-bold">{t('common.appName')}</h1>
      
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Link
          href="/delivery"
          className="p-6 border rounded-lg hover:bg-accent transition-colors"
        >
          <h2 className="text-xl font-semibold mb-2">
            {t('navigation.delivery')}
          </h2>
          <p className="text-muted-foreground">
            Receber botijas do cliente
          </p>
        </Link>

        <Link
          href="/filling"
          className="p-6 border rounded-lg hover:bg-accent transition-colors"
        >
          <h2 className="text-xl font-semibold mb-2">
            {t('navigation.filling')}
          </h2>
          <p className="text-muted-foreground">
            Marcar botijas como cheias
          </p>
        </Link>

        <Link
          href="/pickup"
          className="p-6 border rounded-lg hover:bg-accent transition-colors"
        >
          <h2 className="text-xl font-semibold mb-2">
            {t('navigation.pickup')}
          </h2>
          <p className="text-muted-foreground">
            Entregar botijas ao cliente
          </p>
        </Link>

        <Link
          href="/dashboard"
          className="p-6 border rounded-lg hover:bg-accent transition-colors"
        >
          <h2 className="text-xl font-semibold mb-2">
            {t('navigation.dashboard')}
          </h2>
          <p className="text-muted-foreground">
            Ver estatísticas e pendências
          </p>
        </Link>

        <Link
          href="/clientes"
          className="p-6 border rounded-lg hover:bg-accent transition-colors"
        >
          <h2 className="text-xl font-semibold mb-2">
            {t('navigation.customers')}
          </h2>
          <p className="text-muted-foreground">
            Gerir clientes e botijas
          </p>
        </Link>

        {debugEnabled ? (
          <Link
            href="/debug"
            className="p-6 border rounded-lg hover:bg-accent transition-colors"
          >
            <h2 className="text-xl font-semibold mb-2">
              {t('navigation.debug')}
            </h2>
            <p className="text-muted-foreground">
              Ver registos por cliente e apagar linhas
            </p>
          </Link>
        ) : null}
      </div>
    </div>
  );
}
