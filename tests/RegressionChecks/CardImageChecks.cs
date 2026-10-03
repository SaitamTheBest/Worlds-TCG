using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using web_tcg.Controllers;
using web_tcg.Data.Enums;
using web_tcg.Models.Admin;

public static class CardImageChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "tcg-image-check-" + Guid.NewGuid().ToString("N"));
        var environment = new ImageEnvironment { WebRootPath = Path.Combine(root,"wwwroot") };
        var controller = new CardImageController(environment,NullLogger<CardImageController>.Instance);
        try
        {
            // A real PNG, not just a filename or declared MIME type.
            var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=");
            using var stream = new MemoryStream(bytes);
            var result = await controller.Upload(new FormFile(stream,0,bytes.Length,"file","illustration.png"),CancellationToken.None);
            var ok = result as OkObjectResult;
            var path = ok?.Value?.GetType().GetProperty("path")?.GetValue(ok.Value) as string;
            check(path != null && path.StartsWith("/uploads/cards/"), "images : l'envoi retourne le chemin interne enregistré");
            var diskPath = Path.Combine(environment.WebRootPath,path!.TrimStart('/').Replace('/',Path.DirectorySeparatorChar));
            check(File.Exists(diskPath) && (await File.ReadAllBytesAsync(diskPath)).SequenceEqual(bytes),
                "images : création du dossier wwwroot/uploads/cards et copie intégrale du fichier");
            var variant = new CreateCardVariantViewModel { ArtType = CardArtType.AlternateArt, ImageUrl = path };
            check(Validator.TryValidateObject(variant,new ValidationContext(variant),new List<ValidationResult>(),true),
                "images : une variante accepte le chemin interne retourné par l'envoi");
            var main = new CreateCardViewModel { MainImageUrl = path };
            check(Validator.TryValidateProperty(main.MainImageUrl,new ValidationContext(main) { MemberName = nameof(main.MainImageUrl) },new List<ValidationResult>()),
                "images : illustration principale et variante acceptent le même chemin");
            foreach (var invalid in new[] { "https://example.com/image.png", "/uploads/cards/../../image.png", "blob:image", "/uploads/cards/" + new string('a',32) + ".svg" })
            {
                variant.ImageUrl = invalid;
                check(!Validator.TryValidateObject(variant,new ValidationContext(variant),new List<ValidationResult>(),true),
                    "images : chemin non enregistré refusé : " + invalid);
            }
            using var invalidStream = new MemoryStream(new byte[] {1,2,3,4});
            check(await controller.Upload(new FormFile(invalidStream,0,4,"file","fake.png"),CancellationToken.None) is BadRequestObjectResult,
                "images : un fichier sans signature d'image est refusé");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root,true);
        }
    }

    private sealed class ImageEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "RegressionChecks";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
