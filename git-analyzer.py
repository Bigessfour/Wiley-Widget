#!/usr/bin/env python3
"""
Git repository analysis for Wiley Widget project
"""

from git import Repo


def analyze_git_repo():
    print("Git Repository Analysis")
    print("=" * 30)

    try:
        repo = Repo(".")

        # Get basic repo info
        print(f"Repository: {repo.working_dir}")
        print(f"Active Branch: {repo.active_branch.name}")
        print(f"Is Dirty: {repo.is_dirty()}")

        # Get recent commits
        print("\nRecent Commits:")
        for commit in list(repo.iter_commits("HEAD", max_count=5)):
            print(f"  {commit.hexsha[:8]} - {commit.message.strip()}")

        # Get file status
        if repo.is_dirty():
            print("\nModified Files:")
            for item in repo.index.diff(None):
                print(f"  {item.a_path} ({item.change_type})")

        # Count commits by author
        print("\nTop Contributors:")
        authors = {}
        for commit in repo.iter_commits():
            author = commit.author.name
            authors[author] = authors.get(author, 0) + 1

        for author, count in sorted(authors.items(), key=lambda x: x[1], reverse=True)[
            :5
        ]:
            print(f"  {author}: {count} commits")

    except Exception as e:
        print(f"Error analyzing git repo: {e}")


if __name__ == "__main__":
    analyze_git_repo()
