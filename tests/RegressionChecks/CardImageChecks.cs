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
            using var boosterStream = new MemoryStream(bytes);
            var boosterResult = await controller.UploadCardSet(new FormFile(boosterStream,0,bytes.Length,"file","booster.png"),CancellationToken.None) as OkObjectResult;
            var boosterPath = boosterResult?.Value?.GetType().GetProperty("path")?.GetValue(boosterResult.Value) as string;
            check(boosterPath?.StartsWith("/uploads/cardsets/") == true, "extensions : image enregistrée dans le dossier dédié");
            var boosterDiskPath = Path.Combine(environment.WebRootPath,boosterPath!.TrimStart('/').Replace('/',Path.DirectorySeparatorChar));
            check(File.Exists(boosterDiskPath) && (await File.ReadAllBytesAsync(boosterDiskPath)).SequenceEqual(bytes), "extensions : fichier image copié intégralement");
            using var partyStream = new MemoryStream(bytes);
            var partyResult = await controller.UploadPoliticalParty(new FormFile(partyStream,0,bytes.Length,"file","logo.png"),CancellationToken.None) as OkObjectResult;
            var partyPath = partyResult?.Value?.GetType().GetProperty("path")?.GetValue(partyResult.Value) as string;
            check(partyPath?.StartsWith("/uploads/parties/") == true && File.ReadAllBytes(Path.Combine(environment.WebRootPath,partyPath.TrimStart('/'))).SequenceEqual(bytes),
                "partis : logo PNG enregistré sans modifier ses pixels ou sa transparence");
            var partyModel = new PoliticalPartyViewModel { ImageUrl = partyPath! };
            check(Validator.TryValidateProperty(partyModel.ImageUrl,new ValidationContext(partyModel) { MemberName = nameof(partyModel.ImageUrl) },new List<ValidationResult>()),
                "partis : le formulaire accepte le chemin du logo téléversé");
            var set = new CreateCardSetViewModel { BoosterImageUrl=boosterPath };
            bool ValidBoosterPath() => Validator.TryValidateProperty(set.BoosterImageUrl,new ValidationContext(set) { MemberName=nameof(set.BoosterImageUrl) },new List<ValidationResult>());
            check(ValidBoosterPath(), "extensions : le chemin interne retourné est accepté par le formulaire");
            foreach (var rejected in new[] { "https://example.com/booster.png", "/uploads/cardsets/../../booster.png", "/uploads/cards/"+new string('a',32)+".png", "" })
            {
                set.BoosterImageUrl=rejected;
                check(!ValidBoosterPath(), "extensions : URL externe, chemin détourné ou image absente refusés");
            }
            using var invalidBoosterStream = new MemoryStream(new byte[] {1,2,3,4});
            check(await controller.UploadCardSet(new FormFile(invalidBoosterStream,0,4,"file","fake.png"),CancellationToken.None) is BadRequestObjectResult, "extensions : faux fichier image refusé");
            check(await controller.UploadCardSet(null,CancellationToken.None) is BadRequestObjectResult, "extensions : envoi vide refusé");
            using var largeStream = new MemoryStream();
            check(await controller.UploadCardSet(new FormFile(largeStream,0,8*1024*1024+1,"file","big.png"),CancellationToken.None) is BadRequestObjectResult, "extensions : limite de 8 Mo appliquée");
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
