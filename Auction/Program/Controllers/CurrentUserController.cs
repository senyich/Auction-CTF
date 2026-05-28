using Application.Logic.User;
using Application.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Program.ViewModels;
using System.Security.Claims;
using Application.Logic.Item;
using Program.ViewModels.Dto;
using Application.Logic.Lot;
using Auction;
using Application.Common;

namespace Program.Controllers
{
    [Route("[controller]/[action]")]
    public class CurrentUserController(IMediator mdtr, 
        LotsManagementAndPaymentService paymentService) : Controller
    {
        [Authorize]
        [HttpGet]
        [Route("/user")]
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.User.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized("user data error");
            var userResult = await mdtr.Send(new GetUserByIdQuery(userId));
            if (userResult.Failed)
                return RedirectToAction("logout", "login");
            var user = userResult.Data!;
            ViewBag.SelectedPageName = "Account";
            return View(new CurrentUserViewModel(user.Username, user.Name, user.RegisterDate, user.Currencies));
        }
        [Authorize(Policy = StaticAttributes.HasLinkedAccountPolicyName)]
        [HttpGet]
        public async Task<IActionResult> Items()
        {
            var userId = HttpContext.User.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized("user data error");
            var userResult = await mdtr.Send(new GetUserByIdQuery(userId));
            if (userResult.Failed)
                return RedirectToAction("logout", "login");
            var user = userResult.Data!;
            ViewBag.SelectedPageName = "Account";
            var dataServerItems = await paymentService.UpdateItemsAndLots(user.Id);
            if (dataServerItems.Failed)
            {
                TempData["ErrorMessage"] = "Could not load the items";
            }

            var itemsTasks = (await mdtr.Send(new GetAllItemsQuery(x => x.Owner.Id == user.Id)))
                .Select(async x=> {
                    var lot = (await mdtr.Send(new GetAllLotsQuery(l => l.ItemInfo.Id == x.Id))).FirstOrDefault();
                    return new CurrentUserItemDto(x.Name, x.Poster, x.Type.ToString(),
                        x.Id, (lot is null?null:((await paymentService.CheckLotCompleted(lot.Id) ? null : lot.Id))));
                });
            return View(new CurrentUserItemsViewModel(
                (await Task.WhenAll(itemsTasks)).ToList()));
        }
    }
}
