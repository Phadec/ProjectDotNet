import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {AppConfigService} from "./app-config.service";

@Injectable({
  providedIn: 'root'
})
export class GroupService {

  private apiUrl: string;
  constructor(private http: HttpClient,private appConfig: AppConfigService) {
    this.apiUrl =` ${this.appConfig.getBaseUrl()}/api/Groups`;
  }

  createGroupChat(request: FormData): Observable<any> {
    return this.http.post(`${this.apiUrl}/create-group-chat`, request);
  }

  addGroupChatMember(request: { GroupId: string, UserId: string }): Observable<any> {
    const formData = new FormData();
    formData.append('GroupId', request.GroupId);
    formData.append('UserId', request.UserId);
    return this.http.post(`${this.apiUrl}/add-group-chat-member`, formData);
  }

  deleteGroup(groupId: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${groupId}/delete`);
  }

  getGroupMembers(groupId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/${groupId}/members`);
  }

  removeGroupMember(request: { GroupId: string, UserId: string }): Observable<any> {
    const formData = new FormData();
    formData.append('GroupId', request.GroupId);
    formData.append('UserId', request.UserId);
    return this.http.delete(`${this.apiUrl}/remove-member`, { body: formData });
  }

  getUserGroupsWithDetails(userId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/user-groups-with-details/${userId}`);
  }

  renameGroup(groupId: string, newName: string): Observable<any> {
    return this.http.put(`${this.apiUrl}/rename-group`, { groupId, newName });
  }
  updateGroupAdmin(request: { GroupId: string, UserId: string }): Observable<any> {
    const formData = new FormData();
    formData.append('GroupId', request.GroupId);
    formData.append('UserId', request.UserId);
    return this.http.post(`${this.apiUrl}/update-admin`, formData);
  }

  revokeGroupAdmin(request: { GroupId: string, UserId: string }): Observable<any> {
    const formData = new FormData();
    formData.append('GroupId', request.GroupId);
    formData.append('UserId', request.UserId);
    return this.http.post(`${this.apiUrl}/revoke-admin`, formData);
  }

  updateGroupAvatar(request: { GroupId: string, AvatarFile: File }): Observable<any> {
    const formData = new FormData();
    formData.append('GroupId', request.GroupId);
    formData.append('AvatarFile', request.AvatarFile);
    return this.http.post(`${this.apiUrl}/update-avatar`, formData);
  }
  getFriendsNotInGroup(groupId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/${groupId}/non-members`);
  }
  updateChatTheme(groupId: string, theme: string): Observable<any> {
    const payload = { theme }; // Tạo payload với theme được gửi lên API
    return this.http.post(`${this.apiUrl}/${groupId}/update-chat-theme`, payload);
  }
}
