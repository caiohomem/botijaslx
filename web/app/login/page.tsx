'use client';

import { FormEvent, useState } from 'react';
import { useTranslations } from 'next-intl';
import { useAuth } from '@/components/AuthProvider';

export default function LoginPage() {
  const t = useTranslations('auth');
  const { login } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(false);

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    setError(false);

    if (!login(username, password)) {
      setError(true);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <form
        onSubmit={handleSubmit}
        className="w-full max-w-sm space-y-4 rounded-lg border p-6 shadow-sm"
      >
        <h1 className="text-2xl font-bold text-center">{t('title')}</h1>

        {error && (
          <p className="text-sm text-red-600 dark:text-red-400 text-center">
            {t('invalidCredentials')}
          </p>
        )}

        <div className="space-y-2">
          <label htmlFor="username" className="text-sm font-medium">
            {t('username')}
          </label>
          <input
            id="username"
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            className="w-full px-3 py-2 border rounded-lg bg-background"
            required
          />
        </div>

        <div className="space-y-2">
          <label htmlFor="password" className="text-sm font-medium">
            {t('password')}
          </label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            className="w-full px-3 py-2 border rounded-lg bg-background"
            required
          />
        </div>

        <button
          type="submit"
          className="w-full px-4 py-2 bg-primary text-primary-foreground rounded-lg font-medium hover:opacity-90 transition-opacity"
        >
          {t('login')}
        </button>
      </form>
    </div>
  );
}
