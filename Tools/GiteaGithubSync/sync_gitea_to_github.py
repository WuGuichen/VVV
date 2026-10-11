#!/usr/bin/env python3
"""Manual one-way sync from Gitea Issues/PR metadata to GitHub Issues."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from typing import Any, Dict, Iterable, List, Optional, Tuple


ROOT_LABEL = "mirror/gitea"
ISSUE_LABEL = "mirror/issue"
PR_LABEL = "mirror/pr"
OPEN_LABEL = "gitea/open"
CLOSED_LABEL = "gitea/closed"
MERGED_LABEL = "gitea/merged"


@dataclass(frozen=True)
class GiteaRepo:
    base_url: str
    owner: str
    repo: str


def run(args: List[str], *, input_text: Optional[str] = None) -> str:
    result = subprocess.run(
        args,
        input=input_text,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        command = " ".join(args)
        raise RuntimeError(f"Command failed ({result.returncode}): {command}\n{result.stderr.strip()}")
    return result.stdout


def git_config(key: str) -> Optional[str]:
    result = subprocess.run(
        ["git", "config", "--get", key],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    value = result.stdout.strip()
    return value or None


def infer_gitea_repo() -> GiteaRepo:
    url = git_config("remote.origin.url")
    if not url:
        raise RuntimeError("remote.origin.url is not configured.")

    if url.startswith("http://") or url.startswith("https://"):
        parsed = urllib.parse.urlparse(url)
        parts = parsed.path.strip("/").split("/")
        if len(parts) < 2:
            raise RuntimeError(f"Cannot infer Gitea owner/repo from origin URL: {url}")
        repo = parts[-1]
        if repo.endswith(".git"):
            repo = repo[:-4]
        return GiteaRepo(f"{parsed.scheme}://{parsed.netloc}", parts[-2], repo)

    ssh_match = re.match(r"(?:ssh://)?git@([^/:]+)(?::|/)([^/]+)/(.+?)(?:\.git)?$", url)
    if ssh_match:
        host, owner, repo = ssh_match.groups()
        return GiteaRepo(f"https://{host}", owner, repo)

    raise RuntimeError(f"Cannot infer Gitea repository from origin URL: {url}")


def infer_github_repo() -> str:
    url = git_config("remote.github.url")
    if not url:
        raise RuntimeError("remote.github.url is not configured.")

    ssh_match = re.search(r":([^/:]+)/(.+?)(?:\.git)?$", url)
    if ssh_match:
        return f"{ssh_match.group(1)}/{ssh_match.group(2)}"

    parsed = urllib.parse.urlparse(url)
    parts = parsed.path.strip("/").split("/")
    if len(parts) >= 2:
        repo = parts[-1]
        if repo.endswith(".git"):
            repo = repo[:-4]
        return f"{parts[-2]}/{repo}"

    raise RuntimeError(f"Cannot infer GitHub repository from github remote URL: {url}")


def gitea_api_get(repo: GiteaRepo, path: str, params: Dict[str, str]) -> Any:
    query = urllib.parse.urlencode(params)
    url = f"{repo.base_url}/api/v1{path}"
    if query:
        url = f"{url}?{query}"

    headers = {"Accept": "application/json"}
    token = os.environ.get("GITEA_TOKEN")
    if token:
        headers["Authorization"] = f"token {token}"

    request = urllib.request.Request(url, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Gitea API failed: {url}\n{exc.code} {body}") from exc


def paged(repo: GiteaRepo, path: str, params: Dict[str, str]) -> Iterable[Dict[str, Any]]:
    page = 1
    while True:
        merged = dict(params)
        merged["page"] = str(page)
        merged["limit"] = "50"
        items = gitea_api_get(repo, path, merged)
        if not items:
            break
        for item in items:
            yield item
        if len(items) < 50:
            break
        page += 1


def get_gitea_issues(repo: GiteaRepo, state: str) -> List[Dict[str, Any]]:
    path = f"/repos/{repo.owner}/{repo.repo}/issues"
    issues = []
    for item in paged(repo, path, {"state": state}):
        if item.get("pull_request") is None:
            issues.append(item)
    return issues


def get_gitea_prs(repo: GiteaRepo, state: str) -> List[Dict[str, Any]]:
    path = f"/repos/{repo.owner}/{repo.repo}/pulls"
    return list(paged(repo, path, {"state": state}))


def ensure_labels(github_repo: str, labels: Iterable[str], apply: bool) -> None:
    for label in sorted(set(labels)):
        if not apply:
            continue
        result = subprocess.run(
            ["gh", "label", "create", label, "--repo", github_repo, "--color", label_color(label)],
            text=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            check=False,
        )
        if result.returncode not in (0, 1):
            raise RuntimeError(f"Failed to ensure GitHub label: {label}")


def label_color(label: str) -> str:
    if label == ROOT_LABEL:
        return "6f42c1"
    if label == ISSUE_LABEL:
        return "0969da"
    if label == PR_LABEL:
        return "8250df"
    if label == OPEN_LABEL:
        return "2da44e"
    if label == CLOSED_LABEL:
        return "57606a"
    if label == MERGED_LABEL:
        return "8957e5"
    return "d0d7de"


def gh_api(path: str, fields: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    args = ["gh", "api", "-X", "GET", path]
    if fields:
        for key, value in fields.items():
            args.extend(["-f", f"{key}={value}"])
    output = run(args)
    return json.loads(output)


def load_github_marker_map(github_repo: str) -> Dict[str, int]:
    output = run(
        [
            "gh",
            "issue",
            "list",
            "--repo",
            github_repo,
            "--state",
            "all",
            "--limit",
            "1000",
            "--json",
            "number,body",
        ]
    )
    issues = json.loads(output)
    markers: Dict[str, int] = {}
    for issue in issues:
        body = issue.get("body") or ""
        number = int(issue["number"])
        for match in re.finditer(r"<!-- gitea-(?:issue|pr)-id: \d+ -->", body):
            markers[match.group(0)] = number
    return markers


def state_labels(item_state: str, merged: bool = False) -> List[str]:
    if merged:
        return [MERGED_LABEL]
    if item_state == "closed":
        return [CLOSED_LABEL]
    return [OPEN_LABEL]


def issue_body(repo: GiteaRepo, item: Dict[str, Any]) -> str:
    number = item["number"]
    marker = f"<!-- gitea-issue-id: {number} -->"
    source = item.get("html_url") or f"{repo.base_url}/{repo.owner}/{repo.repo}/issues/{number}"
    body = item.get("body") or ""
    labels = ", ".join(label.get("name", "") for label in item.get("labels") or [] if label.get("name"))
    return "\n".join(
        [
            marker,
            "",
            f"Mirrored from Gitea Issue #{number}.",
            f"Source: {source}",
            f"State: {item.get('state', 'unknown')}",
            f"Gitea labels: {labels or '(none)'}",
            "",
            "---",
            "",
            body,
        ]
    )


def pr_body(repo: GiteaRepo, item: Dict[str, Any]) -> str:
    number = item["number"]
    marker = f"<!-- gitea-pr-id: {number} -->"
    source = item.get("html_url") or f"{repo.base_url}/{repo.owner}/{repo.repo}/pulls/{number}"
    body = item.get("body") or ""
    merged = bool(item.get("merged"))
    head = item.get("head") or {}
    base = item.get("base") or {}
    return "\n".join(
        [
            marker,
            "",
            f"Mirrored from Gitea Pull Request #{number}.",
            f"Source: {source}",
            f"State: {item.get('state', 'unknown')}",
            f"Merged: {'yes' if merged else 'no'}",
            f"Base: {base.get('ref', '(unknown)')} @ {base.get('sha', '(unknown)')}",
            f"Head: {head.get('ref', '(unknown)')} @ {head.get('sha', '(unknown)')}",
            "",
            "This is a metadata mirror, not the authoritative review thread.",
            "",
            "---",
            "",
            body,
        ]
    )


def sync_item(
    *,
    github_repo: str,
    existing_number: Optional[int],
    marker: str,
    title: str,
    body: str,
    labels: List[str],
    close: bool,
    apply: bool,
) -> Tuple[str, Optional[int]]:
    existing = existing_number
    create_label_args: List[str] = []
    edit_label_args: List[str] = []
    for label in labels:
        create_label_args.extend(["--label", label])
        edit_label_args.extend(["--add-label", label])

    if existing is None:
        print(f"create GitHub issue: {title}")
        if not apply:
            return "create", None
        args = ["gh", "issue", "create", "--repo", github_repo, "--title", title, "--body", body]
        args.extend(create_label_args)
        output = run(args).strip()
        number_match = re.search(r"/issues/(\d+)$", output)
        number = int(number_match.group(1)) if number_match else None
    else:
        print(f"update GitHub issue #{existing}: {title}")
        if not apply:
            return "update", existing
        args = ["gh", "issue", "edit", str(existing), "--repo", github_repo, "--title", title, "--body", body]
        args.extend(edit_label_args)
        run(args)
        number = existing

    if apply and number is not None:
        if close:
            subprocess.run(["gh", "issue", "close", str(number), "--repo", github_repo], check=False)
        else:
            subprocess.run(["gh", "issue", "reopen", str(number), "--repo", github_repo], check=False)

    return ("update" if existing else "create"), number


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--issues", action="store_true", help="Sync Gitea Issues to GitHub Issues.")
    parser.add_argument("--prs", action="store_true", help="Sync Gitea Pull Requests as GitHub Issues.")
    parser.add_argument("--apply", action="store_true", help="Actually write to GitHub. Default is dry-run.")
    parser.add_argument("--state", choices=["open", "closed", "all"], default="all")
    parser.add_argument("--gitea-base-url")
    parser.add_argument("--gitea-owner")
    parser.add_argument("--gitea-repo")
    parser.add_argument("--github-repo", help="GitHub repository, for example WuGuichen/VVV.")
    args = parser.parse_args()

    if not args.issues and not args.prs:
        parser.error("Choose at least one of --issues or --prs.")

    inferred_gitea = infer_gitea_repo()
    gitea_repo = GiteaRepo(
        args.gitea_base_url or inferred_gitea.base_url,
        args.gitea_owner or inferred_gitea.owner,
        args.gitea_repo or inferred_gitea.repo,
    )
    github_repo = args.github_repo or infer_github_repo()

    run(["gh", "auth", "status"])
    ensure_labels(
        github_repo,
        [ROOT_LABEL, ISSUE_LABEL, PR_LABEL, OPEN_LABEL, CLOSED_LABEL, MERGED_LABEL],
        args.apply,
    )

    print(f"Gitea:  {gitea_repo.base_url}/{gitea_repo.owner}/{gitea_repo.repo}")
    print(f"GitHub: {github_repo}")
    print(f"Mode:   {'apply' if args.apply else 'dry-run'}")
    marker_map = load_github_marker_map(github_repo)

    if args.issues:
        for item in get_gitea_issues(gitea_repo, args.state):
            number = item["number"]
            marker = f"<!-- gitea-issue-id: {number} -->"
            labels = [ROOT_LABEL, ISSUE_LABEL] + state_labels(item.get("state", "open"))
            sync_item(
                github_repo=github_repo,
                existing_number=marker_map.get(marker),
                marker=marker,
                title=f"[Gitea #{number}] {item.get('title', '(untitled)')}",
                body=issue_body(gitea_repo, item),
                labels=labels,
                close=item.get("state") == "closed",
                apply=args.apply,
            )

    if args.prs:
        for item in get_gitea_prs(gitea_repo, args.state):
            number = item["number"]
            merged = bool(item.get("merged"))
            marker = f"<!-- gitea-pr-id: {number} -->"
            labels = [ROOT_LABEL, PR_LABEL] + state_labels(item.get("state", "open"), merged)
            sync_item(
                github_repo=github_repo,
                existing_number=marker_map.get(marker),
                marker=marker,
                title=f"[Gitea PR #{number}] {item.get('title', '(untitled)')}",
                body=pr_body(gitea_repo, item),
                labels=labels,
                close=item.get("state") == "closed" or merged,
                apply=args.apply,
            )

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise SystemExit(1)
