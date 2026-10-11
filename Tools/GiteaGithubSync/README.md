# Gitea to GitHub Manual Sync

Manual one-way mirror for Gitea Issues and PR metadata.

Gitea remains the project control plane. GitHub is only a non-LFS Git mirror and
public/backup metadata mirror. This tool does not make GitHub authoritative.

## What It Syncs

- Gitea Issues -> GitHub Issues
- Gitea Pull Requests -> GitHub Issues with `mirror/pr`

PRs are mirrored as GitHub Issues by default because a real GitHub PR requires
the source branch to exist on GitHub. This project normally mirrors only
`origin/main` to GitHub and skips LFS object uploads, so mirroring feature
branches just to recreate PRs would weaken that boundary.

The script writes stable markers into GitHub Issue bodies:

```text
<!-- gitea-issue-id: 12 -->
<!-- gitea-pr-id: 2 -->
```

Those markers let later runs update existing mirrors instead of creating
duplicates.

## Requirements

- `gh` installed and authenticated to GitHub.
- Gitea `origin` remote configured in this repository.
- GitHub `github` remote configured in this repository.
- `GITEA_TOKEN` is optional for public read access, but recommended for private
  repositories or stricter Gitea permissions.

## Usage

Preview Issue sync:

```bash
Tools/GiteaGithubSync/sync_gitea_to_github.py --issues
```

Apply Issue sync:

```bash
Tools/GiteaGithubSync/sync_gitea_to_github.py --issues --apply
```

Preview Issue + PR metadata sync:

```bash
Tools/GiteaGithubSync/sync_gitea_to_github.py --issues --prs
```

Apply Issue + PR metadata sync:

```bash
Tools/GiteaGithubSync/sync_gitea_to_github.py --issues --prs --apply
```

Use explicit repositories if remote inference is not enough:

```bash
Tools/GiteaGithubSync/sync_gitea_to_github.py \
  --gitea-base-url http://192.168.1.210:3002 \
  --gitea-owner vincent \
  --gitea-repo WGameFramework \
  --github-repo WuGuichen/VVV \
  --issues --prs --apply
```

## Boundaries

- One-way only: Gitea -> GitHub.
- No GitHub -> Gitea back-sync.
- No PR review, approval, merge, branch protection, or label policy sync.
- No attempt to preserve matching issue numbers.
- Comments are intentionally not synced in v0.
- Closed Gitea Issues close their GitHub mirror; merged/closed Gitea PR mirrors
  are closed on GitHub.

