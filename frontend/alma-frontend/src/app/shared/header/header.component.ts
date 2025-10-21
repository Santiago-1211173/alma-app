import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { map } from 'rxjs/operators';

type SubItem = { label: string; route: string };
type NavItem = {
  label: string;
  route?: string;
  submenu?: SubItem[];
  images?: string[]; // <- imagens a mostrar no hover
};

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.scss'],
})


export class HeaderComponent {
  loggedIn$: any;
  loggingIn$: any;

  constructor(public auth: AuthService) {
    this.loggedIn$ = this.auth.user$.pipe(map(u => !!u));
    this.loggingIn$ = this.auth.loggingIn$;
  }

  login()  { this.auth.loginWithGoogle(); }
  logout() { this.auth.logout(); }

  openItem: NavItem | null = null;

  navItems: NavItem[] = [
    { label: 'A Alma', route: '/' },
    { label: 'Benefícios Membro', route: '/beneficios-membro' },
    { label: 'Marcar Sessão', route: '/marcar-sessao' },
    { label: 'Reservar aulas', route: '/reservar-aulas' },

    {
      label: 'Workshops',
      submenu: [
        { label: 'Workshops em vigor', route: '/workshops' },
        { label: 'Workshops anteriores', route: '/workshops' },
        { label: 'Galeria', route: '/workshops' },
        { label: 'Feedback', route: '/workshops' },
      ],
      images: ['assets/workshops-1.jpg', 'assets/workshops-2.jpg'],
    },

    {
      label: 'Bem Estar e cultura',
      submenu: [
        { label: 'Massagens de Relaxamento', route: '/bem-estar-cultura' },
        { label: 'Terapias', route: '/bem-estar-cultura' },
        { label: 'Nutrição', route: '/bem-estar-cultura' },
        { label: 'Psicologia', route: '/bem-estar-cultura' },
      ],
      images: ['assets/bemestar-1.jpg', 'assets/bemestar-2.jpg'],
    },

    {
      label: 'Exercício Físico',
      submenu: [
        { label: 'Personal Trainer', route: '/exercicio-fisico' },
        { label: 'Personal Trainer - Yoga', route: '/exercicio-fisico' },
        { label: 'Personal Trainer - Pilates', route: '/exercicio-fisico' },
        { label: 'Personal Trainer - Barre', route: '/exercicio-fisico' },
        { label: 'Pilates (Clássico/Clínico)', route: '/exercicio-fisico' },
        { label: 'Aulas (Yoga/Barre/Danças de Salão)', route: '/exercicio-fisico' },
      ],
      images: ['assets/exercicio-1.jpg'],
    },

    {
      label: 'Kids',
      submenu: [{ label: 'Alma Dance Group Kids', route: '/kids' }],
      images: ['assets/kids-1.jpg', 'assets/kids-2.jpg'],
    },
  ];

  showSubmenu(item: NavItem) { this.openItem = item; }
  hideSubmenu() { this.openItem = null; }
}
