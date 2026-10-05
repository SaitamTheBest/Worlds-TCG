using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace web_tcg.Controllers
{
    /// <summary>
    /// Gère le stockage interne des illustrations des cartes.
    ///
    /// Les fichiers sont enregistrés dans :
    /// wwwroot/uploads/cards
    ///
    /// L'API retourne uniquement un chemin interne du type :
    /// /uploads/cards/xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.png
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("Admin/CardImages")]
    public sealed class CardImageController : Controller
    {
        private const long MaxFileSize = 8 * 1024 * 1024;

        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CardImageController> _logger;

        public CardImageController(
            IWebHostEnvironment environment,
            ILogger<CardImageController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        [HttpPost("Upload")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
        public Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
            => SaveImageAsync(file, cancellationToken, "cards");

        [HttpPost("/Admin/CardSets/UploadImage")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
        public Task<IActionResult> UploadCardSet(IFormFile? file, CancellationToken cancellationToken)
            => SaveImageAsync(file, cancellationToken, "cardsets");

        [HttpPost("/Admin/PoliticalParties/UploadImage")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
        public Task<IActionResult> UploadPoliticalParty(IFormFile? file, CancellationToken cancellationToken)
            => SaveImageAsync(file, cancellationToken, "parties");

        private async Task<IActionResult> SaveImageAsync(
            IFormFile? file, CancellationToken cancellationToken, string folder)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Aucun fichier n'a été envoyé."
                });
            }

            if (file.Length > MaxFileSize)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "L'image ne doit pas dépasser 8 Mo."
                });
            }

            var detectedExtension =
                await DetectImageExtensionAsync(
                    file,
                    cancellationToken);

            if (detectedExtension == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Format non autorisé. Utilisez une image PNG, JPG ou WEBP."
                });
            }

            var webRootPath =
                _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "Le dossier wwwroot n'est pas disponible."
                    });
            }

            var uploadDirectory =
                Path.Combine(
                    webRootPath,
                    "uploads",
                    folder);

            var fileName =
                $"{Guid.NewGuid():N}{detectedExtension}";

            var absolutePath =
                Path.Combine(
                    uploadDirectory,
                    fileName);

            try
            {
                Directory.CreateDirectory(uploadDirectory);

                await using var outputStream =
                    new FileStream(
                        absolutePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        useAsync: true);

                await file.CopyToAsync(
                    outputStream,
                    cancellationToken);

                var publicPath =
                    $"/uploads/{folder}/{fileName}";

                _logger.LogInformation(
                    "Illustration de carte enregistrée : {Path}",
                    publicPath);

                return Ok(new
                {
                    success = true,
                    path = publicPath
                });
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(
                    absolutePath);

                throw;
            }
            catch (Exception ex)
            {
                TryDeleteFile(
                    absolutePath);

                _logger.LogError(
                    ex,
                    "Erreur pendant l'enregistrement de l'illustration {FileName}.",
                    file.FileName);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "Impossible d'enregistrer l'image."
                    });
            }
        }

        /// <summary>
        /// Détermine le vrai format du fichier à partir de sa signature binaire.
        /// On ne fait pas confiance uniquement au nom ou au Content-Type envoyé
        /// par le navigateur.
        /// </summary>
        private static async Task<string?> DetectImageExtensionAsync(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var header =
                new byte[12];

            await using var input =
                file.OpenReadStream();

            var read =
                await input.ReadAsync(
                    header.AsMemory(0, header.Length),
                    cancellationToken);

            // PNG
            if (read >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A)
            {
                return ".png";
            }

            // JPEG
            if (read >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF)
            {
                return ".jpg";
            }

            // WEBP : RIFF....WEBP
            if (read >= 12 &&
                header[0] == 0x52 &&
                header[1] == 0x49 &&
                header[2] == 0x46 &&
                header[3] == 0x46 &&
                header[8] == 0x57 &&
                header[9] == 0x45 &&
                header[10] == 0x42 &&
                header[11] == 0x50)
            {
                return ".webp";
            }

            return null;
        }

        private static void TryDeleteFile(
            string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch
            {
                // Le nettoyage ne doit pas masquer l'erreur principale.
            }
        }
    }
}
