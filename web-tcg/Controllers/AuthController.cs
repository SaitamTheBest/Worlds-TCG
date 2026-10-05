using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using web_tcg.Data.Entities;

namespace web_tcg.Controllers
{
    [Route("Auth")]
    public class AuthController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<AuthController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        // ============================================================
        // GET /Auth
        // Affiche la page d'authentification
        // ============================================================

        [HttpGet("")]
        public IActionResult Index(string? mode = "login", string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(
                    GetSafeReturnUrl(returnUrl));
            }

            ViewBag.Mode =
                mode == "register"
                    ? "register"
                    : "login";

            ViewBag.ReturnUrl = returnUrl;

            return View();
        }

        // ============================================================
        // POST /Auth/Login
        // ============================================================

        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            if (request == null)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Requête invalide."
                });
            }

            // --------------------------------------------------------
            // Validation
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Veuillez entrer votre nom d'utilisateur ou votre email."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Veuillez entrer votre mot de passe."
                });
            }

            try
            {
                ApplicationUser? user;

                // ----------------------------------------------------
                // Recherche par email ou username
                // ----------------------------------------------------

                if (request.Username.Contains('@'))
                {
                    user = await _userManager.FindByEmailAsync(
                        request.Username.Trim());
                }
                else
                {
                    user = await _userManager.FindByNameAsync(
                        request.Username.Trim());
                }

                // On ne révèle pas si le compte existe.
                if (user == null)
                {
                    _logger.LogWarning(
                        "Échec de connexion : compte inexistant pour {Username}",
                        request.Username);

                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Identifiants incorrects."
                    });
                }

                // ----------------------------------------------------
                // Vérification du mot de passe
                // ----------------------------------------------------

                var result = await _signInManager.CheckPasswordSignInAsync(
                    user,
                    request.Password,
                    lockoutOnFailure: true);

                if (result.IsLockedOut)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Votre compte est temporairement verrouillé."
                    });
                }

                if (result.IsNotAllowed)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "La connexion n'est pas autorisée pour ce compte."
                    });
                }

                if (!result.Succeeded)
                {
                    _logger.LogWarning(
                        "Mot de passe incorrect pour {Username}",
                        user.UserName);

                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Identifiants incorrects."
                    });
                }

                // ----------------------------------------------------
                // Création du cookie Identity
                // ----------------------------------------------------

                await _signInManager.SignInAsync(
                    user,
                    request.RememberMe);

                _logger.LogInformation(
                    "Connexion réussie pour {Username}",
                    user.UserName);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Connexion réussie.",
                    RedirectUrl = GetSafeReturnUrl(request.ReturnUrl)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur pendant la connexion de {Username}",
                    request.Username);

                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Une erreur est survenue lors de la connexion."
                });
            }
        }

        // ============================================================
        // POST /Auth/Register
        // ============================================================

        [HttpPost("Register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            if (request == null)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Requête invalide."
                });
            }

            var errors = new List<string>();

            // --------------------------------------------------------
            // Validation
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(request.Username))
            {
                errors.Add("Le nom d'utilisateur est requis.");
            }
            else
            {
                request.Username = request.Username.Trim();

                if (request.Username.Length < 3)
                {
                    errors.Add(
                        "Le nom d'utilisateur doit contenir au moins 3 caractères.");
                }

                if (request.Username.Length > 30)
                {
                    errors.Add(
                        "Le nom d'utilisateur ne peut pas dépasser 30 caractères.");
                }
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                errors.Add("L'adresse email est requise.");
            }
            else
            {
                request.Email = request.Email.Trim().ToLowerInvariant();
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                errors.Add("Le mot de passe est requis.");
            }

            if (request.Password != request.ConfirmPassword)
            {
                errors.Add("Les mots de passe ne correspondent pas.");
            }

            if (errors.Count > 0)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Veuillez corriger les erreurs.",
                    Errors = errors
                });
            }

            try
            {
                // ----------------------------------------------------
                // Vérification username
                // ----------------------------------------------------

                var existingUser =
                    await _userManager.FindByNameAsync(request.Username);

                if (existingUser != null)
                {
                    errors.Add(
                        "Ce nom d'utilisateur est déjà utilisé.");
                }

                // ----------------------------------------------------
                // Vérification email
                // ----------------------------------------------------

                var existingEmail =
                    await _userManager.FindByEmailAsync(request.Email);

                if (existingEmail != null)
                {
                    errors.Add(
                        "Cette adresse email est déjà utilisée.");
                }

                if (errors.Count > 0)
                {
                    return Conflict(new AuthResponse
                    {
                        Success = false,
                        Message = "Impossible de créer le compte.",
                        Errors = errors
                    });
                }

                // ----------------------------------------------------
                // Création du compte
                // ----------------------------------------------------

                var user = new ApplicationUser
                {
                    UserName = request.Username,
                    Email = request.Email,
                    EmailConfirmed = false,
                    Balance = 5000
                };

                var result = await _userManager.CreateAsync(
                    user,
                    request.Password);

                // ----------------------------------------------------
                // Erreurs Identity
                // ----------------------------------------------------

                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        errors.Add(error.Description);

                        _logger.LogWarning(
                            "Erreur Identity pour {Username}: {Error}",
                            request.Username,
                            error.Description);
                    }

                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Impossible de créer le compte.",
                        Errors = errors
                    });
                }

                _logger.LogInformation(
                    "Compte créé : {Username} ({Email})",
                    request.Username,
                    request.Email);

                // ----------------------------------------------------
                // Connexion automatique
                // ----------------------------------------------------

                await _signInManager.SignInAsync(
                    user,
                    isPersistent: false);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Compte créé avec succès ! Bienvenue !",
                    RedirectUrl = GetSafeReturnUrl(request.ReturnUrl)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur pendant l'inscription de {Username}",
                    request.Username);

                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Une erreur est survenue lors de la création du compte."
                });
            }
        }

        // ============================================================
        // POST /Auth/Logout
        // ============================================================

        [HttpPost("Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var username = User.Identity?.Name;

                await _signInManager.SignOutAsync();

                _logger.LogInformation(
                    "Utilisateur {Username} déconnecté.",
                    username);

                return RedirectToAction(
                    nameof(Index),
                    new { mode = "login" });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur pendant la déconnexion.");

                return RedirectToAction(
                    nameof(Index),
                    new { mode = "login" });
            }
        }

        // ============================================================
        // Return URL
        // ============================================================

        private string GetSafeReturnUrl(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl)
                && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            return "/";
        }
    }

    // ================================================================
    // REQUESTS
    // ================================================================

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    // ================================================================
    // RESPONSE
    // ================================================================

    public class AuthResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public List<string> Errors { get; set; } = new();

        public string? RedirectUrl { get; set; }
    }
}