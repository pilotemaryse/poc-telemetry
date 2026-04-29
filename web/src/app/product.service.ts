import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Product {
  id: number;
  name: string;
  price: number;
  stock: number;
}

export type ProductInput = Omit<Product, 'id'>;

@Injectable({ providedIn: 'root' })
export class ProductService {
  private http = inject(HttpClient);
  private base = '/api/products';

  getAll(): Observable<Product[]> { return this.http.get<Product[]>(this.base); }
  create(p: ProductInput): Observable<Product> { return this.http.post<Product>(this.base, p); }
  update(id: number, p: ProductInput): Observable<void> { return this.http.put<void>(`${this.base}/${id}`, p); }
  delete(id: number): Observable<void> { return this.http.delete<void>(`${this.base}/${id}`); }
}
