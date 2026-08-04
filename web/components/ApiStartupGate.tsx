'use client';

import { ReactNode, useEffect, useState } from 'react';
import { waitForApiReady } from '@/lib/api';

interface ApiStartupGateProps {
  children: ReactNode;
}

export function ApiStartupGate({ children }: ApiStartupGateProps) {
  const [isApiReady, setIsApiReady] = useState(false);
  const [showExtendedMessage, setShowExtendedMessage] = useState(false);

  useEffect(() => {
    let isMounted = true;

    const extendedMessageTimeout = window.setTimeout(() => {
      if (isMounted) {
        setShowExtendedMessage(true);
      }
    }, 3000);

    const pollApi = async () => {
      while (isMounted) {
        try {
          await waitForApiReady();

          if (isMounted) {
            setIsApiReady(true);
          }

          return;
        } catch {
          await new Promise((resolve) => window.setTimeout(resolve, 1500));
        }
      }
    };

    void pollApi();

    return () => {
      isMounted = false;
      window.clearTimeout(extendedMessageTimeout);
    };
  }, []);

  if (isApiReady) {
    return <>{children}</>;
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-6 text-foreground">
      <div className="flex max-w-md flex-col items-center gap-4 text-center">
        <div className="h-10 w-10 animate-spin rounded-full border-4 border-muted border-t-foreground" />
        <div className="space-y-2">
          <h1 className="text-2xl font-semibold">A iniciar servidor...</h1>
          <p className="text-sm text-muted-foreground">
            A aplicação está à espera que a API acorde.
          </p>
          {showExtendedMessage ? (
            <p className="text-sm text-muted-foreground">
              Confirma que a API está em http://localhost:8080/health. Se usares Docker: docker compose logs -f api
            </p>
          ) : null}
        </div>
      </div>
    </div>
  );
}
