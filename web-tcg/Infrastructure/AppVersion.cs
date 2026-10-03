using System.Diagnostics;

namespace web_tcg.Infrastructure
{
    /// <summary>
    /// Fournit la version affichée de Worlds TCG.
    ///
    /// Ordre de priorité :
    /// 1. Variable d'environnement APP_VERSION.
    /// 2. Tag Git le plus récent du dépôt.
    /// 3. v0.1 si aucun tag n'est disponible.
    /// </summary>
    public static class AppVersion
    {
        private const string FallbackVersion = "v0.1";

        private static readonly Lazy<string> CurrentVersion =
            new(ResolveVersion);

        /// <summary>
        /// Version courante de l'application.
        /// </summary>
        public static string Current => CurrentVersion.Value;

        private static string ResolveVersion()
        {
            var environmentVersion =
                Environment.GetEnvironmentVariable("APP_VERSION");

            if (!string.IsNullOrWhiteSpace(environmentVersion))
                return NormalizeVersion(environmentVersion);

            var gitVersion = GetLatestGitTag();

            if (!string.IsNullOrWhiteSpace(gitVersion))
                return NormalizeVersion(gitVersion);

            return FallbackVersion;
        }

        private static string? GetLatestGitTag()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("for-each-ref");
                startInfo.ArgumentList.Add("--sort=-creatordate");
                startInfo.ArgumentList.Add("--format=%(refname:short)");
                startInfo.ArgumentList.Add("refs/tags");

                using var process = Process.Start(startInfo);

                if (process == null)
                    return null;

                var output = process.StandardOutput.ReadToEnd();

                if (!process.WaitForExit(1500))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Rien à faire.
                    }

                    return null;
                }

                if (process.ExitCode != 0)
                    return null;

                return output
                    .Split(
                        new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            }
            catch
            {
                /*
                 * Git peut ne pas être installé ou le dossier .git
                 * peut ne pas être disponible en production.
                 */
                return null;
            }
        }

        private static string NormalizeVersion(string version)
        {
            version = version.Trim();

            if (string.IsNullOrWhiteSpace(version))
                return FallbackVersion;

            return version.StartsWith(
                "v",
                StringComparison.OrdinalIgnoreCase)
                ? version
                : $"v{version}";
        }
    }
}