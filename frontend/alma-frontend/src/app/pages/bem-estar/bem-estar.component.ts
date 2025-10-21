import { Component } from '@angular/core';
import { SectionLayoutComponent } from '../../shared/section-layout/section-layout.component';

@Component({
  selector: 'app-bem-estar',
  standalone: true,
  imports: [SectionLayoutComponent],
  template: `
    <app-section-layout
      [submenu]="['Massagens de Relaxamento','Terapias','Nutrição','Psicologia']"
      [images]="['assets/bemestar-1.jpg','assets/bemestar-2.jpg']">
    </app-section-layout>
  `
})
export class BemEstarComponent {}
