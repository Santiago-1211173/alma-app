import { Component } from '@angular/core';
import { SectionLayoutComponent } from '../../shared/section-layout/section-layout.component';

@Component({
  selector: 'app-workshops',
  standalone: true,
  imports: [SectionLayoutComponent],
  template: `
    <app-section-layout
      [submenu]="['Workshops em vigor','Workshops anteriores','Galeria','Feedback']"
      [images]="['assets/workshops-1.jpg','assets/workshops-2.jpg']">
    </app-section-layout>
  `
})
export class WorkshopsComponent {}
