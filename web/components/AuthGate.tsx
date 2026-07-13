'use client';

import { ReactNode, useEffect } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { useAuth } from '@/components/AuthProvider';

interface AuthGateProps {
  children: ReactNode;
}

export function AuthGate({ children }: AuthGateProps) {
  const { isAuthenticated, isLoading } = useAuth();
  const pathname = usePathname();
  const router = useRouter();
  const isLoginPage = pathname === '/login';

  useEffect(() => {
    if (isLoading) return;

    if (!isAuthenticated && !isLoginPage) {
      router.replace('/login');
      return;
    }

    if (isAuthenticated && isLoginPage) {
      router.replace('/');
    }
  }, [isAuthenticated, isLoading, isLoginPage, router]);

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-muted border-t-foreground" />
      </div>
    );
  }

  if (!isAuthenticated && !isLoginPage) {
    return null;
  }

  if (isAuthenticated && isLoginPage) {
    return null;
  }

  return <>{children}</>;
}
