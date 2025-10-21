import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AsyncPipe, NgIf } from '@angular/common';
import { AuthService } from '../../core/auth.service';
import { map } from 'rxjs/operators';

@Component({
  selector: 'app-header',
  standalone: true,
  // usa styleUrls (plural) para não haver dúvidas
  styleUrls: ['./header.component.scss'],
  templateUrl: './header.component.html',
  imports: [RouterLink, RouterLinkActive, AsyncPipe, NgIf]
})
export class HeaderComponent {
  private auth = inject(AuthService);
  loggedIn$ = this.auth.user$.pipe(map(u => !!u));
  login(){ this.auth.loginWithGoogle(); }
  logout(){ this.auth.logout(); }
}
