import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {AppConfigService} from "./app-config.service";

@Injectable({
  providedIn: 'root'
})
export class FriendsService {
  private apiUrl: string;
  constructor(private http: HttpClient, private appConfig: AppConfigService) {
    this.apiUrl = `${this.appConfig.getBaseUrl()}/api/Friends`;
  }

  getSentFriendRequests(userId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/${userId}/get-sent-friend-requests`);
  }

  changeNickname(userId: string, friendId: string, nickname: string): Observable<any> {
    const payload = { FriendId: friendId, Nickname: nickname };
    return this.http.post(`${this.apiUrl}/${userId}/change-nickname`, payload);
  }


  addFriend(userId: string, friendId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${userId}/add/${friendId}`, {});
  }

  removeFriend(userId: string, friendId: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${userId}/remove/${friendId}`);
  }

  getFriends(userId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/${userId}/friends`);
  }

  getFriendRequests(userId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/${userId}/friend-requests`);
  }

  acceptFriendRequest(userId: string, requestId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${userId}/accept-friend-request/${requestId}`, {});
  }

  rejectFriendRequest(userId: string, requestId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${userId}/reject-friend-request/${requestId}`, {});
  }

  cancelFriendRequest(userId: string, requestId: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${userId}/cancel-friend-request/${requestId}`);
  }

  blockUser(userId: string, blockedUserId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${userId}/block/${blockedUserId}`, {});
  }

  unblockUser(userId: string, blockedUserId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${userId}/unblock/${blockedUserId}`, {});
  }

  getBlockedUsers(userId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/${userId}/blocked-users`);
  }
  getFriendInfo(userId: string, entityId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/${userId}/relationship-info/${entityId}`);
  }
  updateChatTheme(userId: string, friendId: string, theme: string): Observable<any> {
    const payload = { theme }; // Tạo payload với theme được gửi lên API
    return this.http.post(`${this.apiUrl}/${userId}/update-chat-theme/${friendId}`, payload);
  }
}
