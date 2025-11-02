using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using CourseWork.Models;
using CourseWork.Services;
using System.Text.Json;

namespace CourseWork.Hubs
{
    [Authorize]
    public class DiscussionHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly UserManager<User> _userManager;

        public DiscussionHub(AppDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task Join(string inventoryId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"inventory_{inventoryId}");
        }

        public async Task Leave(string inventoryId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"inventory_{inventoryId}");
        }

        public async Task SendMessage(string inventoryId, string message)
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user == null) return;

            var inventory = await _db.Inventories.FindAsync(int.Parse(inventoryId));
            if (inventory == null) return;

            var discussion = new InventoryDiscussion
            {
                InventoryId = int.Parse(inventoryId),
                UserId = user.Id,
                Message = message,
                CreatedAt = DateTime.UtcNow
            };

            _db.InventoryDiscussions.Add(discussion);
            await _db.SaveChangesAsync();

            var messageData = new
            {
                id = discussion.Id,
                user = user.UserName,
                message = message,
                time = discussion.CreatedAt
            };

            await Clients.Group($"inventory_{inventoryId}").SendAsync("ReceiveMessage", messageData);
        }
    }
}
