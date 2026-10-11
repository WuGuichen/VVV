# ADR-001: Use NAS Gitea As Primary Repository

> Status: ADR

Date: 2026-05-13

Decision Status: Accepted only for repository hosting authority; all task, branch, PR, review, commit, push, and merge mechanics formerly recorded here were rescinded by the repository owner on 2026-08-08.

## Context

MxFramework contains Git LFS assets and uses a NAS-hosted Gitea remote together with a GitHub mirror. The repositories have different storage roles.

## Decision

- NAS Gitea `origin` is the primary Git repository and Git LFS object authority.
- GitHub `github` is a non-LFS Git mirror and is not an LFS backup.
- This ADR defines repository roles only. It does not define a project-specific development or delivery workflow.

## Historical Note

The original decision also required Issues, task branches, pull requests, protected-main delivery, and a merge gate. Those mechanics are rescinded and remain available only through Git history for historical analysis. Their removal does not prescribe a replacement workflow.

## Consequences

- Repository and LFS recovery must use the Gitea-side data and backups.
- GitHub mirroring remains a separate operation whose tooling is documented by `Tools/GiteaGithubSync/README.md`.
- Task execution and Git operations are not governed by this ADR.

## References

- `.gitattributes`
- `Tools/GiteaGithubSync/README.md`
