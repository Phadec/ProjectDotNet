import { Injectable } from '@angular/core';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  constructor(private router: Router) { }

  isAuthenticated(){
    if(localStorage.getItem("accessToken")){
      return true;
    }

    this.router.navigateByUrl("/login");
    return false;
  }
// Giả sử bạn có một cách để lưu trạng thái đăng nhập, ví dụ như token trong localStorage
  isLoggedIn(): boolean {
    return !!localStorage.getItem('userToken');  // Hoặc cách kiểm tra khác bạn dùng
  }
}
