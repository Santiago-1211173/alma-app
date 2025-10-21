import { Component } from '@angular/core';
import { SectionLayoutComponent } from '../../shared/section-layout/section-layout.component';

@Component({
  selector: 'app-exercicio-fisico',
  standalone: true,
  imports: [SectionLayoutComponent],
  template: `
    <app-section-layout
      [submenu]="[
        'Personal Trainer',
        'Personal Trainer - Yoga',
        'Personal Trainer - Pilates',
        'Personal Trainer - Barre',
        'Pilates (Clássico/Clínico)',
        'Aulas (Yoga/Barre/Danças de Salão)']"
      [images]="['assets/exercicio-1.jpg','assets/exercicio-2.jpg']">
    </app-section-layout>
  `
})
export class ExercicioFisicoComponent {}
