import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RegisterModel } from '../../models/register.model';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.css']
})
export class RegisterComponent {
  registerModel: RegisterModel = new RegisterModel();
  usernameError: boolean = false;
  passwordError: boolean = false;
  confirmPasswordError: boolean = false;
  passwordLengthError: boolean = false;
  passwordMismatchError: boolean = false;
  firstNameError: boolean = false;
  lastNameError: boolean = false;
  birthDateError: boolean = false;
  emailError: boolean = false;
  emailFormatError: boolean = false;
  avatarError: boolean = false;

  constructor(private http: HttpClient, private router: Router) {}

  register() {
    this.usernameError = !this.registerModel.userName;
    this.passwordError = !this.registerModel.password;
    this.passwordLengthError = this.registerModel.password.length > 0 && this.registerModel.password.length < 8;
    this.confirmPasswordError = !this.registerModel.confirmPassword;
    this.passwordMismatchError = this.registerModel.password !== this.registerModel.confirmPassword;
    this.firstNameError = !this.registerModel.firstName;
    this.lastNameError = !this.registerModel.lastName;
    this.birthDateError = !this.registerModel.birthDate;
    this.emailError = !this.registerModel.email;
    this.emailFormatError = this.registerModel.email !== '' && !this.isValidEmail(this.registerModel.email);
    this.avatarError = !this.registerModel.avatar;

    if (this.usernameError || this.passwordError || this.passwordLengthError || this.confirmPasswordError || this.passwordMismatchError || this.firstNameError || this.lastNameError || this.birthDateError || this.emailError || this.emailFormatError || this.avatarError) {
      return;
    }

    const formData = new FormData();
    formData.append('Username', this.registerModel.userName);
    formData.append('Password', this.registerModel.password);
    formData.append('RetypePassword', this.registerModel.confirmPassword);
    formData.append('FirstName', this.registerModel.firstName);
    formData.append('LastName', this.registerModel.lastName);
    formData.append('Birthday', this.registerModel.birthDate);
    formData.append('Email', this.registerModel.email);
    formData.append('File', this.registerModel.avatar as File);

    formData.forEach((value, key) => {
      console.log(`${key}: ${value}`);
    });

    this.http.post('https://localhost:7267/api/Auth/Register/RegisterUser', formData, {
      headers: new HttpHeaders({
        'Accept': 'application/json'
      })
    }).subscribe({
      next: (res: any) => {
        console.log('Registration successful', res);
        localStorage.setItem('accessToken', JSON.stringify(res));
        this.router.navigateByUrl('/login');
      },
      error: (err) => {
        console.error('Registration failed', err);
        if (err.error) {
          console.error('Error details:', err.error);
        } else {
          console.error('Error:', err.message);
        }
        // In chi tiết lỗi ra console để hiểu rõ hơn về vấn đề
        console.log('Full error details:', err);
      }
    });
  }

  isValidEmail(email: string): boolean {
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return re.test(email);
  }

  setImage(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.registerModel.avatar = file;
      this.avatarError = false;
    } else {
      this.registerModel.avatar = null;
      this.avatarError = true;
    }
  }
}
