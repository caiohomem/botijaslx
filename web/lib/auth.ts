const AUTH_KEY = 'botijas-auth';
const ROLE_KEY = 'botijas-role';

const AUTH_USER = process.env.NEXT_PUBLIC_AUTH_USER ?? 'oficina';
const AUTH_PASS = process.env.NEXT_PUBLIC_AUTH_PASS ?? 'oficina';
const ADMIN_USER = process.env.NEXT_PUBLIC_ADMIN_USER ?? 'goncalo';
const ADMIN_PASS = process.env.NEXT_PUBLIC_ADMIN_PASS ?? 'marrocosoffroad';

export type AuthRole = 'operator' | 'admin';

export function isAuthenticated(): boolean {
  if (typeof window === 'undefined') return false;
  return localStorage.getItem(AUTH_KEY) === '1';
}

export function getAuthRole(): AuthRole | null {
  if (typeof window === 'undefined') return null;
  if (!isAuthenticated()) return null;
  return localStorage.getItem(ROLE_KEY) === 'admin' ? 'admin' : 'operator';
}

export function isAdminAuthenticated(): boolean {
  return getAuthRole() === 'admin';
}

/**
 * Aceita login de operador (oficina) ou administrador (goncalo).
 * Ambos autenticam a app; só o admin vê Negócio.
 */
export function login(username: string, password: string): boolean {
  const trimmedUser = username.trim();

  if (trimmedUser === ADMIN_USER && password === ADMIN_PASS) {
    localStorage.setItem(AUTH_KEY, '1');
    localStorage.setItem(ROLE_KEY, 'admin');
    return true;
  }

  if (trimmedUser === AUTH_USER && password === AUTH_PASS) {
    localStorage.setItem(AUTH_KEY, '1');
    localStorage.setItem(ROLE_KEY, 'operator');
    return true;
  }

  return false;
}

export function logout(): void {
  localStorage.removeItem(AUTH_KEY);
  localStorage.removeItem(ROLE_KEY);
}
