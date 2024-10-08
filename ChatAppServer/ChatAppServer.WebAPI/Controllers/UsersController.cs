﻿using ChatAppServer.WebAPI.Dtos;
using ChatAppServer.WebAPI.Models;
using ChatAppServer.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace ChatAppServer.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UsersController> _logger;
        private readonly IEmailService _emailService;

        public UsersController(ApplicationDbContext context, ILogger<UsersController> logger, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }
        [HttpGet("search")]
        public async Task<IActionResult> SearchUserByTagName(string tagName, CancellationToken cancellationToken)
        {
            try
            {
                // Validate the tagName
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    return BadRequest(new { message = "TagName is required" });
                }

                // Authenticate the current user
                var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (authenticatedUserId == null)
                {
                    return Forbid("You are not authorized to search users.");
                }

                _logger.LogInformation($"Searching for user with tagName: {tagName}");

                // Ensure tagName starts with '@'
                if (!tagName.StartsWith("@"))
                {
                    tagName = "@" + tagName;
                }

                // Get the authenticated user's TagName
                var authenticatedUser = await _context.Users
                    .Where(u => u.Id == Guid.Parse(authenticatedUserId))
                    .Select(u => u.TagName)
                    .FirstOrDefaultAsync(cancellationToken);

                if (authenticatedUser != null && tagName.ToLower() == authenticatedUser.ToLower())
                {
                    _logger.LogWarning($"User tried to search for themselves with tagName {tagName}");
                    return NotFound(new { message = "User not found" });
                }

                // Find the user by TagName
                var user = await _context.Users
                    .Where(u => u.TagName.ToLower() == tagName.ToLower())
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        u.Avatar,
                        u.Status,
                        u.TagName
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning($"User with tagName {tagName} not found");
                    return NotFound(new { message = "User not found" });
                }

                var authenticatedUserIdGuid = Guid.Parse(authenticatedUserId);

                // Check if the authenticated user is blocked by or has blocked the searched user
                var isBlockedByUser = await _context.UserBlocks
                    .AnyAsync(ub => ub.UserId == authenticatedUserIdGuid && ub.BlockedUserId == user.Id, cancellationToken);

                var isBlockedByTarget = await _context.UserBlocks
                    .AnyAsync(ub => ub.UserId == user.Id && ub.BlockedUserId == authenticatedUserIdGuid, cancellationToken);

                if (isBlockedByUser || isBlockedByTarget)
                {
                    _logger.LogWarning($"User with tagName {tagName} has blocked the authenticated user or vice versa");
                    return NotFound(new { message = "User not found" });
                }

                // Check if the authenticated user has sent a friend request to the searched user
                var friendRequestSent = await _context.FriendRequests
                    .FirstOrDefaultAsync(fr => fr.SenderId == authenticatedUserIdGuid && fr.ReceiverId == user.Id && fr.Status == "Pending", cancellationToken);

                // Check if the searched user has sent a friend request to the authenticated user
                var friendRequestReceived = await _context.FriendRequests
                    .FirstOrDefaultAsync(fr => fr.SenderId == user.Id && fr.ReceiverId == authenticatedUserIdGuid && fr.Status == "Pending", cancellationToken);

                var response = new
                {
                    user.Id,
                    user.Username,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.Avatar,
                    user.Status,
                    user.TagName,
                    HasSentRequest = friendRequestSent != null,
                    RequestId = friendRequestSent?.Id, // Returns null if no friend request was sent
                    HasReceivedRequest = friendRequestReceived != null,
                    ReceivedRequestId = friendRequestReceived?.Id // Returns null if no friend request was received
                };

                _logger.LogInformation($"User with tagName {tagName} found with friend request status");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while searching for user with tagName {TagName}", tagName);
                return StatusCode(500, new { message = "An error occurred while processing your request." });
            }
        }



        [HttpPost("update-avatar")]
        public async Task<IActionResult> UpdateGroupAvatar([FromForm] UpdateAvatarGroupDto request, CancellationToken cancellationToken)
        {
            try
            {
                var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (authenticatedUserId == null)
                {
                    return Forbid("You are not authorized to update this group avatar.");
                }

                var group = await _context.Groups.FindAsync(new object[] { request.GroupId }, cancellationToken);
                if (group == null)
                {
                    return NotFound(new { Message = "Group not found" });
                }

                var isAdmin = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == Guid.Parse(authenticatedUserId) && gm.IsAdmin, cancellationToken);
                if (!isAdmin)
                {
                    return Forbid("You are not authorized to update this group avatar.");
                }

                if (request.AvatarFile != null)
                {
                    // Save the current avatar path before updating
                    var oldAvatarPath = group.Avatar;

                    // Validate the file type and size if necessary
                    var (savedFileName, originalFileName) = FileService.FileSaveToServer(request.AvatarFile, "wwwroot/avatar/");
                    group.Avatar = Path.Combine("avatars", savedFileName).Replace("\\", "/");

                    // Delete the old avatar file if it exists
                    if (!string.IsNullOrEmpty(oldAvatarPath))
                    {
                        var fullOldAvatarPath = Path.Combine("wwwroot", oldAvatarPath.Replace("/", "\\"));
                        if (System.IO.File.Exists(fullOldAvatarPath))
                        {
                            System.IO.File.Delete(fullOldAvatarPath);
                        }
                    }
                }
                else
                {
                    return BadRequest(new { Message = "No avatar file provided" });
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation($"Group {request.GroupId} avatar updated");

                return Ok(new
                {
                    Message = "Group avatar updated successfully",
                    group.Id,
                    group.Name,
                    group.Avatar
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating avatar for group {GroupId}", request.GroupId);
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }


        [HttpPost("{userId}/update-status")]
        public async Task<IActionResult> UpdateStatus(Guid userId, [FromForm] string status, CancellationToken cancellationToken)
        {
            try
            {
                // Xác thực người dùng hiện tại
                var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (authenticatedUserId == null || userId.ToString() != authenticatedUserId)
                {
                    return Forbid("You are not authorized to update this user's status.");
                }

                // Xác định các trạng thái hợp lệ
                var validStatuses = new List<string> { "online", "offline" };
                if (string.IsNullOrWhiteSpace(status) || !validStatuses.Contains(status.ToLower()))
                {
                    return BadRequest(new { Message = "Invalid status. Status must be 'online' or 'offline'." });
                }

                // Tìm kiếm người dùng trong cơ sở dữ liệu
                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                // Cập nhật trạng thái người dùng
                user.Status = status.ToLower();
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation($"User {userId} status updated to {status}");

                return Ok(new { Message = "Status updated successfully", user.Status });
            }
            catch (Exception ex)
            {
                // Ghi log lỗi và trả về mã lỗi 500
                _logger.LogError(ex, $"An error occurred while updating status for user {userId}");
                return StatusCode(500, "An error occurred while updating the status. Please try again later.");
            }
        }


        [HttpPost("{userId}/update-status-visibility")]
        public async Task<IActionResult> UpdateStatusVisibility(Guid userId, [FromForm] bool showOnlineStatus, CancellationToken cancellationToken)
        {
            try
            {
                // Xác thực người dùng hiện tại
                var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (authenticatedUserId == null || userId.ToString() != authenticatedUserId)
                {
                    return Forbid("You are not authorized to update this user's status visibility.");
                }

                // Tìm kiếm người dùng trong cơ sở dữ liệu
                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Cập nhật trạng thái hiển thị của người dùng
                user.ShowOnlineStatus = showOnlineStatus;
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation($"User {userId} show online status updated to {showOnlineStatus}");

                return Ok(new { Message = "Status visibility updated successfully", user.ShowOnlineStatus });
            }
            catch (Exception ex)
            {
                // Ghi log lỗi và trả về mã lỗi 500
                _logger.LogError(ex, $"An error occurred while updating status visibility for user {userId}");
                return StatusCode(500, "An error occurred while updating the status visibility. Please try again later.");
            }
        }

        [HttpGet("{userId}/status")]
        public async Task<IActionResult> GetStatus(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                // Xác thực người dùng hiện tại
                var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (authenticatedUserId == null || userId.ToString() != authenticatedUserId)
                {
                    return Forbid("You are not authorized to view this user's status.");
                }

                // Tìm kiếm người dùng trong cơ sở dữ liệu
                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                return Ok(new { user.Status });
            }
            catch (Exception ex)
            {
                // Ghi log lỗi và trả về mã lỗi 500
                _logger.LogError(ex, $"An error occurred while retrieving the status for user {userId}");
                return StatusCode(500, "An error occurred while retrieving the user status. Please try again later.");
            }
        }
        [HttpPut("{userId}/update-user")]
        public async Task<IActionResult> UpdateUser(Guid userId, [FromForm] UpdateUserDto request, CancellationToken cancellationToken)
        {
            try
            {
                var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (authenticatedUserId == null || userId.ToString() != authenticatedUserId)
                {
                    return Forbid("You are not authorized to update this user.");
                }

                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                bool emailChanged = false;
                bool tagNameChanged = false;

                // Update FirstName if provided
                if (!string.IsNullOrEmpty(request.FirstName))
                {
                    var normalizedFirstName = NormalizeName(request.FirstName);
                    if (!IsValidName(normalizedFirstName))
                    {
                        return BadRequest(new { Message = "Invalid FirstName format. Only letters and spaces are allowed." });
                    }
                    user.FirstName = normalizedFirstName;
                }

                // Update LastName if provided
                if (!string.IsNullOrEmpty(request.LastName))
                {
                    var normalizedLastName = NormalizeName(request.LastName);
                    if (!IsValidName(normalizedLastName))
                    {
                        return BadRequest(new { Message = "Invalid LastName format. Only letters and spaces are allowed." });
                    }
                    user.LastName = normalizedLastName;
                }

                // Update Birthday if provided
                if (request.Birthday != null)
                {
                    user.Birthday = request.Birthday.Value;
                }

                // Update Email if provided
                if (!string.IsNullOrEmpty(request.Email) && !request.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsValidEmail(request.Email))
                    {
                        return BadRequest(new { Message = "Invalid email format." });
                    }
                    emailChanged = true;
                    user.Email = request.Email;
                }

                // Update Avatar if a new file is provided
                if (request.AvatarFile != null)
                {
                    if (request.AvatarFile.Length > 5 * 1024 * 1024) // Giới hạn 5MB
                    {
                        return BadRequest(new { Message = "Avatar file size exceeds the limit of 5MB." });
                    }

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(request.AvatarFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return BadRequest(new { Message = "Invalid file format. Only JPG and PNG are allowed." });
                    }

                    // Check if the user has an existing avatar and delete it
                    if (!string.IsNullOrEmpty(user.Avatar))
                    {
                        var oldAvatarPath = Path.Combine("wwwroot", user.Avatar);
                        if (System.IO.File.Exists(oldAvatarPath))
                        {
                            System.IO.File.Delete(oldAvatarPath);
                        }
                    }

                    // Save the new avatar
                    var (savedFileName, originalFileName) = FileService.FileSaveToServer(request.AvatarFile, "wwwroot/avatars/");
                    user.Avatar = Path.Combine("avatars", savedFileName).Replace("\\", "/");
                    user.OriginalAvatarFileName = originalFileName;
                }

                // Update TagName if provided
                if (!string.IsNullOrEmpty(request.TagName) && !request.TagName.Equals(user.TagName, StringComparison.OrdinalIgnoreCase))
                {
                    tagNameChanged = true;

                    // Normalize and validate TagName
                    var normalizedTagName = NormalizeTagName(request.TagName);
                    if (!IsValidTagName(normalizedTagName))
                    {
                        return BadRequest(new { Message = "Invalid TagName format. Only letters, numbers, and one '@' at the beginning are allowed." });
                    }

                    // Check if TagName already exists
                    if (await _context.Users.AnyAsync(u => u.TagName == normalizedTagName, cancellationToken))
                    {
                        return BadRequest(new { Message = "TagName already exists. Please choose a different TagName." });
                    }
                    user.TagName = normalizedTagName;
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation($"User {userId} information updated");

                if (emailChanged)
                {
                    await _emailService.SendEmailConfirmationAsync(user.Email, user.FirstName, user.LastName);
                }

                return Ok(new
                {
                    Message = "User information updated successfully",
                    user.Id,
                    user.Username,
                    user.FirstName,
                    user.LastName,
                    user.Birthday,
                    user.Email,
                    user.Avatar,
                    user.OriginalAvatarFileName,
                    user.TagName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while updating user {userId}");
                return StatusCode(500, "An error occurred while updating the user. Please try again later.");
            }
        }

        // Helper method to normalize names (remove extra spaces)
        private string NormalizeName(string name)
        {
            // Replace multiple spaces with a single space
            return Regex.Replace(name, @"\s+", " ").Trim();
        }

        // Helper method to normalize TagName (remove extra spaces and convert to lowercase)
        private string NormalizeTagName(string tagName)
        {
            // Replace multiple spaces with a single space and remove diacritics
            tagName = Regex.Replace(RemoveDiacritics(tagName), @"\s+", "").ToLower();

            // Ensure the TagName starts with '@'
            if (!tagName.StartsWith("@"))
            {
                tagName = "@" + tagName;
            }

            return tagName;
        }

        // Helper method to validate names
        private bool IsValidName(string name)
        {
            // Regular expression to allow letters from any language and spaces
            var regex = new Regex(@"^[\p{L}\s]+$", RegexOptions.Compiled);
            return regex.IsMatch(name);
        }

        // Helper method to validate TagName
        private bool IsValidTagName(string tagName)
        {
            // Regular expression to allow letters, numbers, and exactly one '@' at the beginning
            var regex = new Regex(@"^@[a-zA-Z0-9]+$", RegexOptions.Compiled);
            return regex.IsMatch(tagName);
        }

        // Phương thức loại bỏ dấu tiếng Việt
        private string RemoveDiacritics(string text)
        {
            string normalizedString = text.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            foreach (char c in normalizedString)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            // Normalize lại chuỗi để đưa về dạng chuẩn
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }



        [HttpGet("{userId}/get-user-info")]
        public async Task<IActionResult> GetUserInfo(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                // Xác thực người dùng hiện tại
                var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (authenticatedUserId == null || userId.ToString() != authenticatedUserId)
                {
                    return Forbid("You are not authorized to view this user's info.");
                }

                // Tìm kiếm người dùng trong cơ sở dữ liệu
                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Trả về thông tin người dùng
                return Ok(new
                {
                    user.Id,
                    user.Username,
                    user.FirstName,
                    user.LastName,
                    user.Birthday,
                    user.Email,
                    user.Avatar,
                    user.Status,
                    user.ShowOnlineStatus,
                    user.TagName
                });
            }
            catch (Exception ex)
            {
                // Ghi log lỗi và trả về mã lỗi 500
                _logger.LogError(ex, $"An error occurred while retrieving the info for user {userId}");
                return StatusCode(500, "An error occurred while retrieving the user info. Please try again later.");
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}