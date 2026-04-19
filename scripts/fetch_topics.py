"""Fetches trending AI topics from HN, GitHub, and arXiv, plus existing blog posts."""

import json
import os
import sys
import time
import feedparser
import requests

HN_KEYWORDS = ["ai", "llm", "agent", "mcp", "rag", "vector", "gpt", "claude",
               "neural", "transformer", "embedding", "copilot", "openai", "anthropic"]

GITHUB_TOKEN = os.environ["GH_PAT"]
HEADERS = {"Authorization": f"Bearer {GITHUB_TOKEN}", "Accept": "application/vnd.github+json"}


def fetch_hacker_news():
    top_ids = requests.get(
        "https://hacker-news.firebaseio.com/v0/topstories.json", timeout=10
    ).json()

    results = []
    for story_id in top_ids[:200]:
        story = requests.get(
            f"https://hacker-news.firebaseio.com/v0/item/{story_id}.json", timeout=10
        ).json()
        title = (story.get("title") or "").lower()
        if any(kw in title for kw in HN_KEYWORDS):
            results.append({
                "source": "hackernews",
                "title": story.get("title"),
                "url": story.get("url", ""),
                "score": story.get("score", 0),
            })
        if len(results) >= 10:
            break
        time.sleep(0.05)

    return sorted(results, key=lambda x: x["score"], reverse=True)[:5]


def fetch_github_trending():
    response = requests.get(
        "https://api.github.com/search/repositories",
        params={
            "q": "topic:llm stars:>100 pushed:>2026-04-01",
            "sort": "stars",
            "order": "desc",
            "per_page": 5,
        },
        headers=HEADERS,
        timeout=10,
    ).json()

    return [
        {
            "source": "github",
            "title": r["full_name"],
            "description": r.get("description") or "",
            "stars": r["stargazers_count"],
            "url": r["html_url"],
        }
        for r in response.get("items", [])
    ]


def fetch_arxiv():
    feed = feedparser.parse("https://rss.arxiv.org/rss/cs.AI")
    return [
        {
            "source": "arxiv",
            "title": entry.title,
            "description": entry.get("summary", "")[:300],
        }
        for entry in feed.entries[:8]
    ]


def fetch_existing_posts():
    response = requests.get(
        "https://api.github.com/repos/Harry-Zhao-AU/harry-zhao-au.github.io/contents/_posts",
        headers=HEADERS,
        timeout=10,
    )
    if response.status_code != 200:
        return []
    return [f["name"].replace(".md", "") for f in response.json() if isinstance(f, dict)]


if __name__ == "__main__":
    print("Fetching HN...", file=sys.stderr)
    hn = fetch_hacker_news()

    print("Fetching GitHub...", file=sys.stderr)
    gh = fetch_github_trending()

    print("Fetching arXiv...", file=sys.stderr)
    arxiv = fetch_arxiv()

    print("Fetching existing posts...", file=sys.stderr)
    existing = fetch_existing_posts()

    output = {
        "hackernews": hn,
        "github": gh,
        "arxiv": arxiv,
        "existing_posts": existing,
    }

    print(json.dumps(output, indent=2))
