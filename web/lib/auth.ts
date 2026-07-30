const AUTH_KEY = 'botijas-auth';
const ADMIN_KEY = 'botijas-admin';

const AUTH_USER = process.env.NEXT_PUBLIC_AUTH_USER ?? 'oficina';
const AUTH_PASS = process.env.NEXT_PUBLIC_AUTH_PASS ?? 'oficina';
const ADMIN_USER = process.env.NEXT_PUBLIC_ADMIN_USER ?? 'admin';
const ADMIN_PASS = process.env.NEXT_PUBLIC_ADMIN_PASS ?? 'negocio';

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
  logoutAdmin();
}

export function isAdminAuthenticated(): boolean {
  if (typeof window === 'undefined') return false;
  return sessionStorage.getItem(ADMIN_KEY) === '1';
}

export function loginAdmin(username: string, password: string): boolean {
  if (username === ADMIN_USER && password === ADMIN_PASS) {
    sessionStorage.setItem(ADMIN_KEY, '1');
    return true;
  }
  return false;
}

export function logoutAdmin(): void {
  if (typeof window !== 'undefined') {
    sessionStorage.removeItem(ADMIN_KEY);
  }
}
