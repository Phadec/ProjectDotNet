﻿namespace ChatAppServer.WebAPI.Dtos
{
    public class RegisterDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string RetypePassword { get; set; } // Thêm trường này
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Birthday { get; set; }
        public string Email { get; set; }
        public IFormFile? File { get; set; }
    }
}
