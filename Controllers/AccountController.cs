using Microsoft.AspNetCore.Mvc;

using ImageSharingWithCloud.DAL;
using ImageSharingWithCloud.Models;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ImageSharingWithCloud.Controllers
{
    // TODO require authorization
    [Authorize]
    public class AccountController : BaseController
    {
        protected SignInManager<ApplicationUser> signInManager;

        private readonly ILogger<AccountController> logger;

        // Dependency injection of DB context and user/signin managers
        public AccountController(UserManager<ApplicationUser> userManager, 
                                 SignInManager<ApplicationUser> signInManager, 
                                 ApplicationDbContext db,
                                 IImageStorage imageStorage,
                                 ILogger<AccountController> logger) 
            : base(userManager, imageStorage, db)
        {
            this.signInManager = signInManager;
            this.logger = logger;
        }

        // TODO allow anonymous
        [AllowAnonymous]
        public ActionResult Register()
        {
            CheckAda();
            return View();
        }

        // TODO allow anonymous, prevent CSRF
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterModel model)
        {
            CheckAda();

            if (ModelState.IsValid)
            {
                logger.LogDebug("Registering user: " + model.Email);
                // Register the user from the model, and log them in

                var user = new ApplicationUser(model.Email, "true".Equals(model.ADA));
                var result = await userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    logger.LogDebug("...registration succeeded.");
                    result = await userManager.AddToRoleAsync(user, "User");
                    await signInManager.SignInAsync(user, false);
                    return RedirectToAction("Index", "Home", new { UserName = model.Email });
                }
                else
                {
                    logger.LogDebug("...registration failed.");
                    ModelState.AddModelError(string.Empty, "Registration failed");
                }

            }

            // If we got this far, something failed, redisplay form
            return View(model);

        }

        // TODO allow anonymous
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl)
        {
            CheckAda();
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // TODO allow anonymous, prevent CSRF
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel model, string returnUrl)
        {
            CheckAda();
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            logger.LogDebug("Logging in user " + model.UserName);

            /*
             * Log in the user from the model (make sure they are still active)
             */

            ApplicationUser User = null;
            // TODO Use UserManager to obtain the user record from the database.
            User = await userManager.FindByNameAsync(model.UserName);
            if (User != null && User.Active)
            {
                SignInResult result = null;
                // TODO Use SignInManager to log in the user.
                result = await signInManager.PasswordSignInAsync(User, model.Password, false, false);
                if (result.Succeeded)
                {
                    SaveADACookie(User.ADA);
                    logger.LogDebug("Successful login, redirecting to " + returnUrl);
                    return Redirect(returnUrl ?? "/");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Login failed");
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "No such user");
            }

            return View(model);
        }

        // TODO
        [Authorize]
        public ActionResult Password(PasswordMessageId? message)
        {
            CheckAda();
            ViewBag.StatusMessage =
                 message == PasswordMessageId.ChangePasswordSuccess ? "Your password has been changed."
                 : message == PasswordMessageId.SetPasswordSuccess ? "Your password has been set."
                 : message == PasswordMessageId.RemoveLoginSuccess ? "The external login was removed."
                 : "";
            ViewBag.ReturnUrl = Url.Action("Password");
            return View();
        }

        // TODO prevent CSRF
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Password(LocalPasswordModel model)
        {
            CheckAda();

            ViewBag.ReturnUrl = Url.Action("Password");
            if (ModelState.IsValid)
            {
                /*
                 * Change the password
                 */
                ApplicationUser user = await GetLoggedInUser();
                //string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                //idResult = await userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
                await userManager.RemovePasswordAsync(user);
                IdentityResult idResult = await userManager.AddPasswordAsync(user, model.NewPassword);

                if (idResult.Succeeded)
                {
                    return RedirectToAction("Password", new { Message = PasswordMessageId.ChangePasswordSuccess });
                }
                else
                {
                    ModelState.AddModelError("", "The new password is invalid.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        // TODO require Admin permission
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Manage()
        {
            CheckAda();

            List<SelectListItem> users = new List<SelectListItem>();
            foreach (var u in db.Users)
            {
                SelectListItem item = new SelectListItem { Text = u.UserName, Value = u.Id, Selected = u.Active };
                users.Add(item);
            }

            ViewBag.message = "";
            ManageModel model = new ManageModel { Users = users };
            return View(model);
        }

        // TODO require Admin permission, prevent CSRF
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(ManageModel model)
        {
            CheckAda();

            foreach (var userItem in model.Users)
            {
                ApplicationUser user = await userManager.FindByIdAsync(userItem.Value);

                userItem.Text = user.UserName;


                if (user.Active && !userItem.Selected)
                {
                    await imageStorage.RemoveImagesAsync(user);
                    user.Active = false;
                }
                else if (!user.Active && userItem.Selected)
                {
                    /*
                     * Reactivate a user
                     */
                    user.Active = true;
                }
            }
            await db.SaveChangesAsync();

            ViewBag.message = "Users successfully deactivated/reactivated";

            return View(model);
        }

        // TODO
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            CheckAda();

            ApplicationUser user = await GetLoggedInUser();
            return View(new LogoutModel { UserName = user.UserName });
        }

        // TODO
        [HttpPost]
        public async Task<IActionResult> DoLogout()
        {
            CheckAda();

            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // TODO
        [HttpGet]
        public IActionResult AccessDenied(string returnUrl)
        {
            CheckAda();

            return View(new AccessDeniedModel
            {
                ReturnUrl = returnUrl
            });
        }


        protected void SaveADACookie(bool value)
        {
            // Save the value in a cookie field key
            var options = new CookieOptions()
            {
                IsEssential = true,
                Expires = DateTime.Now.AddMonths(3)
            };
            Response.Cookies.Append("ADA", value ? "true" : "false", options);
        }

        public enum PasswordMessageId
        {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
        }

    }
}
