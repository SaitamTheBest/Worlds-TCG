import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const notificationMarker = /<!-- discord-notification: \d+ -->/;
const git = (...args) => execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();

export function changelog(commits, repositoryUrl, previous, tag) {
    const groups = new Map([['✨ Nouveautés', []], ['🐛 Corrections', []], ['🔧 Autres changements', []]]);
    for (const { sha, subject } of commits) {
        const heading = /^feat(?:\([^)]*\))?!?:/i.test(subject) ? '✨ Nouveautés' : /^fix(?:\([^)]*\))?!?:/i.test(subject) ? '🐛 Corrections' : '🔧 Autres changements';
        const text = subject.replace(/[\r\n]/g, ' ').replace(/[<>]/g, '').replace(/([\[\]])/g, '\\$1');
        groups.get(heading).push(`- ${text} ([${sha.slice(0, 7)}](${repositoryUrl}/commit/${sha}))`);
    }
    let body = `## Worlds TCG — ${tag}\n\n`;
    for (const [heading, lines] of groups) if (lines.length) body += `### ${heading}\n${lines.join('\n')}\n\n`;
    if (!commits.length) body += 'Aucun nouveau commit depuis la version précédente.\n\n';
    const link = previous ? `${repositoryUrl}/compare/${encodeURIComponent(previous)}...${encodeURIComponent(tag)}` : `${repositoryUrl}/commits/${encodeURIComponent(tag)}`;
    return `${body}[Consulter tous les changements](${link})`;
}

export function discordPayload(release, repository, actor) {
    const body = (release.body || '').replace(notificationMarker, '').trim();
    const summary = body.length > 3400 ? `${body.slice(0, 3300)}\n\n… [Lire le changelog complet](${release.html_url})` : body;
    return {
        username: 'Worlds TCG · Releases',
        allowed_mentions: { parse: [] },
        embeds: [{
            title: `🃏 Worlds TCG · ${release.tag_name}`.slice(0, 256),
            url: release.html_url,
            color: release.prerelease ? 0xf39c12 : 0xf4c430,
            description: summary || 'Une nouvelle version est disponible !',
            fields: [
                { name: '🏷️ Version', value: release.tag_name.slice(0, 1024), inline: true },
                { name: '📦 Publication', value: release.prerelease ? 'Préversion' : 'Version stable', inline: true },
                { name: '🚀 Publiée par', value: actor.slice(0, 1024), inline: true },
                { name: '🔗 GitHub', value: `[Notes complètes](${release.html_url}) · [Dépôt](https://github.com/${repository})` }
            ],
            footer: { text: 'Worlds TCG • Notes de version automatiques' },
            timestamp: release.published_at || new Date().toISOString()
        }]
    };
}

export async function publish(env = process.env, request = fetch, runGit = git) {
    const tag = env.RELEASE_TAG;
    if (!tag || !env.GITHUB_TOKEN || !env.GITHUB_REPOSITORY) throw new Error('Tag, dépôt ou jeton GitHub manquant.');
    runGit('check-ref-format', `refs/tags/${tag}`);
    const sha = runGit('rev-parse', '--verify', `refs/tags/${tag}^{commit}`);
    const apiRoot = env.GITHUB_API_URL || 'https://api.github.com';
    const repositoryUrl = `${env.GITHUB_SERVER_URL || 'https://github.com'}/${env.GITHUB_REPOSITORY}`;
    async function api(path, method = 'GET', body, allowMissing = false) {
        const response = await request(`${apiRoot}/repos/${env.GITHUB_REPOSITORY}${path}`, {
            method, headers: { Authorization: `Bearer ${env.GITHUB_TOKEN}`, Accept: 'application/vnd.github+json', 'Content-Type': 'application/json', 'X-GitHub-Api-Version': '2022-11-28' },
            body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(30000)
        });
        if (allowMissing && response.status === 404) return null;
        if (!response.ok) throw new Error(`GitHub : erreur HTTP ${response.status} (${method}).`);
        return response.json();
    }
    let release = await api(`/releases/tags/${encodeURIComponent(tag)}`, 'GET', undefined, true);
    if (!release) {
        let previous = null;
        try { previous = runGit('describe', '--tags', '--abbrev=0', `--exclude=${tag}`, sha); } catch { /* Première version. */ }
        const range = previous ? `${previous}..${sha}` : sha;
        const commits = runGit('log', '--no-merges', '--format=%H%x09%s', range).split('\n').filter(Boolean).map(line => {
            const index = line.indexOf('\t'); return { sha: line.slice(0, index), subject: line.slice(index + 1) };
        });
        const generated = await api('/releases/generate-notes', 'POST', { tag_name: tag, target_commitish: sha, ...(previous ? { previous_tag_name: previous } : {}) });
        const body = `${changelog(commits, repositoryUrl, previous, tag)}\n\n### Pull requests et contributeurs\n${generated.body || 'Voir la liste des commits ci-dessus.'}`;
        release = await api('/releases', 'POST', { tag_name: tag, target_commitish: sha, name: `Worlds TCG ${tag}`, body: body.slice(0, 120000), prerelease: /-(alpha|beta|rc|preview)([.-]|\d|$)/i.test(tag), draft: false });
    }
    if (release.draft) throw new Error('La release existante est un brouillon : publiez-la avant de notifier Discord.');
    if (!env.DISCORD_WEBHOOK_URL) { console.log('Release disponible. Secret DISCORD_WEBHOOK_URL absent : notification ignorée.'); return release; }
    if (notificationMarker.test(release.body || '')) { console.log('Cette release a déjà été annoncée sur Discord.'); return release; }
    const webhook = new URL(env.DISCORD_WEBHOOK_URL);
    if (webhook.protocol !== 'https:' || !['discord.com', 'canary.discord.com', 'ptb.discord.com'].includes(webhook.hostname) || !/^\/api(?:\/v\d+)?\/webhooks\/\d+\/[^/]+$/.test(webhook.pathname)) throw new Error('Le secret DISCORD_WEBHOOK_URL doit contenir une URL de webhook Discord valide.');
    webhook.searchParams.set('wait', 'true');
    const payload = discordPayload(release, env.GITHUB_REPOSITORY, env.GITHUB_ACTOR || 'GitHub Actions');
    let response;
    for (let attempt = 0; attempt < 4; attempt++) {
        response = await request(webhook, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload), signal: AbortSignal.timeout(30000) });
        if (response.status !== 429) break;
        const limit = await response.json();
        const seconds = Math.min(30, Math.max(1, Number(limit.retry_after) || 5));
        await new Promise(resolve => setTimeout(resolve, seconds * 1000));
    }
    if (!response.ok) throw new Error(`Discord : erreur HTTP ${response.status}. La release reste publiée ; relancez le workflow pour réessayer.`);
    const message = await response.json();
    if (!/^\d+$/.test(message.id || '')) throw new Error('Discord ne confirme pas la création du message.');
    await api(`/releases/${release.id}`, 'PATCH', { body: `${release.body || ''}\n\n<!-- discord-notification: ${message.id} -->` });
    console.log('Release publiée et annoncée sur Discord.');
    return release;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    publish().catch(error => { console.error(error.message); process.exitCode = 1; });
}

