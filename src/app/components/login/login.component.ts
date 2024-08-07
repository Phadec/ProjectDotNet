import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { LoginModel } from '../../models/login.model';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent {
  loginModel: LoginModel = new LoginModel();
  errorMessage: string | null = null;
  usernameError: string | null = null;
  passwordError: string | null = null;
  showPassword: boolean = false;

  constructor(private http: HttpClient, private router: Router) {}

  login() {
    this.usernameError = null;
    this.passwordError = null;
    this.errorMessage = null; // Reset error message

    if (!this.loginModel.userName || !this.loginModel.password) {
      if (!this.loginModel.userName) {
        this.usernameError = 'Please enter your username.';
      }
      if (!this.loginModel.password) {
        this.passwordError = 'Please enter your password.';
      }
      return;
    }

    const formData = new FormData();
    formData.append('Username', this.loginModel.userName);
    formData.append('Password', this.loginModel.password);

    this.http.post('https://localhost:7267/api/Auth/Login/UserLogin', formData, {
      headers: new HttpHeaders({
        'Accept': 'application/json'
      })
    }).subscribe({
      next: (res: any) => {
        // Giả sử res chứa token
        localStorage.setItem('userToken', res.token);
        this.router.navigateByUrl('/home');
      },
      error: (err) => {
        console.error('Login failed', err);
        this.errorMessage = 'Invalid username or password. Please try again.';
      }
    });
  }
}
