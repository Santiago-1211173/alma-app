import { Injectable, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Auth } from '@angular/fire/auth';
import {
  GoogleAuthProvider,
  onIdTokenChanged,
  signInWithPopup,
  signInWithRedirect,
  signOut,
  setPersistence,
  browserLocalPersistence,
  indexedDBLocalPersistence,
  User
} from 'firebase/auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private auth = inject(Auth);

  user$    = new BehaviorSubject<User | null>(null);
  idToken$ = new BehaviorSubject<string | null>(null);
  loggingIn$ = new BehaviorSubject<boolean>(false); // para desactivar botão

  constructor() {
    // Persistência (local → mantém sessão entre reloads)
    setPersistence(this.auth, indexedDBLocalPersistence)
      .catch(() => setPersistence(this.auth, browserLocalPersistence))
      .catch(() => { /* ignora, continua com default */ });

    onIdTokenChanged(this.auth, async (u: User | null) => {
      this.user$.next(u);
      this.idToken$.next(u ? await u.getIdToken() : null);
    });
  }

  async loginWithGoogle() {
    if (this.loggingIn$.value) return;
    this.loggingIn$.next(true);
    const provider = new GoogleAuthProvider();

    try {
      await signInWithPopup(this.auth, provider);
    } catch (err: any) {
      const code = err?.code || '';
      // Fallbacks típicos de UI
      if (
        code === 'auth/popup-closed-by-user' ||
        code === 'auth/popup-blocked' ||
        code === 'auth/cancelled-popup-request'
      ) {
        // Usa redirect quando o popup é fechado/bloqueado
        await signInWithRedirect(this.auth, provider);
      } else {
        // Outros erros (ex.: domínio não autorizado)
        console.error('login error', err);
        alert('Não foi possível iniciar sessão. Verifica a consola para detalhes.');
      }
    } finally {
      this.loggingIn$.next(false);
    }
  }

  logout() {
    return signOut(this.auth);
  }

  async getToken(): Promise<string | null> {
    return this.idToken$.value ?? null;
  }
}
