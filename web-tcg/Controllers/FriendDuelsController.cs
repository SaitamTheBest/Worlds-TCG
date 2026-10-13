using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Game.Engine;
using web_tcg.Models.Friends;
using web_tcg.Models.Home;
using web_tcg.Services;

namespace web_tcg.Controllers;

[Authorize,Route("Friends/Duel")]
public sealed class FriendDuelsController(ApplicationDbContext db,FriendDuelService duels,FriendsService friends) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private async Task<FriendDuelViewModel> ModelAsync(Guid friendshipId,FriendDuelInvitation? invitation=null) {
        var relation=await friends.FriendshipAsync(friendshipId,UserId);
        var other=relation.FirstUserId==UserId?relation.SecondUser:relation.FirstUser;
        return new() { UserId=UserId,FriendshipId=friendshipId,FriendName=other.UserName??"Votre ami",Invitation=invitation,
            Decks=await db.Set<Deck>().AsNoTracking().Where(x=>x.UserId==UserId).OrderBy(x=>x.Name)
                .Select(x=>new HomeDeckOption {Id=x.Id,Name=x.Name}).ToListAsync() };
    }
    [HttpGet("Create/{id:guid}")]
    public async Task<IActionResult> Create(Guid id) {
        try {return View("Duel",await ModelAsync(id));} catch(FriendRuleException) {return NotFound();}
    }
    [HttpPost("Create/{id:guid}"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(Guid id,Guid deckId) {
        try { return RedirectToAction(nameof(Duel),new {id=await duels.InviteAsync(UserId,id,deckId)}); }
        catch(Exception ex) when (ex is FriendRuleException or GameRuleException) {
            ModelState.AddModelError("",ex.Message);
            try {return View("Duel",await ModelAsync(id));} catch(FriendRuleException) {return NotFound();}
        }
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Duel(Guid id) {
        try {
            var invitation=await duels.GetAsync(id,UserId);
            if(invitation.Status==FriendDuelStatus.Started) return Redirect($"/Game/Play/{invitation.MatchId}");
            return View(await ModelAsync(invitation.FriendshipId,invitation));
        } catch(FriendRuleException) {return NotFound();}
    }
    [HttpGet("{id:guid}/Status"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
    public async Task<IActionResult> Status(Guid id) {
        try {var invitation=await duels.GetAsync(id,UserId);return Json(new {status=(int)invitation.Status,invitation.ExpiresAt,invitation.MatchId,serverTime=DateTime.UtcNow});}
        catch(FriendRuleException) {return NotFound();}
    }
    [HttpPost("{id:guid}/Resolve"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id,string action,Guid? deckId=null) {
        try {await duels.ResolveAsync(UserId,id,action,deckId);}
        catch(Exception ex) when(ex is FriendRuleException or GameRuleException) {TempData["FriendsError"]=ex.Message;}
        return RedirectToAction(nameof(Duel),new {id});
    }
}
