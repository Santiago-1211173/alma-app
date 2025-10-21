import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { provideFirebaseApp } from '@angular/fire/app';
import { provideAuth, getAuth } from '@angular/fire/auth';

// 👇 usa estes de 'firebase/app' (SDK base) para evitar duplicados
import { initializeApp, getApp, getApps } from 'firebase/app';

import { environment } from '../environments/environment';
import { tokenInterceptor } from './core/token.interceptor';

const fb = {
  ...environment.firebase,
  authDomain: Array.isArray(environment.firebase.authDomain)
    ? environment.firebase.authDomain[0]
    : environment.firebase.authDomain
};

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([tokenInterceptor])),
    provideFirebaseApp(() => getApps().length ? getApp() : initializeApp(fb)),
    provideAuth(() => getAuth()),
  ]
};
