import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { AuthService } from './auth.service';
import { from } from 'rxjs';
import { switchMap } from 'rxjs/operators';

export const tokenInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  // getToken() returns a Promise<string | null>
  return from(auth.getToken()).pipe(
    switchMap(token => {
      if (token) {
        req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
      }
      return next(req);
    })
  );
};
