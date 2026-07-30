const AUTH_KEY = 'botijas-auth';
const BUSINESS_KEY = 'botijas-business';

const AUTH_USER = process.env.NEXT_PUBLIC_AUTH_USER ?? 'oficina';
const AUTH_PASS = process.env.NEXT_PUBLIC_AUTH_PASS ?? 'oficina';
const BUSINESS_PASS = process.env.NEXT_PUBLIC_BUSINESS_PASS ?? 'negocio';

export function isAuthenticated(): boolean {
  if (typeof window === 'undefined') return false;
  return localStorage.getItem(AUTH_KEY) === '1';
}

export function login(username: string, password: string): boolean {
  if (username === AUTH_USER && password === AUTH_PASS) {
    localStorage.setItem(AUTH_KEY, '1');
    return true;
  }
  return false;
}

export function logout(): void {
  localStorage.removeItem(AUTH_KEY);
  if (typeof window !== 'undefined') {
    sessionStorage.removeItem(BUSINESS_KEY);
  }
}

export function isBusinessUnlocked(): boolean {
  if (typeof window === 'undefined') return false;
  return sessionStorage.getItem(BUSINESS_KEY) === '1';
}

export function unlockBusiness(password: string): boolean {
  if (password === BUSINESS_PASS) {
    sessionStorage.setItem(BUSINESS_KEY, '1');
    return true;
  }
  return false;
}

export function lockBusiness(): void {
  sessionStorage.removeItem(BUSINESS_KEY);
}
