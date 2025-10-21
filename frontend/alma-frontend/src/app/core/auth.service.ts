import { Injectable, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Auth } from '@angular/fire/auth';
import {
  GoogleAuthProvider,
  onIdTokenChanged,
  signInWithPopup,
  signInWithRedirect,
  signOut,
  User
} from 'firebase/auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private auth = inject(Auth);
  user$ = new BehaviorSubject<User | null>(null);
  idToken$ = new BehaviorSubject<string | null>(null);

  constructor() {
    onIdTokenChanged(this.auth, async (u: User | null) => {
      this.user$.next(u);
      this.idToken$.next(u ? await u.getIdToken() : null);
    });
  }

  async loginWithGoogle() {
    try {
      await signInWithPopup(this.auth, new GoogleAuthProvider());
    } catch (e: any) {
      if (e?.code === 'auth/popup-blocked' || e?.message?.includes('popup')) {
        await signInWithRedirect(this.auth, new GoogleAuthProvider());
      } else {
        console.error('login error', e);
      }
    }
  }

  logout() { return signOut(this.auth); }
  async getToken(): Promise<string | null> { return this.idToken$.value ?? null; }
}
