'use client';

import { NextIntlClientProvider } from 'next-intl';
import { ThemeProvider } from '@/components/ThemeProvider';
import { LocaleProvider } from '@/components/LocaleProvider';
import { Header } from '@/components/Header';
import { ApiStartupGate } from '@/components/ApiStartupGate';
import { AuthProvider } from '@/components/AuthProvider';
import { AuthGate } from '@/components/AuthGate';
import { defaultLocale } from '@/i18n';
import { useState, useEffect } from 'react';
import { usePathname } from 'next/navigation';
import './globals.css';

function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const isLoginPage = pathname === '/login';

  if (isLoginPage) {
    return <>{children}</>;
  }

  return (
    <div className="min-h-screen bg-background text-foreground">
      <Header />
      <main className="container mx-auto px-4 py-8">
        {children}
      </main>
    </div>
  );
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [messages, setMessages] = useState<any>(null);
  const [locale, setLocaleState] = useState(defaultLocale);

  useEffect(() => {
    // Carregar mensagens baseado no locale salvo
    const savedLocale = (localStorage.getItem('locale') as typeof defaultLocale) || defaultLocale;
    const validLocale = savedLocale === 'pt-PT' || savedLocale === 'en' ? savedLocale : defaultLocale;
    setLocaleState(validLocale);
    
    // Carregar mensagens dinamicamente
    import(`../messages/${validLocale}.json`).then((mod) => {
      setMessages(mod.default);
    });
  }, []);

  if (!messages) {
    return (
      <html lang={defaultLocale} suppressHydrationWarning>
        <body>
          <div className="flex items-center justify-center min-h-screen">
            <p>Carregando...</p>
          </div>
        </body>
      </html>
    );
  }

  return (
    <html lang={locale} suppressHydrationWarning>
      <body>
        <script
          dangerouslySetInnerHTML={{
            __html: `
              (function() {
                try {
                  const theme = localStorage.getItem('theme') || 
                    (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
                  if (theme === 'dark') {
                    document.documentElement.classList.add('dark');
                  }
                  const locale = localStorage.getItem('locale') || 'pt-PT';
                  document.documentElement.lang = locale;
                } catch (e) {}
              })();
            `,
          }}
        />
        <NextIntlClientProvider locale={locale} messages={messages}>
          <LocaleProvider>
            <ThemeProvider>
              <AuthProvider>
                <ApiStartupGate>
                  <AuthGate>
                    <AppShell>{children}</AppShell>
                  </AuthGate>
                </ApiStartupGate>
              </AuthProvider>
            </ThemeProvider>
          </LocaleProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
