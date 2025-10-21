import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { BeneficiosMembroComponent } from './pages/beneficios-membro/beneficios-membro.component';
import { MarcarSessaoComponent } from './pages/marcar-sessao/marcar-sessao.component';
import { ReservarAulasComponent } from './pages/reservar-aulas/reservar-aulas.component';
import { WorkshopsComponent } from './pages/workshops/workshops.component';
import { BemEstarCulturaComponent } from './pages/bem-estar-cultura/bem-estar-cultura.component';
import { ExercicioFisicoComponent } from './pages/exercicio-fisico/exercicio-fisico.component';
import { KidsComponent } from './pages/kids/kids.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'beneficios-membro', component: BeneficiosMembroComponent },
  { path: 'marcar-sessao', component: MarcarSessaoComponent },
  { path: 'reservar-aulas', component: ReservarAulasComponent },
  { path: 'workshops', component: WorkshopsComponent },
  { path: 'bem-estar-cultura', component: BemEstarCulturaComponent },
  { path: 'exercicio-fisico', component: ExercicioFisicoComponent },
  { path: 'kids', component: KidsComponent },
  { path: '**', redirectTo: '' }
];
