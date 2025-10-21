import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-section-layout',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="panel">
      <div class="container grid">
        <aside class="left">
          <ul>
            <li *ngFor="let item of submenu">{{ item }}</li>
          </ul>
        </aside>

        <div class="right">
          <div class="card" *ngFor="let img of images">
            <img *ngIf="img" [src]="img" alt="" />
            <div class="ph" *ngIf="!img">Imagem</div>
          </div>
        </div>
      </div>
    </section>
  `,
  styleUrls: ['./section-layout.component.scss']
})
export class SectionLayoutComponent {
  @Input() submenu: string[] = [];
  @Input() images: string[] = [];
}
