import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class AppConfigService {

  // Khai báo baseURL
  private baseUrl = 'https://localhost:7267';

  constructor() { }

  // Phương thức để lấy baseURL
  getBaseUrl(): string {
    return this.baseUrl;
  }

  // Phương thức để cập nhật baseURL nếu cần
  setBaseUrl(url: string): void {
    this.baseUrl = url;
  }
}
