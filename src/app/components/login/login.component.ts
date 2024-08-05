import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {LoginModel} from "../../models/login.model";

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent {
  loginModel: LoginModel = new LoginModel();

  constructor(private http: HttpClient, private router: Router) {}

  login() {
    // Tạo FormData để gửi dữ liệu dưới dạng form-data
    const formData = new FormData();
    formData.append('Username', this.loginModel.userName);
    formData.append('Password', this.loginModel.password);

    // Sử dụng phương thức POST để gửi dữ liệu
    this.http.post('https://localhost:7267/api/Auth/Login', formData, {
      headers: new HttpHeaders({
        'Accept': 'application/json'
      })
    }).subscribe({
      next: (res: any) => {
        localStorage.setItem('accessToken', JSON.stringify(res));
        this.router.navigateByUrl('/');
      },
      error: (err) => {
        console.error('Login failed', err);
      }
    });
  }
}
