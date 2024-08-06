﻿using ChatAppServer.WebAPI.Dtos;
using ChatAppServer.WebAPI.Models;
using ChatAppServer.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatAppServer.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public sealed class GroupsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GroupsController> _logger;

        public GroupsController(ApplicationDbContext context, ILogger<GroupsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromForm] CreateGroupDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { Message = "Group name is required" });
            }

            if (request.MemberIds == null || request.MemberIds.Count < 3)
            {
                return BadRequest(new { Message = "A group must have at least 3 members." });
            }

            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null)
            {
                return Forbid("You are not authorized to create this group.");
            }

            if (!request.MemberIds.Contains(Guid.Parse(authenticatedUserId)))
            {
                return BadRequest(new { Message = "The group creator must be a member of the group." });
            }

            string avatarUrl = null;
            if (request.AvatarFile != null)
            {
                (string savedFileName, string originalFileName) = FileService.FileSaveToServer(request.AvatarFile, "wwwroot/avatar/");
                avatarUrl = Path.Combine("avatar", savedFileName).Replace("\\", "/"); // Tạo đường dẫn tương đối từ tên tệp và thay thế gạch chéo ngược bằng gạch chéo
            }

            var group = new Group
            {
                Name = request.Name,
                Avatar = avatarUrl
            };

            await _context.Groups.AddAsync(group, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var members = new List<object>();

            foreach (var userId in request.MemberIds)
            {
                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user == null)
                {
                    return NotFound(new { Message = $"User with Id {userId} not found" });
                }

                var groupMember = new GroupMember
                {
                    GroupId = group.Id,
                    UserId = userId,
                    IsAdmin = userId == Guid.Parse(authenticatedUserId) // Set the group creator as admin
                };

                await _context.GroupMembers.AddAsync(groupMember, cancellationToken);

                members.Add(new
                {
                    user.Id,
                    user.Username,
                    user.FirstName,
                    user.LastName,
                    user.Birthday,
                    user.Email,
                    user.Avatar,
                    user.Status
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation($"Group {group.Name} created with ID {group.Id}");

            return CreatedAtAction(nameof(GetGroupMembers), new { groupId = group.Id }, new
            {
                id = group.Id,
                name = group.Name,
                avatar = group.Avatar,
                members
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddMember([FromForm] AddGroupMemberDto request, CancellationToken cancellationToken)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null)
            {
                return Forbid("You are not authorized to add members to this group.");
            }

            var group = await _context.Groups.FindAsync(new object[] { request.GroupId }, cancellationToken);
            if (group == null)
            {
                return NotFound(new { Message = "Group not found" });
            }

            var isAdmin = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == Guid.Parse(authenticatedUserId) && gm.IsAdmin, cancellationToken);
            if (!isAdmin)
            {
                return Forbid("You are not authorized to add members to this group.");
            }

            var user = await _context.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            var isMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == request.UserId, cancellationToken);
            if (isMember)
            {
                return Conflict(new { Message = "User is already a member of the group" });
            }

            var groupMember = new GroupMember
            {
                GroupId = request.GroupId,
                UserId = request.UserId
            };

            await _context.GroupMembers.AddAsync(groupMember, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"User {user.Username} added to group {group.Name}");

            return Ok(new
            {
                user.Id,
                user.Username,
                user.FirstName,
                user.LastName,
                user.Birthday,
                user.Email,
                user.Avatar,
                user.Status
            });
        }

        [HttpDelete("{groupId}")]
        public async Task<IActionResult> DeleteGroup(Guid groupId, CancellationToken cancellationToken)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null)
            {
                return Forbid("You are not authorized to delete this group.");
            }

            var group = await _context.Groups.Include(g => g.Members)
                                             .Include(g => g.Chats)
                                             .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
            if (group == null)
            {
                return NotFound(new { Message = "Group not found" });
            }

            var isAdmin = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == Guid.Parse(authenticatedUserId) && gm.IsAdmin, cancellationToken);
            if (!isAdmin)
            {
                return Forbid("You are not authorized to delete this group.");
            }

            _context.GroupMembers.RemoveRange(group.Members);
            _context.Chats.RemoveRange(group.Chats);
            _context.Groups.Remove(group);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Group {group.Name} deleted");

            return NoContent();
        }

        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetGroupMembers(Guid groupId, CancellationToken cancellationToken)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null)
            {
                return Forbid("You are not authorized to view the members of this group.");
            }

            var groupMembers = await _context.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Include(gm => gm.User)
                .ToListAsync(cancellationToken);

            if (!groupMembers.Any())
            {
                return NotFound(new { Message = "No members found in this group" });
            }

            var isMember = groupMembers.Any(gm => gm.UserId == Guid.Parse(authenticatedUserId));
            if (!isMember)
            {
                return Forbid("You are not authorized to view the members of this group.");
            }

            var membersDto = groupMembers.Select(gm => new UserDto
            {
                Id = gm.User.Id,
                Username = gm.User.Username,
                FirstName = gm.User.FirstName,
                LastName = gm.User.LastName,
                Birthday = gm.User.Birthday,
                Email = gm.User.Email,
                Avatar = gm.User.Avatar,
                Status = gm.User.Status
            }).ToList();

            return Ok(membersDto);
        }

        [HttpDelete]
        public async Task<IActionResult> RemoveMember([FromForm] RemoveGroupMemberDto request, CancellationToken cancellationToken)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null)
            {
                return Forbid("You are not authorized to remove members from this group.");
            }

            var groupMember = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && gm.UserId == request.UserId, cancellationToken);

            if (groupMember == null)
            {
                return NotFound(new { Message = "Group member not found" });
            }

            var isAdmin = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == Guid.Parse(authenticatedUserId) && gm.IsAdmin, cancellationToken);
            if (!isAdmin)
            {
                return Forbid("You are not authorized to remove members from this group.");
            }

            var wasAdmin = groupMember.IsAdmin;
            _context.GroupMembers.Remove(groupMember);
            await _context.SaveChangesAsync(cancellationToken);

            // Check if there are any admins left
            var anyAdminsLeft = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.IsAdmin, cancellationToken);
            if (!anyAdminsLeft)
            {
                // Promote a random member to admin if no admins are left
                var remainingMember = await _context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId, cancellationToken);
                if (remainingMember != null)
                {
                    remainingMember.IsAdmin = true;
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation($"User {remainingMember.UserId} promoted to admin in group {request.GroupId} as no admins were left.");
                }
            }

            _logger.LogInformation($"User {request.UserId} removed from group {request.GroupId}");

            return Ok(new { Message = "Member removed from the group" });
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserGroupsWithDetails(Guid userId, CancellationToken cancellationToken)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null || userId.ToString() != authenticatedUserId)
            {
                return Forbid("You are not authorized to view this user's groups.");
            }

            var userGroups = await _context.Groups
                .Where(g => g.Members.Any(gm => gm.UserId == userId))
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    Members = g.Members.Select(gm => new
                    {
                        gm.User.Id,
                        gm.User.Username,
                        gm.User.FirstName,
                        gm.User.LastName,
                        gm.User.Email,
                        gm.User.Avatar
                    }).ToList()
                })
                .ToListAsync(cancellationToken);

            return Ok(userGroups);
        }

        [HttpPut("rename")]
        public async Task<IActionResult> RenameGroup([FromForm] RenameGroupDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.NewName))
            {
                return BadRequest(new { Message = "New group name is required" });
            }

            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null)
            {
                return Forbid("You are not authorized to rename this group.");
            }

            var group = await _context.Groups.FindAsync(new object[] { request.GroupId }, cancellationToken);
            if (group == null)
            {
                return NotFound(new { Message = "Group not found" });
            }

            var isAdmin = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == Guid.Parse(authenticatedUserId) && gm.IsAdmin, cancellationToken);
            if (!isAdmin)
            {
                return Forbid("You are not authorized to rename this group.");
            }

            group.Name = request.NewName;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Group {request.GroupId} renamed to {request.NewName}");

            return Ok(new { Message = "Group name updated successfully", group.Id, group.Name });
        }


        [HttpPost("update-admin")]
        public async Task<IActionResult> UpdateGroupAdmin([FromForm] UpdateGroupAdminDto request, CancellationToken cancellationToken)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null)
            {
                return Forbid("You are not authorized to update group admin.");
            }

            var group = await _context.Groups.FindAsync(new object[] { request.GroupId }, cancellationToken);
            if (group == null)
            {
                return NotFound(new { Message = "Group not found" });
            }

            var isAdmin = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == Guid.Parse(authenticatedUserId) && gm.IsAdmin, cancellationToken);
            if (!isAdmin)
            {
                return Forbid("You are not authorized to update group admin.");
            }

            var groupMember = await _context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && gm.UserId == request.UserId, cancellationToken);
            if (groupMember == null)
            {
                return NotFound(new { Message = "Group member not found" });
            }

            groupMember.IsAdmin = true; // Tự động đặt IsAdmin thành true
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"User {request.UserId} admin status updated to true in group {request.GroupId}");

            return Ok(new { Message = "Admin status updated successfully" });
        }

        [HttpPost("revoke-admin")]
        public async Task<IActionResult> RevokeGroupAdmin([FromForm] RevokeGroupAdminDto request, CancellationToken cancellationToken)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null)
            {
                return Forbid("You are not authorized to revoke admin rights.");
            }

            var group = await _context.Groups.FindAsync(new object[] { request.GroupId }, cancellationToken);
            if (group == null)
            {
                return NotFound(new { Message = "Group not found" });
            }

            var isAdmin = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == Guid.Parse(authenticatedUserId) && gm.IsAdmin, cancellationToken);
            if (!isAdmin)
            {
                return Forbid("You are not authorized to revoke admin rights.");
            }

            var groupMember = await _context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && gm.UserId == request.UserId, cancellationToken);
            if (groupMember == null)
            {
                return NotFound(new { Message = "Group member not found" });
            }

            groupMember.IsAdmin = false;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Admin rights revoked for user {request.UserId} in group {request.GroupId}");

            // Check if there are any admins left
            var anyAdminsLeft = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.IsAdmin, cancellationToken);
            if (!anyAdminsLeft)
            {
                // Promote a random member to admin if no admins are left
                var remainingMember = await _context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId, cancellationToken);
                if (remainingMember != null)
                {
                    remainingMember.IsAdmin = true;
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation($"User {remainingMember.UserId} promoted to admin in group {request.GroupId} as no admins were left.");
                }
            }

            return Ok(new { Message = "Admin rights revoked successfully" });
        }
        [HttpPost("update-avatar")]
        public async Task<IActionResult> UpdateGroupAvatar([FromForm] UpdateAvatarGroupDto request, CancellationToken cancellationToken)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId == null)
            {
                return Forbid("You are not authorized to update this group avatar.");
            }

            var group = await _context.Groups.FindAsync(new object[] { request.GroupId }, cancellationToken);
            if (group == null)
            {
                return NotFound("Group not found");
            }

            var isAdmin = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == Guid.Parse(authenticatedUserId) && gm.IsAdmin, cancellationToken);
            if (!isAdmin)
            {
                return Forbid("You are not authorized to update this group avatar.");
            }

            if (request.AvatarFile != null)
            {
                var (savedFileName, originalFileName) = FileService.FileSaveToServer(request.AvatarFile, "wwwroot/avatar/");
                group.Avatar = Path.Combine("avatar", savedFileName).Replace("\\", "/");
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

    }

}
