'use client';

import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import {
  AuthRole,
  getAuthRole,
  isAuthenticated as checkAuth,
  isAdminAuthenticated as checkAdmin,
  login as doLogin,
  logout as doLogout,
} from '@/lib/auth';

interface AuthContextType {
  isAuthenticated: boolean;
  isAdmin: boolean;
  role: AuthRole | null;
  isLoading: boolean;
  login: (user: string, pass: string) => boolean;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isAdmin, setIsAdmin] = useState(false);
  const [role, setRole] = useState<AuthRole | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const authenticated = checkAuth();
    setIsAuthenticated(authenticated);
    setIsAdmin(checkAdmin());
    setRole(getAuthRole());
    setIsLoading(false);
  }, []);

  const login = (user: string, pass: string) => {
    const ok = doLogin(user, pass);
    if (ok) {
      setIsAuthenticated(true);
      setIsAdmin(checkAdmin());
      setRole(getAuthRole());
    }
    return ok;
  };

  const logout = () => {
    doLogout();
    setIsAuthenticated(false);
    setIsAdmin(false);
    setRole(null);
  };

  return (
    <AuthContext.Provider value={{ isAuthenticated, isAdmin, role, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
