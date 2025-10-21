import { Component } from '@angular/core';
import { SectionLayoutComponent } from '../../shared/section-layout/section-layout.component';

@Component({
  selector: 'app-kids',
  standalone: true,
  imports: [SectionLayoutComponent],
  template: `
    <app-section-layout
      [submenu]="['Alma Dance Group Kids']"
      [images]="['assets/kids-1.jpg','assets/kids-2.jpg']">
    </app-section-layout>
  `
})
export class KidsComponent {}
