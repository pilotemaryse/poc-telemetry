import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ErrorService {
  private http = inject(HttpClient);
  private base = '/api/errors';

  trigger(kind: 'throw' | 'notfound' | 'badrequest' | 'slow'): Observable<unknown> {
    return this.http.get(`${this.base}/${kind}`);
  }
}
