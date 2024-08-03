﻿using ChatAppServer.WebAPI.Dtos;
using ChatAppServer.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatAppServer.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public sealed class GroupsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GroupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromForm] CreateGroupDto request, CancellationToken cancellationToken)
        {
            var group = new Group
            {
                Name = request.Name
            };

            await _context.Groups.AddAsync(group, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var userId in request.MemberIds)
            {
                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user == null)
                {
                    return BadRequest(new { Message = $"User with Id {userId} not found" });
                }

                var groupMember = new GroupMember
                {
                    GroupId = group.Id,
                    UserId = userId
                };

                await _context.GroupMembers.AddAsync(groupMember, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(group);
        }


        [HttpPost]
        public async Task<IActionResult> AddMember([FromForm] AddGroupMemberDto request, CancellationToken cancellationToken)
        {
            var group = await _context.Groups.FindAsync(new object[] { request.GroupId }, cancellationToken);
            if (group == null)
            {
                return NotFound("Group not found");
            }

            var user = await _context.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
            if (user == null)
            {
                return NotFound("User not found");
            }

            var isMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == request.UserId, cancellationToken);
            if (isMember)
            {
                return BadRequest("User is already a member of the group");
            }

            var groupMember = new GroupMember
            {
                GroupId = request.GroupId,
                UserId = request.UserId
            };

            await _context.GroupMembers.AddAsync(groupMember, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(groupMember);
        }

        [HttpDelete("{groupId}")]
        public async Task<IActionResult> DeleteGroup(Guid groupId, CancellationToken cancellationToken)
        {
            var group = await _context.Groups.Include(g => g.Members)
                                             .Include(g => g.Chats)
                                             .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
            if (group == null)
            {
                return NotFound("Group not found");
            }

            // Xóa tất cả các thành viên của nhóm
            _context.GroupMembers.RemoveRange(group.Members);

            // Xóa tất cả các tin nhắn trong nhóm
            _context.Chats.RemoveRange(group.Chats);

            // Xóa nhóm
            _context.Groups.Remove(group);

            await _context.SaveChangesAsync(cancellationToken);

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupMembers(Guid groupId, CancellationToken cancellationToken)
        {
            var groupMembers = await _context.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Include(gm => gm.User)
                .ToListAsync(cancellationToken);

            if (!groupMembers.Any())
            {
                return NotFound("No members found in this group");
            }

            var membersDto = groupMembers.Select(gm => new UserDto
            {
                Id = gm.User.Id,
                Username = gm.User.Username,
                FullName = gm.User.FullName,
                Birthday = gm.User.Birthday,
                Email = gm.User.Email,
                Avatar = gm.User.Avatar,
                Status = gm.User.Status
            }).ToList();

            return Ok(membersDto);
        }

        [HttpDelete]
        public async Task<IActionResult> RemoveMember([FromBody] RemoveGroupMemberDto request, CancellationToken cancellationToken)
        {
            var groupMember = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && gm.UserId == request.UserId, cancellationToken);

            if (groupMember == null)
            {
                return NotFound("Group member not found");
            }

            _context.GroupMembers.Remove(groupMember);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok("Member removed from the group");
        }
        [HttpGet]
        public async Task<IActionResult> GetUserGroups(Guid userId, CancellationToken cancellationToken)
        {
            var userGroups = await _context.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Select(gm => gm.Group)
                .ToListAsync(cancellationToken);

            return Ok(userGroups);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserGroupsWithDetails(Guid userId, CancellationToken cancellationToken)
        {
            var userGroups = await _context.Groups
                .Where(g => g.Members.Any(gm => gm.UserId == userId))
                .Select(g => new
                {
                    Group = g,
                    Members = g.Members.Select(gm => gm.User).ToList(),
                    RecentChats = g.Chats.OrderByDescending(c => c.Date).Take(10).ToList() // lấy 10 tin nhắn gần nhất
                })
                .ToListAsync(cancellationToken);

            return Ok(userGroups);
        }

    }

}
