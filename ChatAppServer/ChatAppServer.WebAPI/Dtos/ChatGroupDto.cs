﻿namespace ChatAppServer.WebAPI.Dtos
{
    public class ChatGroupDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public Guid? GroupId { get; set; }
        public string Message { get; set; }
        public string AttachmentUrl { get; set; }
        public DateTime Date { get; set; }
    }

}
