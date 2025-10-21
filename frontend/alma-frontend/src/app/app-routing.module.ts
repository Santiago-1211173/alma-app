import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { AAlmaComponent } from './pages/a-alma/a-alma.component';
import { BeneficiosMembroComponent } from './pages/beneficios-membro/beneficios-membro.component';
import { WorkshopsComponent } from './pages/workshops/workshops.component';
import { ExercicioFisicoComponent } from './pages/exercicio-fisico/exercicio-fisico.component';
import { KidsComponent } from './pages/kids/kids.component';
import { BemEstarCulturaComponent } from './pages/bem-estar-cultura/bem-estar-cultura.component';


const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'a-alma', component: AAlmaComponent },
  { path: 'beneficios-membro', component: BeneficiosMembroComponent },
  { path: 'workshops', component: WorkshopsComponent },
  { path: 'bem-estar', component: BemEstarCulturaComponent },
  { path: 'exercicio-fisico', component: ExercicioFisicoComponent },
  { path: 'kids', component: KidsComponent },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { bindToComponentInputs: true })],
  exports: [RouterModule]
})
export class AppRoutingModule {}
