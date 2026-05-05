import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { Product, ProductInput, ProductService } from './product.service';
import { ErrorService } from './error.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private svc = inject(ProductService);
  private errSvc = inject(ErrorService);

  products: Product[] = [];
  form: ProductInput = { name: '', price: 0, stock: 0 };
  editingId: number | null = null;
  error: string | null = null;
  lastSimulation: string | null = null;

  ngOnInit() { this.load(); }

  load() {
    this.svc.getAll().subscribe({
      next: (ps: Product[]) => this.products = ps,
      error: (e: { message: string }) => this.error = e.message
    });
  }

  submit() {
    this.error = null;
    if (!this.form.name.trim()) { this.error = 'Name required'; return; }

    const op: Observable<unknown> = this.editingId === null
      ? this.svc.create(this.form)
      : this.svc.update(this.editingId, this.form);

    op.subscribe({
      next: () => { this.reset(); this.load(); },
      error: (e: { message: string }) => this.error = e.message
    });
  }

  edit(p: Product) {
    this.editingId = p.id;
    this.form = { name: p.name, price: p.price, stock: p.stock };
  }

  remove(id: number) {
    if (!confirm('Delete this product?')) return;
    this.svc.delete(id).subscribe({ next: () => this.load(), error: (e: { message: string }) => this.error = e.message });
  }

  reset() {
    this.editingId = null;
    this.form = { name: '', price: 0, stock: 0 };
  }

  simulate(kind: 'throw' | 'notfound' | 'badrequest' | 'slow') {
    this.lastSimulation = `Calling /${kind}...`;
    this.errSvc.trigger(kind).subscribe({
      next: () => { this.lastSimulation = `${kind}: 200 OK`; },
      error: (e: { status: number; message: string }) => {
        this.lastSimulation = `${kind}: HTTP ${e.status} — ${e.message}`;
      }
    });
  }
}
