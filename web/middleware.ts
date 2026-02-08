import { NextRequest, NextResponse } from 'next/server';
import { defaultLocale } from './i18n';

export function middleware(request: NextRequest) {
  const response = NextResponse.next();
  
  // Se não tiver cookie de locale, definir o padrão
  if (!request.cookies.get('locale')) {
    response.cookies.set('locale', defaultLocale, {
      path: '/',
      maxAge: 60 * 60 * 24 * 365 // 1 ano
    });
  }
  
  return response;
}

export const config = {
  // Match all pathnames except for
  // - … if they start with `/api`, `/_next` or `/_vercel`
  // - … the ones containing a dot (e.g. `favicon.ico`)
  matcher: ['/((?!api|_next|_vercel|.*\\..*).*)']
};
