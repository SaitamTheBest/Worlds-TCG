using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Models.Friends;
using web_tcg.Services;
namespace web_tcg.Controllers;

[Authorize]
[Route("Friends")]
public sealed class FriendsController(ApplicationDbContext db,FriendsService friends,CardPresentationService presentation) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet("")]
    public async Task<IActionResult> Index(string search="") {
        search=(search??"").Trim(); if(search.Length>100) search=search[..100];
        var results=new List<ApplicationUser>();
        if(search.Length>0) {var term=search.ToUpperInvariant(); results=await db.Users.AsNoTracking()
            .Where(x=>!x.IsBot&&x.Id!=UserId&&x.NormalizedUserName!=null&&x.NormalizedUserName.Contains(term))
            .OrderBy(x=>x.UserName).Take(30).ToListAsync();}
        return View(new FriendsIndexViewModel { UserId=UserId,Search=search,Results=results,
            Relations=await db.Friendships.AsNoTracking().Include(x=>x.FirstUser).Include(x=>x.SecondUser)
                .Where(x=>(x.FirstUserId==UserId||x.SecondUserId==UserId)&&x.Status!=FriendshipStatus.Declined)
                .OrderByDescending(x=>x.CreatedAt).ToListAsync() });
    }
    [HttpPost("Request"),ValidateAntiForgeryToken]
    public async Task<IActionResult> SendInvitation(string targetId) {
        try {await friends.RequestAsync(UserId,targetId); TempData["FriendsSuccess"]="Invitation envoyée.";}
        catch(FriendRuleException ex) {TempData["FriendsError"]=ex.Message;}
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("Invitation/{id:guid}"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Invitation(Guid id,bool accept) {
        try {await friends.RespondAsync(UserId,id,accept); TempData["FriendsSuccess"]=accept?"Invitation acceptée.":"Invitation refusée.";}
        catch(FriendRuleException ex) {TempData["FriendsError"]=ex.Message;}
        return RedirectToAction(nameof(Index));
    }
    [HttpGet("Conversation/{id:guid}")]
    public async Task<IActionResult> Conversation(Guid id) {
        try {return View(await ConversationModelAsync(id));} catch(FriendRuleException) {return NotFound();}
    }
    private async Task<FriendConversationViewModel> ConversationModelAsync(Guid id) => new() {
        UserId=UserId,Friendship=await friends.FriendshipAsync(id,UserId),
        Messages=await db.FriendMessages.AsNoTracking().Where(x=>x.FriendshipId==id).OrderByDescending(x=>x.Id).Take(100).OrderBy(x=>x.Id).ToListAsync(),
        Trades=await db.FriendTrades.AsNoTracking().Where(x=>x.FriendshipId==id).OrderByDescending(x=>x.CreatedAt).Take(30).ToListAsync()
    };
    [HttpGet("Conversation/{id:guid}/Messages")]
    public async Task<IActionResult> Messages(Guid id) {
        try {return PartialView("_Messages",await ConversationModelAsync(id));} catch(FriendRuleException) {return NotFound();}
    }
    [HttpPost("Conversation/{id:guid}/Message"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Message(Guid id,string text) {
        try {await friends.MessageAsync(UserId,id,text); return Ok();} catch(FriendRuleException ex) {return BadRequest(new {message=ex.Message});}
    }
    [HttpGet("Trade/Create/{id:guid}")]
    public async Task<IActionResult> CreateTrade(Guid id,string search="") {
        try {return View("CreateTrade",await TradeCreateModelAsync(new() {FriendshipId=id},search));} catch(FriendRuleException) {return NotFound();}
    }
    private async Task<FriendTradeCreateViewModel> TradeCreateModelAsync(FriendTradeInput input,string search="") {
        var relation=await friends.FriendshipAsync(input.FriendshipId,UserId);
        var friend=relation.FirstUserId==UserId?relation.SecondUser:relation.FirstUser;
        search=(search??"").Trim(); if(search.Length>100) search=search[..100];
        var query=db.UserCardCollections.AsNoTracking().Where(x=>(x.UserId==UserId||x.UserId==friend.Id)&&x.Quantity>0);
        if(search.Length>0) {var lower=search.ToLower();query=query.Where(x=>x.Card.Name.ToLower().Contains(lower));}
        var rows=await query.OrderBy(x=>x.Card.Name).ThenBy(x=>x.CardId).Take(1000).Select(x=>new {x.UserId,x.CardId,x.Quantity}).ToListAsync();
        var cards=(await presentation.GetAsync(rows.Select(x=>x.CardId))).ToDictionary(x=>x.Id);
        return new() {Input=input,Friend=friend,Balance=await db.Users.Where(x=>x.Id==UserId).Select(x=>x.CardBalance).SingleAsync(),
            OfferedCards=rows.Where(x=>x.UserId==UserId).Select(x=>(cards[x.CardId],x.Quantity)).ToList(),
            RequestedCards=rows.Where(x=>x.UserId==friend.Id).Select(x=>(cards[x.CardId],x.Quantity)).ToList()};
    }
    [HttpPost("Trade/Create"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Propose(FriendTradeInput input) {
        if(ModelState.IsValid) try {return RedirectToAction(nameof(Trade),new {id=await friends.ProposeAsync(UserId,input)});} catch(FriendRuleException ex) {ModelState.AddModelError("",ex.Message);}
        try {return View("CreateTrade",await TradeCreateModelAsync(input));} catch(FriendRuleException) {return NotFound();}
    }
    [HttpGet("Trade/{id:guid}")]
    public async Task<IActionResult> Trade(Guid id) {
        var trade=await db.FriendTrades.AsNoTracking().Include(x=>x.Cards).FirstOrDefaultAsync(x=>x.Id==id&&(x.ProposerId==UserId||x.RecipientId==UserId));
        if(trade==null) return NotFound();
        var names=await db.Users.Where(x=>x.Id==trade.ProposerId||x.Id==trade.RecipientId).ToDictionaryAsync(x=>x.Id,x=>x.UserName);
        var cards=(await presentation.GetAsync(trade.Cards.Select(x=>x.CardId))).ToDictionary(x=>x.Id);
        return View(new FriendTradeViewModel {UserId=UserId,Trade=trade,ProposerName=names[trade.ProposerId]??"Joueur",RecipientName=names[trade.RecipientId]??"Joueur",
            Cards=trade.Cards.Select(x=>(cards[x.CardId],x.Quantity,x.IsOffered)).ToList()});
    }
    [HttpPost("Trade/{id:guid}/Resolve"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id,string action) {
        try {await friends.ResolveAsync(UserId,id,action);TempData["FriendsSuccess"]=action=="accept"?"Échange confirmé. Les biens ont été transférés.":"Proposition terminée sans transfert.";}
        catch(FriendRuleException ex) {TempData["FriendsError"]=ex.Message;}
        return RedirectToAction(nameof(Trade),new {id});
    }
}
