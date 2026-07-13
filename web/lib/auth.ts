const AUTH_KEY = 'botijas-auth';

const AUTH_USER = process.env.NEXT_PUBLIC_AUTH_USER ?? 'oficina';
const AUTH_PASS = process.env.NEXT_PUBLIC_AUTH_PASS ?? 'oficina';

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
}
